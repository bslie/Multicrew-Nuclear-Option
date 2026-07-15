using System;
using System.Collections.Generic;
using MulticrewNuclearOption.Core;
using UnityEngine;

namespace MulticrewNuclearOption.Gunner
{
    /// <summary>
    /// Reconstructs the gunner camera on the pilot's local aircraft and feeds it to the cockpit MFD.
    /// </summary>
    public static class PilotGunnerMfdFeed
    {
        private sealed class FeedState
        {
            public uint AircraftNetId;
            public byte Station;
            public Aircraft Aircraft;
            public TargetCam TargetCam;
            public TacScreen TacScreen;
            public Renderer TargetScreenRenderer;
            public Material SavedScreenMaterial;
            public Texture SavedMainTexture;
            public RenderTexture SavedRenderTexture;
            public GameObject CameraRoot;
            public Camera FeedCamera;
            public RenderTexture FeedTexture;
            public ViewS2C LatestView;
            public bool HasView;
        }

        private static readonly Dictionary<long, FeedState> _feeds = new Dictionary<long, FeedState>();
        private static readonly Dictionary<long, ViewS2C> _latestViewStates = new Dictionary<long, ViewS2C>();

        private static long Key(uint aircraftNetId, byte station) => ((long)aircraftNetId << 8) | station;

        public static void UpdateViewState(ViewS2C msg)
        {
            long key = Key(msg.AircraftNetId, msg.Station);
            _latestViewStates[key] = msg;
        }

        public static void ClearViewState(uint aircraftNetId, byte station)
        {
            long key = Key(aircraftNetId, station);
            _latestViewStates.Remove(key);
            if (_feeds.TryGetValue(key, out var feed))
                RestoreFeed(feed);
            _feeds.Remove(key);
        }

        public static void ClearAircraft(uint aircraftNetId)
        {
            var stale = new List<long>();
            foreach (var pair in _feeds)
            {
                if ((uint)(pair.Key >> 8) == aircraftNetId)
                {
                    RestoreFeed(pair.Value);
                    stale.Add(pair.Key);
                }
            }

            foreach (long key in stale)
            {
                _feeds.Remove(key);
                _latestViewStates.Remove(key);
            }
        }

        public static void Reset()
        {
            foreach (var pair in new List<FeedState>(_feeds.Values))
                RestoreFeed(pair);
            _feeds.Clear();
            _latestViewStates.Clear();
        }

        public static void Update()
        {
            if (!GameManager.GetLocalAircraft(out Aircraft localAircraft) || localAircraft == null)
            {
                Reset();
                return;
            }

            var activeKeys = new HashSet<long>();
            foreach (var pair in MulticrewNet.GetActiveRemoteGunnerStations(localAircraft.NetId))
            {
                long key = Key(localAircraft.NetId, pair.station);
                activeKeys.Add(key);

                if (!_latestViewStates.TryGetValue(key, out var view))
                    continue;

                if (!_feeds.TryGetValue(key, out var feed) || feed.Aircraft != localAircraft)
                {
                    if (_feeds.TryGetValue(key, out var oldFeed))
                        RestoreFeed(oldFeed);
                    feed = CreateFeed(localAircraft, pair.station);
                    if (feed == null)
                        continue;
                    _feeds[key] = feed;
                }

                feed.LatestView = view;
                feed.HasView = true;
                RenderFeed(feed);
            }

            var stale = new List<long>();
            foreach (var pair in _feeds)
            {
                if (!activeKeys.Contains(pair.Key))
                    stale.Add(pair.Key);
            }

            foreach (long key in stale)
            {
                RestoreFeed(_feeds[key]);
                _feeds.Remove(key);
                _latestViewStates.Remove(key);
            }
        }

        public static void SendLocalViewStateIfNeeded()
        {
            if (!GunnerState.Active || GunnerState.TargetAircraft == null)
                return;

            var ts = GunnerState.Current;
            if (ts == null)
                return;

            var csm = CameraStateManager.i;
            if (csm == null || csm.mainCamera == null)
                return;

            Transform aircraftTransform = GunnerState.TargetAircraft.transform;
            Transform cameraTransform = csm.mainCamera.transform;
            Vector3 localPos = aircraftTransform.InverseTransformPoint(cameraTransform.position);
            Vector3 localFwd = aircraftTransform.InverseTransformDirection(cameraTransform.forward);
            Vector3 localUp = aircraftTransform.InverseTransformDirection(cameraTransform.up);
            Unit primary = GunnerState.PrimaryTarget();

            MulticrewNet.SendViewState(new ViewC2S
            {
                AircraftNetId = GunnerState.TargetAircraft.NetId,
                Station = ts.Number,
                PosX = localPos.x,
                PosY = localPos.y,
                PosZ = localPos.z,
                FwdX = localFwd.x,
                FwdY = localFwd.y,
                FwdZ = localFwd.z,
                UpX = localUp.x,
                UpY = localUp.y,
                UpZ = localUp.z,
                Fov = csm.mainCamera.fieldOfView,
                PrimaryTargetId = primary != null ? primary.persistentID.Id : 0u,
            });
        }

        private static FeedState CreateFeed(Aircraft aircraft, byte station)
        {
            TargetCam targetCam = aircraft.targetCam ?? aircraft.GetComponentInChildren<TargetCam>(true);
            if (targetCam == null)
                return null;

            Renderer renderer = Reflect.TryGetField<Renderer>(targetCam, "targetScreenRenderer", out var screenRenderer)
                ? screenRenderer
                : null;
            if (renderer == null)
                return null;

            Cockpit cockpit = aircraft.GetComponentInChildren<Cockpit>(true);
            TacScreen tacScreen = null;
            if (cockpit != null)
                Reflect.TryGetField<TacScreen>(cockpit, "tacScreen", out tacScreen);

            var feed = new FeedState
            {
                AircraftNetId = aircraft.NetId,
                Station = station,
                Aircraft = aircraft,
                TargetCam = targetCam,
                TacScreen = tacScreen,
                TargetScreenRenderer = renderer,
                SavedScreenMaterial = renderer.material,
                SavedMainTexture = renderer.material != null ? renderer.material.mainTexture : null,
            };

            if (tacScreen != null && Reflect.TryGetField<RenderTexture>(tacScreen, "renderTexture", out var tacRt))
                feed.SavedRenderTexture = tacRt;

            feed.CameraRoot = new GameObject($"MulticrewGunnerMfdCam_{aircraft.NetId}_{station}");
            feed.CameraRoot.hideFlags = HideFlags.HideAndDontSave;
            feed.FeedCamera = feed.CameraRoot.AddComponent<Camera>();
            feed.FeedCamera.enabled = false;
            feed.FeedCamera.clearFlags = CameraClearFlags.Skybox;
            feed.FeedCamera.nearClipPlane = 0.05f;
            feed.FeedCamera.farClipPlane = 50000f;
            feed.FeedTexture = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
            feed.FeedCamera.targetTexture = feed.FeedTexture;

            if (feed.SavedScreenMaterial != null)
                feed.SavedScreenMaterial.mainTexture = feed.FeedTexture;

            return feed;
        }

        private static void RenderFeed(FeedState feed)
        {
            if (feed == null || !feed.HasView || feed.Aircraft == null || feed.FeedCamera == null)
                return;

            Transform aircraftTransform = feed.Aircraft.transform;
            var view = feed.LatestView;
            Vector3 worldPos = aircraftTransform.TransformPoint(new Vector3(view.PosX, view.PosY, view.PosZ));
            Vector3 worldFwd = aircraftTransform.TransformDirection(new Vector3(view.FwdX, view.FwdY, view.FwdZ));
            Vector3 worldUp = aircraftTransform.TransformDirection(new Vector3(view.UpX, view.UpY, view.UpZ));
            if (worldFwd.sqrMagnitude < 1e-6f)
                worldFwd = aircraftTransform.forward;
            if (worldUp.sqrMagnitude < 1e-6f)
                worldUp = aircraftTransform.up;

            feed.FeedCamera.transform.position = worldPos;
            feed.FeedCamera.transform.rotation = Quaternion.LookRotation(worldFwd.normalized, worldUp.normalized);
            feed.FeedCamera.fieldOfView = view.Fov > 1f ? view.Fov : 60f;
            feed.FeedCamera.Render();
        }

        private static void RestoreFeed(FeedState feed)
        {
            if (feed == null)
                return;

            try
            {
                if (feed.SavedScreenMaterial != null)
                    feed.SavedScreenMaterial.mainTexture = feed.SavedMainTexture;

                if (feed.TargetCam != null)
                {
                    TryCall(feed.TargetCam, "SetTargetCam");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[MFD] Pilot feed restore failed: {e.GetType().Name}: {e.Message}");
            }

            if (feed.FeedTexture != null)
            {
                feed.FeedTexture.Release();
                UnityEngine.Object.Destroy(feed.FeedTexture);
            }

            if (feed.CameraRoot != null)
                UnityEngine.Object.Destroy(feed.CameraRoot);
        }

        private static void TryCall(object instance, string method)
        {
            try
            {
                Reflect.Call(instance, method);
            }
            catch
            {
            }
        }
    }
}
