using System;
using MulticrewNuclearOption.Core;
using UnityEngine;

namespace MulticrewNuclearOption.Gunner
{
    /// <summary>
    /// Forces vanilla cockpit sensor/target displays to initialize for a remote gunner client.
    /// </summary>
    public static class GunnerCockpitDisplay
    {
        internal static bool BypassTargetCamAuthority;

        private static Aircraft _initializedAircraft;

        public static void ForceInit(Aircraft aircraft)
        {
            if (aircraft == null || !GunnerState.Active || GunnerState.TargetAircraft != aircraft)
                return;

            if (_initializedAircraft == aircraft)
                return;

            try
            {
                aircraft.SetCockpitRenderers(true);
                ForceTargetCam(aircraft);
                ForceTacScreen(aircraft);
                _initializedAircraft = aircraft;
                Plugin.LogVerbose($"[MFD] Forced cockpit displays for aircraft netId={aircraft.NetId}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[MFD] Force init failed: {e.GetType().Name}: {e.Message}");
            }
        }

        public static void Cleanup()
        {
            _initializedAircraft = null;
            BypassTargetCamAuthority = false;
        }

        public static bool ShouldForceInit(TargetCam targetCam)
        {
            if (!GunnerState.Active || GunnerState.TargetAircraft == null || targetCam == null)
                return false;

            if (Reflect.TryGetField<Aircraft>(targetCam, "aircraft", out var ac) && ac != null)
                return ac == GunnerState.TargetAircraft;

            return targetCam.GetComponentInParent<Aircraft>() == GunnerState.TargetAircraft;
        }

        private static void ForceTargetCam(Aircraft aircraft)
        {
            TargetCam targetCam = aircraft.targetCam;
            if (targetCam == null)
                targetCam = aircraft.GetComponentInChildren<TargetCam>(true);
            if (targetCam == null)
                return;

            if (Reflect.TryGetField<Camera>(targetCam, "cam", out var cam) && cam != null)
            {
                TryCall(targetCam, "SetTargetCam");
                return;
            }

            BypassTargetCamAuthority = true;
            try
            {
                Reflect.Call(targetCam, "Initialize");
                TryCall(targetCam, "SetTargetCam");
            }
            finally
            {
                BypassTargetCamAuthority = false;
            }
        }

        private static void ForceTacScreen(Aircraft aircraft)
        {
            Cockpit cockpit = FindCockpit(aircraft);
            if (cockpit == null)
                return;

            TacScreen tacScreen = null;
            Reflect.TryGetField<TacScreen>(cockpit, "tacScreen", out tacScreen);
            if (tacScreen == null)
            {
                TryCall(cockpit, "Cockpit_OnAircraftInitialize");
                Reflect.TryGetField<TacScreen>(cockpit, "tacScreen", out tacScreen);
            }

            if (tacScreen == null)
                return;

            if (Reflect.TryGetField<RenderTexture>(tacScreen, "renderTexture", out var rt) && rt != null)
                return;

            Reflect.Call(tacScreen, "Initialize", aircraft, cockpit);
            if (Reflect.TryGetField<Behaviour>(tacScreen, "cam", out var camBehaviour) && camBehaviour != null)
                camBehaviour.enabled = true;
        }

        private static Cockpit FindCockpit(Aircraft aircraft)
        {
            if (aircraft == null)
                return null;

            Cockpit cockpit = aircraft.GetComponentInChildren<Cockpit>(true);
            if (cockpit != null)
                return cockpit;

            if (Reflect.TryGetField<UnitPart>(aircraft, "cockpit", out var cockpitPart) && cockpitPart != null)
                return cockpitPart.GetComponentInChildren<Cockpit>(true);

            return null;
        }

        private static void TryCall(object instance, string method, params object[] args)
        {
            try
            {
                Reflect.Call(instance, method, args);
            }
            catch
            {
                // Optional airframe widgets vary by aircraft.
            }
        }
    }
}
