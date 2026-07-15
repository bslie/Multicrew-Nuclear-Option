using System.Collections.Generic;
using Mirage;
using Mirage.Serialization;
using MulticrewNuclearOption.Gunner;
using UnityEngine;

namespace MulticrewNuclearOption.Core
{
    /// <summary>
    /// Protocol V2 transport for gunner input. The listen-host validates every C2S
    /// message and forwards authorized S2C updates only to the owner or gunner peer.
    /// </summary>
    public static class MulticrewNet
    {
        private static bool _serializersRegistered;
        private static bool _clientHandlersRegistered;
        private static bool _serverHandlersRegistered;
        private static bool _serverEventsBound;

        private static NetworkClient _client;
        private static NetworkServer _server;
        private static readonly MulticrewServerSessions _serverSessions = new MulticrewServerSessions();

        private static readonly Dictionary<long, Vector3> _ownerAim = new Dictionary<long, Vector3>();
        private static readonly Dictionary<long, uint[]> _ownerTargetNetIds = new Dictionary<long, uint[]>();
        private static readonly HashSet<long> _ownerFiring = new HashSet<long>();
        private static readonly HashSet<long> _remoteGunnerStations = new HashSet<long>();
        private static readonly Dictionary<long, float> _lastControlSent = new Dictionary<long, float>();
        private static readonly Dictionary<long, Vector3> _lastAimDirSent = new Dictionary<long, Vector3>();
        private static readonly Dictionary<long, uint[]> _lastTargetsSent = new Dictionary<long, uint[]>();
        private static readonly Dictionary<uint, Aircraft> _ownerSubscribedAircraft = new Dictionary<uint, Aircraft>();
        private static readonly Dictionary<uint, float> _pilotPresenceSeen = new Dictionary<uint, float>();
        private static uint _lastPresenceAircraftNetId;
        private static float _nextPresenceAnnounceTime;
        private static float _nextHeartbeatTime;
        private static uint _nextRequestId;
        private static uint _controlSequence;
        private static uint _targetSequence;
        private static uint _viewSequence;

        private static readonly Dictionary<long, uint> _ownerAppliedTargetId = new Dictionary<long, uint>();
        private static readonly Dictionary<long, uint> _gunnerPlayerNetIds = new Dictionary<long, uint>();
        private static readonly Dictionary<long, ViewS2C> _ownerViewStates = new Dictionary<long, ViewS2C>();
        private static readonly Dictionary<long, float> _lastViewStateSent = new Dictionary<long, float>();

        private static readonly List<long> _ownerTickKeys = new List<long>();
        private static readonly List<long> _ownerTickStale = new List<long>();

        public static bool Initialized { get; private set; }
        public static bool ClientReady => _client != null && _client.Active;
        public static bool ServerReady => _server != null && _server.Active;

        private static long Key(uint netId, byte station) => ((long)netId << 8) | station;

        public static void TryInit()
        {
            if (Initialized && ClientReady)
                return;

            var ac = StationDiscovery.FindAircraft();
            NetworkIdentity anyIdentity = null;
            foreach (var a in ac)
            {
                if (a != null && a.Identity != null)
                {
                    anyIdentity = a.Identity;
                    break;
                }
            }

            if (anyIdentity == null)
                return;

            _client = anyIdentity.Client;
            _server = anyIdentity.Server;

            if (_client == null)
                return;

            try
            {
                RegisterSerializers();
                UnregisterHandlers();
                RegisterHandlers();
                BindServerEvents();
                _serverSessions.Bind(_server);

                if (!ClientReady)
                {
                    Initialized = false;
                    return;
                }

                Initialized = true;
                SendHello();
                Plugin.Log.LogInfo($"[Net] Initialized V{MulticrewProtocol.WireVersion}. server={ServerReady} client={ClientReady}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Net] Init failed: {e}");
            }
        }

        public static void HandleDisconnect()
        {
            if (!Initialized && _client == null)
                return;

            Plugin.Log.LogWarning("[Net] Client disconnected; resetting multicrew state.");
            Reset();
        }

        public static void Reset()
        {
            UnregisterHandlers();
            UnbindServerEvents();

            Initialized = false;
            _client = null;
            _server = null;
            _nextRequestId = 0u;
            _controlSequence = 0u;
            _targetSequence = 0u;
            _viewSequence = 0u;

            _serverSessions.Reset();
            UnsubscribeOwnerAircraft();

            _ownerAim.Clear();
            _ownerTargetNetIds.Clear();
            _ownerFiring.Clear();
            _remoteGunnerStations.Clear();
            _lastControlSent.Clear();
            _lastAimDirSent.Clear();
            _lastTargetsSent.Clear();
            _ownerAppliedTargetId.Clear();
            _gunnerPlayerNetIds.Clear();
            _ownerViewStates.Clear();
            _lastViewStateSent.Clear();
            _pilotPresenceSeen.Clear();
            _lastPresenceAircraftNetId = 0u;
            _nextPresenceAnnounceTime = 0f;
            _nextHeartbeatTime = 0f;

            PilotGunnerMfdFeed.Reset();
            GunnerKillCredit.Reset();
        }

        public static bool IsRemoteGunnerStation(uint netId, byte station)
            => _remoteGunnerStations.Contains(Key(netId, station));

        public static bool TryGetRemoteGunnerAim(uint aircraftNetId, byte station, out Vector3 aimDir)
            => _ownerAim.TryGetValue(Key(aircraftNetId, station), out aimDir);

        public static uint GetRemoteGunnerTargetId(uint aircraftNetId, byte station)
        {
            if (_ownerTargetNetIds.TryGetValue(Key(aircraftNetId, station), out var ids) &&
                ids != null &&
                ids.Length > 0)
            {
                return ids[0];
            }

            return 0u;
        }

        public static void SendJoin(uint netId, byte station, uint gunnerPlayerNetId)
        {
            _nextRequestId++;
            GunnerState.PendingRequestId = _nextRequestId;
            GunnerState.SessionState = GunnerSessionState.JoinPending;
            GunnerState.SessionToken = 0UL;
            GunnerState.SessionGeneration = 0u;

            Send(new JoinReqC2S
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = netId,
                Station = station,
                RequestId = _nextRequestId,
            }, Channel.Reliable);
        }

        public static void SendLeave(uint netId, byte station)
        {
            if (GunnerState.SessionState != GunnerSessionState.Active)
                return;

            Send(new LeaveC2S
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = netId,
                Station = station,
                SessionToken = GunnerState.SessionToken,
                Generation = GunnerState.SessionGeneration,
            }, Channel.Reliable);

            GunnerState.SessionState = GunnerSessionState.Inactive;
            GunnerState.SessionToken = 0UL;
            GunnerState.SessionGeneration = 0u;
        }

        public static void AnnounceLocalPilotPresence()
        {
            if (!ClientReady)
                return;

            Aircraft aircraft = StationDiscovery.FindLocalAircraft();
            if (aircraft == null || aircraft.disabled)
            {
                _lastPresenceAircraftNetId = 0u;
                _nextPresenceAnnounceTime = 0f;
                return;
            }

            if (_lastPresenceAircraftNetId != aircraft.NetId)
            {
                _lastPresenceAircraftNetId = aircraft.NetId;
                _nextPresenceAnnounceTime = 0f;
            }

            if (Time.unscaledTime < _nextPresenceAnnounceTime)
                return;

            _nextPresenceAnnounceTime = Time.unscaledTime + MulticrewProtocol.PresenceAnnounceSeconds;
            _pilotPresenceSeen[aircraft.NetId] = Time.unscaledTime;

            Send(new PresenceC2S { WireVersion = MulticrewProtocol.WireVersion }, Channel.Reliable);
            Plugin.LogVerbose($"[Net] Advertised pilot presence for aircraft netId={aircraft.NetId}.");
        }

        public static bool HasPilotPresence(Aircraft aircraft)
        {
            if (aircraft == null)
                return false;
            if (aircraft.LocalSim)
                return true;
            if (aircraft.Player == null)
                return true;
            if (!Initialized)
                return false;

            if (!_pilotPresenceSeen.TryGetValue(aircraft.NetId, out float lastSeen))
                return false;

            return Time.unscaledTime - lastSeen <= MulticrewProtocol.PresenceTtlSeconds;
        }

        public static void SendAim(uint netId, byte station, Vector3 dir, bool firing, IList<Unit> targets)
        {
            if (GunnerState.SessionState != GunnerSessionState.Active)
                return;

            long key = Key(netId, station);
            float now = Time.unscaledTime;
            if (_lastControlSent.TryGetValue(key, out float last) && now - last < MulticrewProtocol.ControlRateSeconds)
                return;

            if (!MulticrewValidation.TryNormalizeAim(dir.x, dir.y, dir.z, out dir))
                dir = Vector3.forward;

            _lastControlSent[key] = now;
            _lastAimDirSent[key] = dir;
            _controlSequence++;

            Send(new ControlC2S
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = netId,
                Station = station,
                SessionToken = GunnerState.SessionToken,
                Generation = GunnerState.SessionGeneration,
                Sequence = _controlSequence,
                Firing = firing,
                X = dir.x,
                Y = dir.y,
                Z = dir.z,
            }, Channel.Unreliable);

            SendTargetsIfChanged(netId, station, targets);
            SendHeartbeatIfNeeded(netId, station);
        }

        public static void SendFire(uint netId, byte station, bool firing, IList<Unit> targets)
        {
            if (GunnerState.SessionState != GunnerSessionState.Active)
                return;

            long key = Key(netId, station);
            Vector3 dir = _lastAimDirSent.TryGetValue(key, out var cached) ? cached : Vector3.forward;
            _lastControlSent.Remove(key);
            SendAim(netId, station, dir, firing, targets);
        }

        public static void ShareTargets(Aircraft aircraft, byte direction, bool replace, IList<Unit> targets)
        {
            if (aircraft == null)
                return;

            uint[] ids = BuildTargetIds(targets);
            byte station = 0;
            ulong token = 0UL;
            uint generation = 0u;

            if (direction == TargetShareDirection.GunnerToPilot)
            {
                if (GunnerState.SessionState != GunnerSessionState.Active || GunnerState.Current == null)
                    return;
                station = GunnerState.Current.Number;
                token = GunnerState.SessionToken;
                generation = GunnerState.SessionGeneration;
            }

            _targetSequence++;
            Send(new TargetsC2S
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = aircraft.NetId,
                Station = station,
                SessionToken = token,
                Generation = generation,
                Sequence = _targetSequence,
                Replace = replace,
                TargetIds = ids,
            }, Channel.Reliable);
        }

        public static void SendViewState(ViewC2S msg)
        {
            if (!ClientReady || msg.AircraftNetId == 0u || GunnerState.SessionState != GunnerSessionState.Active)
                return;

            long key = Key(msg.AircraftNetId, msg.Station);
            float now = Time.unscaledTime;
            if (_lastViewStateSent.TryGetValue(key, out float last) && now - last < MulticrewProtocol.ViewRateSeconds)
                return;

            _lastViewStateSent[key] = now;
            _viewSequence++;
            msg.WireVersion = MulticrewProtocol.WireVersion;
            msg.SessionToken = GunnerState.SessionToken;
            msg.Generation = GunnerState.SessionGeneration;
            msg.Sequence = _viewSequence;
            Send(msg, Channel.Unreliable);
        }

        public static void SendHitFeedbackFromOwner(uint aircraftNetId, byte station, GlobalPosition hitPosition, Unit hitUnit)
        {
            if (!ClientReady)
                return;

            if (!_gunnerPlayerNetIds.TryGetValue(Key(aircraftNetId, station), out _))
                return;

            Send(new HitFeedbackC2S
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = aircraftNetId,
                Station = station,
                SessionToken = 0UL,
                Generation = 0u,
                HitX = hitPosition.x,
                HitY = hitPosition.y,
                HitZ = hitPosition.z,
                HitUnitId = hitUnit != null ? hitUnit.persistentID.Id : 0u,
            }, Channel.Unreliable);
        }

        public static void RegisterGunnerPlayer(long stationKey, uint gunnerPlayerNetId)
        {
            if (gunnerPlayerNetId == 0u)
                return;
            _gunnerPlayerNetIds[stationKey] = gunnerPlayerNetId;
        }

        public static void UnregisterGunnerPlayer(long stationKey)
            => _gunnerPlayerNetIds.Remove(stationKey);

        public static bool TryGetGunnerPlayerNetId(uint aircraftNetId, byte station, out uint gunnerPlayerNetId)
        {
            return _gunnerPlayerNetIds.TryGetValue(Key(aircraftNetId, station), out gunnerPlayerNetId) &&
                   gunnerPlayerNetId != 0u;
        }

        public static bool TryGetFiringGunnerStation(uint aircraftNetId, out byte station)
        {
            foreach (long key in _ownerFiring)
            {
                if ((uint)(key >> 8) != aircraftNetId)
                    continue;

                station = (byte)(key & 0xFF);
                if (IsRemoteGunnerStation(aircraftNetId, station))
                    return true;
            }

            station = 0;
            return false;
        }

        public static IEnumerable<(byte station, uint gunnerPlayerNetId)> GetActiveRemoteGunnerStations(uint aircraftNetId)
        {
            var seen = new HashSet<byte>();
            foreach (long key in _remoteGunnerStations)
            {
                if ((uint)(key >> 8) != aircraftNetId)
                    continue;

                byte station = (byte)(key & 0xFF);
                if (!seen.Add(station))
                    continue;

                _gunnerPlayerNetIds.TryGetValue(key, out uint gunnerNetId);
                yield return (station, gunnerNetId);
            }
        }

        public static uint GetLocalPlayerNetId()
        {
            if (GameManager.GetLocalPlayer(out NuclearOption.Networking.Player localPlayer) && localPlayer != null)
                return localPlayer.NetId;
            return 0u;
        }

        public static void OwnerTick()
        {
            if (ServerReady)
                _serverSessions.Tick(Time.unscaledTime);

            PruneLocalPresence();
            ApplyFireWatchdog();

            if (_ownerAim.Count == 0 && _ownerFiring.Count == 0)
                return;

            _ownerTickKeys.Clear();
            foreach (var key in _ownerAim.Keys)
                _ownerTickKeys.Add(key);
            foreach (var key in _ownerFiring)
            {
                if (!_ownerTickKeys.Contains(key))
                    _ownerTickKeys.Add(key);
            }

            _ownerTickStale.Clear();
            foreach (var key in _ownerTickKeys)
            {
                if (!ApplyOwnerStation(key, _ownerAim.TryGetValue(key, out var dir) ? dir : Vector3.forward, _ownerFiring.Contains(key)))
                    _ownerTickStale.Add(key);
            }

            foreach (var key in _ownerTickStale)
            {
                uint netId = (uint)(key >> 8);
                byte station = (byte)(key & 0xFF);
                var ts = ResolveOwned(netId, station, allowDisabled: true);
                CleanupOwnerStation(netId, station, ts, restoreStationActive: false);
                if (!HasOwnerStateForAircraft(netId))
                    UnsubscribeOwnerAircraft(netId);
            }
        }

        private static void SendHello()
        {
            Send(new HelloC2S { WireVersion = MulticrewProtocol.WireVersion }, Channel.Reliable);
        }

        private static void SendHeartbeatIfNeeded(uint netId, byte station)
        {
            if (Time.unscaledTime < _nextHeartbeatTime)
                return;

            _nextHeartbeatTime = Time.unscaledTime + MulticrewProtocol.HeartbeatSeconds;
            Send(new HeartbeatC2S
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = netId,
                Station = station,
                SessionToken = GunnerState.SessionToken,
                Generation = GunnerState.SessionGeneration,
            }, Channel.Unreliable);
        }

        private static void SendTargetsIfChanged(uint netId, byte station, IList<Unit> targets)
        {
            uint[] ids = BuildTargetIds(targets);
            long key = Key(netId, station);
            if (_lastTargetsSent.TryGetValue(key, out var previous) && TargetArraysEqual(previous, ids))
                return;

            _lastTargetsSent[key] = ids;
            _targetSequence++;
            Send(new TargetsC2S
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = netId,
                Station = station,
                SessionToken = GunnerState.SessionToken,
                Generation = GunnerState.SessionGeneration,
                Sequence = _targetSequence,
                Replace = true,
                TargetIds = ids,
            }, Channel.Reliable);
        }

        private static bool TargetArraysEqual(uint[] left, uint[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static void PruneLocalPresence()
        {
            float now = Time.unscaledTime;
            var stale = new List<uint>();
            foreach (var pair in _pilotPresenceSeen)
            {
                if (now - pair.Value > MulticrewProtocol.PresenceTtlSeconds)
                    stale.Add(pair.Key);
            }

            foreach (uint netId in stale)
                _pilotPresenceSeen.Remove(netId);
        }

        private static void ApplyFireWatchdog()
        {
            // Server-side watchdog also clears firing; this protects owner-only paths.
            if (_ownerFiring.Count == 0)
                return;
        }

        private static void Send<T>(T msg, Channel channel)
        {
            if (!ClientReady)
                return;

            try
            {
                _client.Send(msg, channel);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Net] Send<{typeof(T).Name}> failed: {e.Message}");
            }
        }

        private static void BindServerEvents()
        {
            if (_server == null || _serverEventsBound)
                return;

            _server.Disconnected.AddListener(OnServerPlayerDisconnected);
            _serverEventsBound = true;
        }

        private static void UnbindServerEvents()
        {
            if (_server != null && _serverEventsBound)
                _server.Disconnected.RemoveListener(OnServerPlayerDisconnected);
            _serverEventsBound = false;
        }

        private static void OnServerPlayerDisconnected(INetworkPlayer player)
            => _serverSessions.OnDisconnected(player);

        private static void RegisterHandlers()
        {
            if (_client?.MessageHandler != null && !_clientHandlersRegistered)
            {
                var handler = _client.MessageHandler;
                handler.RegisterHandler<HelloS2C>((p, m) => OnHelloS2C(m), false);
                handler.RegisterHandler<PresenceS2C>((p, m) => OnPresenceS2C(m), false);
                handler.RegisterHandler<PresenceRemovedS2C>((p, m) => OnPresenceRemovedS2C(m), false);
                handler.RegisterHandler<JoinGrantedS2C>((p, m) => OnJoinGrantedS2C(m), false);
                handler.RegisterHandler<JoinRejectedS2C>((p, m) => OnJoinRejectedS2C(m), false);
                handler.RegisterHandler<LeaseRevokedS2C>((p, m) => OnLeaseRevokedS2C(m), false);
                handler.RegisterHandler<LeaseActivatedS2C>((p, m) => OnLeaseActivatedS2C(m), false);
                handler.RegisterHandler<ControlS2C>((p, m) => OnControlS2C(m), false);
                handler.RegisterHandler<TargetsS2C>((p, m) => OnTargetsS2C(m), false);
                handler.RegisterHandler<ViewS2C>((p, m) => OnViewS2C(m), false);
                handler.RegisterHandler<HitFeedbackS2C>((p, m) => OnHitFeedbackS2C(m), false);
                _clientHandlersRegistered = true;
            }

            if (_server?.MessageHandler != null && !_serverHandlersRegistered)
            {
                var handler = _server.MessageHandler;
                handler.RegisterHandler<HelloC2S>((p, m) => _serverSessions.HandleHello(p, m), false);
                handler.RegisterHandler<PresenceC2S>((p, m) => _serverSessions.HandlePresence(p, m), false);
                handler.RegisterHandler<JoinReqC2S>((p, m) => _serverSessions.HandleJoin(p, m), false);
                handler.RegisterHandler<LeaveC2S>((p, m) => _serverSessions.HandleLeave(p, m), false);
                handler.RegisterHandler<HeartbeatC2S>((p, m) => _serverSessions.HandleHeartbeat(p, m), false);
                handler.RegisterHandler<ControlC2S>((p, m) => _serverSessions.HandleControl(p, m), false);
                handler.RegisterHandler<TargetsC2S>((p, m) => _serverSessions.HandleTargets(p, m), false);
                handler.RegisterHandler<ViewC2S>((p, m) => _serverSessions.HandleView(p, m), false);
                handler.RegisterHandler<HitFeedbackC2S>((p, m) => _serverSessions.HandleHitFeedback(p, m), false);
                _serverHandlersRegistered = true;
            }
        }

        private static void UnregisterHandlers()
        {
            if (_client?.MessageHandler != null && _clientHandlersRegistered)
            {
                var handler = _client.MessageHandler;
                handler.UnregisterHandler<HelloS2C>();
                handler.UnregisterHandler<PresenceS2C>();
                handler.UnregisterHandler<PresenceRemovedS2C>();
                handler.UnregisterHandler<JoinGrantedS2C>();
                handler.UnregisterHandler<JoinRejectedS2C>();
                handler.UnregisterHandler<LeaseRevokedS2C>();
                handler.UnregisterHandler<LeaseActivatedS2C>();
                handler.UnregisterHandler<ControlS2C>();
                handler.UnregisterHandler<TargetsS2C>();
                handler.UnregisterHandler<ViewS2C>();
                handler.UnregisterHandler<HitFeedbackS2C>();
                _clientHandlersRegistered = false;
            }

            if (_server?.MessageHandler != null && _serverHandlersRegistered)
            {
                var handler = _server.MessageHandler;
                handler.UnregisterHandler<HelloC2S>();
                handler.UnregisterHandler<PresenceC2S>();
                handler.UnregisterHandler<JoinReqC2S>();
                handler.UnregisterHandler<LeaveC2S>();
                handler.UnregisterHandler<HeartbeatC2S>();
                handler.UnregisterHandler<ControlC2S>();
                handler.UnregisterHandler<TargetsC2S>();
                handler.UnregisterHandler<ViewC2S>();
                handler.UnregisterHandler<HitFeedbackC2S>();
                _serverHandlersRegistered = false;
            }
        }

        private static void OnHelloS2C(HelloS2C msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
            {
                Plugin.Log.LogWarning($"[Net] Host protocol mismatch: {msg.WireVersion}.");
                return;
            }
        }

        private static void OnPresenceS2C(PresenceS2C msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;

            _pilotPresenceSeen[msg.AircraftNetId] = Time.unscaledTime;
        }

        private static void OnPresenceRemovedS2C(PresenceRemovedS2C msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;

            _pilotPresenceSeen.Remove(msg.AircraftNetId);
        }

        private static void OnJoinGrantedS2C(JoinGrantedS2C msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;
            if (msg.RequestId != GunnerState.PendingRequestId)
            {
                Send(new LeaveC2S
                {
                    WireVersion = MulticrewProtocol.WireVersion,
                    AircraftNetId = msg.AircraftNetId,
                    Station = msg.Station,
                    SessionToken = msg.SessionToken,
                    Generation = msg.Generation,
                }, Channel.Reliable);
                return;
            }

            GunnerState.SessionState = GunnerSessionState.Active;
            GunnerState.SessionToken = msg.SessionToken;
            GunnerState.SessionGeneration = msg.Generation;
            Plugin.Log.LogInfo($"[Net] Join granted for aircraft={msg.AircraftNetId} station={msg.Station}.");
        }

        private static void OnJoinRejectedS2C(JoinRejectedS2C msg)
        {
            if (msg.RequestId != GunnerState.PendingRequestId)
                return;

            GunnerState.SessionState = GunnerSessionState.Inactive;
            GunnerState.SessionToken = 0UL;
            GunnerState.SessionGeneration = 0u;
            Plugin.Log.LogWarning($"[Net] Join rejected: reason={msg.Reason}.");
        }

        private static void OnLeaseRevokedS2C(LeaseRevokedS2C msg)
        {
            var ts = ResolveOwned(msg.AircraftNetId, msg.Station, allowDisabled: true);
            CleanupOwnerStation(msg.AircraftNetId, msg.Station, ts, restoreStationActive: true);

            if (GunnerState.Active &&
                GunnerState.TargetAircraft != null &&
                GunnerState.TargetAircraft.NetId == msg.AircraftNetId &&
                GunnerState.Current != null &&
                GunnerState.Current.Number == msg.Station &&
                GunnerState.SessionToken == msg.SessionToken &&
                GunnerState.SessionGeneration == msg.Generation)
            {
                GunnerState.SessionState = GunnerSessionState.Inactive;
                GunnerState.SessionToken = 0UL;
                GunnerState.SessionGeneration = 0u;
            }
        }

        private static void OnLeaseActivatedS2C(LeaseActivatedS2C msg)
        {
            var ts = ResolveOwned(msg.AircraftNetId, msg.Station);
            if (ts == null)
                return;

            SubscribeOwnerAircraft(ts.Aircraft);
            long key = Key(msg.AircraftNetId, msg.Station);
            _remoteGunnerStations.Add(key);
            if (msg.GunnerPlayerNetId != 0u)
            {
                _gunnerPlayerNetIds[key] = msg.GunnerPlayerNetId;
                GunnerKillCredit.RegisterGunnerStation(msg.AircraftNetId, msg.Station, msg.GunnerPlayerNetId);
            }

            TurretController.EngageManual(ts);
            TurretController.ReleaseTurretTargetLock(ts);
            Plugin.Log.LogInfo($"[Net] Owner engaged station {msg.Station} for remote gunner.");
        }

        private static void OnControlS2C(ControlS2C msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;
            if (ResolveOwned(msg.AircraftNetId, msg.Station) == null)
                return;
            if (!_remoteGunnerStations.Contains(Key(msg.AircraftNetId, msg.Station)))
                return;
            if (!MulticrewValidation.TryNormalizeAim(msg.X, msg.Y, msg.Z, out Vector3 aim))
                return;

            long key = Key(msg.AircraftNetId, msg.Station);
            _ownerAim[key] = aim;
            if (msg.Firing)
                _ownerFiring.Add(key);
            else
                _ownerFiring.Remove(key);
        }

        private static void OnTargetsS2C(TargetsS2C msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;
            if (!MulticrewValidation.TrySanitizeTargetIds(msg.TargetIds, out uint[] targetIds))
                return;

            if (msg.Direction == TargetShareDirection.GunnerToPilot)
            {
                var ac = ResolveOwnedAircraft(msg.AircraftNetId);
                if (ac == null)
                    return;

                long key = Key(msg.AircraftNetId, msg.Station);
                _ownerTargetNetIds[key] = targetIds;
                ApplyTargetsToPilot(ac, ResolveTargets(targetIds), msg.Replace);
                return;
            }

            if (msg.Direction == TargetShareDirection.PilotToGunner)
            {
                if (!GunnerState.Active || GunnerState.TargetAircraft == null)
                    return;
                if (GunnerState.TargetAircraft.NetId != msg.AircraftNetId)
                    return;
                if (GunnerState.Current == null || GunnerState.Current.Number != msg.Station)
                    return;
                ApplyTargetsToGunner(ResolveTargets(targetIds), msg.Replace);
            }
        }

        private static void OnViewS2C(ViewS2C msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;
            if (ResolveOwnedAircraft(msg.AircraftNetId) == null)
                return;

            long key = Key(msg.AircraftNetId, msg.Station);
            _ownerViewStates[key] = msg;
            PilotGunnerMfdFeed.UpdateViewState(msg);
        }

        private static void OnHitFeedbackS2C(HitFeedbackS2C msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;
            if (!GunnerState.Active || GunnerState.TargetAircraft == null)
                return;
            if (GunnerState.TargetAircraft.NetId != msg.AircraftNetId)
                return;

            var ts = GunnerState.Current;
            if (ts == null || ts.Number != msg.Station)
                return;

            var hud = CombatHUD.i;
            if (hud == null || hud.aircraft != GunnerState.TargetAircraft)
                return;

            Unit hitUnit = null;
            if (msg.HitUnitId != 0u)
            {
                PersistentID hitId = default;
                hitId.Id = msg.HitUnitId;
                UnitRegistry.TryGetUnit(hitId, out hitUnit);
            }

            var hitPosition = new GlobalPosition(msg.HitX, msg.HitY, msg.HitZ);
            hud.DisplayHit(hitPosition, hitUnit);
        }

        private static void SubscribeOwnerAircraft(Aircraft aircraft)
        {
            if (aircraft == null || _ownerSubscribedAircraft.ContainsKey(aircraft.NetId))
                return;

            aircraft.onDisableUnit += OnOwnedAircraftDisabled;
            _ownerSubscribedAircraft[aircraft.NetId] = aircraft;
        }

        private static void UnsubscribeOwnerAircraft(uint netId)
        {
            if (!_ownerSubscribedAircraft.TryGetValue(netId, out var aircraft))
                return;

            if (aircraft != null)
                aircraft.onDisableUnit -= OnOwnedAircraftDisabled;
            _ownerSubscribedAircraft.Remove(netId);
        }

        private static void UnsubscribeOwnerAircraft()
        {
            foreach (var pair in new List<KeyValuePair<uint, Aircraft>>(_ownerSubscribedAircraft))
            {
                if (pair.Value != null)
                    pair.Value.onDisableUnit -= OnOwnedAircraftDisabled;
            }
            _ownerSubscribedAircraft.Clear();
        }

        private static void OnOwnedAircraftDisabled(Unit unit)
        {
            try
            {
                if (unit is Aircraft aircraft)
                {
                    if (ServerReady)
                        _serverSessions.OnAircraftDisabled(aircraft.NetId);
                    CleanupOwnerAircraft(aircraft, "disabled");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[Net] onDisableUnit cleanup error (ignored): {e.GetType().Name}: {e.Message}");
            }
        }

        private static void CleanupOwnerAircraft(Aircraft aircraft, string reason)
        {
            if (aircraft == null)
                return;

            uint netId = aircraft.NetId;
            _pilotPresenceSeen.Remove(netId);
            if (_lastPresenceAircraftNetId == netId)
            {
                _lastPresenceAircraftNetId = 0u;
                _nextPresenceAnnounceTime = 0f;
            }

            var stationLookup = new Dictionary<byte, TurretStation>();
            foreach (var station in StationDiscovery.GetGunnerStations(aircraft))
            {
                if (station != null)
                    stationLookup[station.Number] = station;
            }

            var keys = new HashSet<long>();
            CollectOwnerKeysForAircraft(_remoteGunnerStations, netId, keys);
            CollectOwnerKeysForAircraft(_ownerAim.Keys, netId, keys);
            CollectOwnerKeysForAircraft(_ownerTargetNetIds.Keys, netId, keys);
            CollectOwnerKeysForAircraft(_ownerFiring, netId, keys);
            CollectOwnerKeysForAircraft(_ownerAppliedTargetId.Keys, netId, keys);

            foreach (long key in keys)
            {
                byte stationNumber = (byte)(key & 0xFF);
                stationLookup.TryGetValue(stationNumber, out var station);
                CleanupOwnerStation(netId, stationNumber, station, restoreStationActive: false);
            }

            UnsubscribeOwnerAircraft(netId);
            PilotGunnerMfdFeed.ClearAircraft(netId);

            if (keys.Count > 0)
                Plugin.Log.LogInfo($"[Net] Cleared {keys.Count} remote gunner station(s) for aircraft {netId} ({reason}).");
        }

        private static void CollectOwnerKeysForAircraft(IEnumerable<long> source, uint netId, HashSet<long> keys)
        {
            foreach (long key in source)
            {
                if ((uint)(key >> 8) == netId)
                    keys.Add(key);
            }
        }

        private static bool HasOwnerStateForAircraft(uint netId)
        {
            foreach (long key in _remoteGunnerStations)
                if ((uint)(key >> 8) == netId) return true;
            foreach (long key in _ownerAim.Keys)
                if ((uint)(key >> 8) == netId) return true;
            foreach (long key in _ownerTargetNetIds.Keys)
                if ((uint)(key >> 8) == netId) return true;
            foreach (long key in _ownerFiring)
                if ((uint)(key >> 8) == netId) return true;
            foreach (long key in _ownerAppliedTargetId.Keys)
                if ((uint)(key >> 8) == netId) return true;
            return false;
        }

        private static void CleanupOwnerStation(uint netId, byte station, TurretStation ts, bool restoreStationActive)
        {
            long key = Key(netId, station);
            _ownerAim.Remove(key);
            _ownerTargetNetIds.Remove(key);
            _ownerFiring.Remove(key);
            _remoteGunnerStations.Remove(key);
            _gunnerPlayerNetIds.Remove(key);
            _ownerViewStates.Remove(key);
            _lastViewStateSent.Remove(key);
            _lastControlSent.Remove(key);
            _lastAimDirSent.Remove(key);
            _lastTargetsSent.Remove(key);
            GunnerKillCredit.UnregisterGunnerStation(netId, station);
            PilotGunnerMfdFeed.ClearViewState(netId, station);

            bool hadTarget = _ownerAppliedTargetId.TryGetValue(key, out var appliedId) && appliedId != 0u;
            _ownerAppliedTargetId.Remove(key);

            if (ts == null)
                return;

            if (hadTarget && ts.Aircraft != null && !ts.Aircraft.disabled)
                TurretController.ApplyStationTarget(ts, null);

            TurretController.ReleaseManual(ts);
            if (restoreStationActive &&
                ts.Aircraft != null &&
                !ts.Aircraft.disabled &&
                ts.Aircraft?.weaponManager?.currentWeaponStation == ts.Station)
            {
                ts.Station.SetStationActive(ts.Aircraft, true);
            }
        }

        private static bool ApplyOwnerStation(long key, Vector3 aimDir, bool fire)
        {
            uint netId = (uint)(key >> 8);
            byte station = (byte)(key & 0xFF);
            var ts = ResolveOwned(netId, station);
            if (ts == null)
                return false;

            List<Unit> targets = ResolveOwnerTargets(key);
            Unit primary = targets.Count > 0 ? targets[0] : null;
            if (ts.HasTurret)
            {
                uint desiredId = primary != null ? primary.persistentID.Id : 0u;
                uint appliedId = _ownerAppliedTargetId.TryGetValue(key, out var prev) ? prev : 0u;
                if (desiredId != appliedId || !TurretController.StationTurretTargetsMatch(ts, desiredId))
                {
                    _ownerAppliedTargetId[key] = desiredId;
                    TurretController.ApplyStationTarget(ts, primary);
                }

                if (primary == null)
                    TurretController.Aim(ts, aimDir, null, driveTurretAim: true);
            }

            if (fire)
                GunnerWeaponFire.FireStation(ts, primary, targets);
            return true;
        }

        private static TurretStation ResolveOwned(uint netId, byte station, bool allowDisabled = false)
        {
            var ac = ResolveOwnedAircraft(netId, allowDisabled);
            if (ac == null)
                return null;

            foreach (var ts in StationDiscovery.GetGunnerStations(ac))
            {
                if (ts.Number == station)
                    return ts;
            }

            return null;
        }

        private static Aircraft ResolveOwnedAircraft(uint netId, bool allowDisabled = false)
        {
            if (_client == null || _client.World == null)
                return null;
            if (!_client.World.TryGetIdentity(netId, out var identity) || identity == null)
                return null;

            var ac = identity.GetComponent<Aircraft>();
            return ac != null && (allowDisabled || !ac.disabled) && ac.LocalSim ? ac : null;
        }

        private static List<Unit> ResolveOwnerTargets(long key)
        {
            if (!_ownerTargetNetIds.TryGetValue(key, out var netIds) || netIds == null || netIds.Length == 0)
                return new List<Unit>();
            return ResolveTargets(netIds);
        }

        private static uint[] BuildTargetIds(IList<Unit> targets)
            => TargetListUtil.BuildPersistentIds(targets, MulticrewProtocol.MaxSharedTargets);

        private static List<Unit> ResolveTargets(uint[] targetPersistentIds)
            => TargetListUtil.ResolvePersistentIds(targetPersistentIds);

        private static void ApplyTargetsToPilot(Aircraft aircraft, List<Unit> targets, bool replace)
        {
            if (aircraft == null || aircraft.weaponManager == null)
                return;

            var hud = CombatHUD.i;
            bool hudIsGunnerView = GunnerState.Active && GunnerState.TargetAircraft == aircraft;
            bool useHud = !hudIsGunnerView && hud != null && hud.aircraft == aircraft;
            if (replace)
            {
                if (useHud) hud.DeselectAll(false);
                else aircraft.weaponManager.ClearTargetList();
            }

            var current = aircraft.weaponManager.GetTargetList();
            foreach (var target in targets)
            {
                if (target == null || current.Contains(target))
                    continue;
                if (useHud) hud.SelectUnit(target);
                else aircraft.weaponManager.AddTargetList(target);
            }
        }

        private static void ApplyTargetsToGunner(List<Unit> targets, bool replace)
        {
            var hud = CombatHUD.i;
            bool useHud = hud != null && GunnerState.TargetAircraft != null && hud.aircraft == GunnerState.TargetAircraft;
            if (replace)
            {
                if (useHud) hud.DeselectAll(false);
                else GunnerState.TargetList.Clear();
            }

            foreach (var target in targets)
            {
                if (target == null || GunnerState.TargetList.Contains(target))
                    continue;
                if (useHud) hud.SelectUnit(target);
                else GunnerState.TargetList.Add(target);
            }
        }

        private static void RegisterSerializers()
        {
            if (_serializersRegistered)
                return;
            _serializersRegistered = true;

            RegisterSimple<HelloC2S>(
                (w, m) => w.WriteByte(m.WireVersion),
                r => new HelloC2S { WireVersion = r.ReadByte() });
            RegisterSimple<HelloS2C>(
                (w, m) => w.WriteByte(m.WireVersion),
                r => new HelloS2C { WireVersion = r.ReadByte() });

            RegisterSimple<PresenceC2S>(
                (w, m) => w.WriteByte(m.WireVersion),
                r => new PresenceC2S { WireVersion = r.ReadByte() });

            Writer<PresenceS2C>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteUInt32(m.PilotPlayerNetId);
            };
            Reader<PresenceS2C>.Read = r => new PresenceS2C
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                PilotPlayerNetId = r.ReadUInt32(),
            };

            Writer<PresenceRemovedS2C>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
            };
            Reader<PresenceRemovedS2C>.Read = r => new PresenceRemovedS2C
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
            };

            Writer<JoinReqC2S>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt32(m.RequestId);
            };
            Reader<JoinReqC2S>.Read = r => new JoinReqC2S
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                Station = r.ReadByte(),
                RequestId = r.ReadUInt32(),
            };

            RegisterJoinGranted();
            RegisterJoinRejected();
            RegisterLeave();
            RegisterLeaseRevoked();
            RegisterLeaseActivated();
            RegisterControl();
            RegisterTargets();
            RegisterView();
            RegisterHitFeedback();
            RegisterHeartbeat();
        }

        private static void RegisterSimple<T>(
            System.Action<NetworkWriter, T> write,
            System.Func<NetworkReader, T> read)
        {
            Writer<T>.Write = write;
            Reader<T>.Read = read;
        }

        private static void RegisterJoinGranted()
        {
            Writer<JoinGrantedS2C>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt32(m.RequestId);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
                w.WriteUInt32(m.GunnerPlayerNetId);
            };
            Reader<JoinGrantedS2C>.Read = r => new JoinGrantedS2C
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                Station = r.ReadByte(),
                RequestId = r.ReadUInt32(),
                SessionToken = r.ReadUInt64(),
                Generation = r.ReadUInt32(),
                GunnerPlayerNetId = r.ReadUInt32(),
            };
        }

        private static void RegisterJoinRejected()
        {
            Writer<JoinRejectedS2C>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.RequestId);
                w.WriteByte(m.Reason);
            };
            Reader<JoinRejectedS2C>.Read = r => new JoinRejectedS2C
            {
                WireVersion = r.ReadByte(),
                RequestId = r.ReadUInt32(),
                Reason = r.ReadByte(),
            };
        }

        private static void RegisterLeave()
        {
            Writer<LeaveC2S>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
            };
            Reader<LeaveC2S>.Read = r => new LeaveC2S
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                Station = r.ReadByte(),
                SessionToken = r.ReadUInt64(),
                Generation = r.ReadUInt32(),
            };
        }

        private static void RegisterLeaseRevoked()
        {
            Writer<LeaseRevokedS2C>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
                w.WriteByte(m.Reason);
            };
            Reader<LeaseRevokedS2C>.Read = r => new LeaseRevokedS2C
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                Station = r.ReadByte(),
                SessionToken = r.ReadUInt64(),
                Generation = r.ReadUInt32(),
                Reason = r.ReadByte(),
            };
        }

        private static void RegisterLeaseActivated()
        {
            Writer<LeaseActivatedS2C>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
                w.WriteUInt32(m.GunnerPlayerNetId);
            };
            Reader<LeaseActivatedS2C>.Read = r => new LeaseActivatedS2C
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                Station = r.ReadByte(),
                SessionToken = r.ReadUInt64(),
                Generation = r.ReadUInt32(),
                GunnerPlayerNetId = r.ReadUInt32(),
            };
        }

        private static void RegisterControl()
        {
            System.Action<NetworkWriter, ControlC2S> write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
                w.WriteUInt32(m.Sequence);
                w.WriteBoolean(m.Firing);
                w.WriteSingle(m.X);
                w.WriteSingle(m.Y);
                w.WriteSingle(m.Z);
            };
            System.Func<NetworkReader, ControlC2S> read = r => new ControlC2S
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                Station = r.ReadByte(),
                SessionToken = r.ReadUInt64(),
                Generation = r.ReadUInt32(),
                Sequence = r.ReadUInt32(),
                Firing = r.ReadBoolean(),
                X = r.ReadSingle(),
                Y = r.ReadSingle(),
                Z = r.ReadSingle(),
            };
            Writer<ControlC2S>.Write = write;
            Reader<ControlC2S>.Read = read;
            Writer<ControlS2C>.Write = (w, m) => write(w, new ControlC2S
            {
                WireVersion = m.WireVersion,
                AircraftNetId = m.AircraftNetId,
                Station = m.Station,
                SessionToken = m.SessionToken,
                Generation = m.Generation,
                Sequence = m.Sequence,
                Firing = m.Firing,
                X = m.X,
                Y = m.Y,
                Z = m.Z,
            });
            Reader<ControlS2C>.Read = r =>
            {
                var msg = read(r);
                return new ControlS2C
                {
                    WireVersion = msg.WireVersion,
                    AircraftNetId = msg.AircraftNetId,
                    Station = msg.Station,
                    SessionToken = msg.SessionToken,
                    Generation = msg.Generation,
                    Sequence = msg.Sequence,
                    Firing = msg.Firing,
                    X = msg.X,
                    Y = msg.Y,
                    Z = msg.Z,
                };
            };
        }

        private static void RegisterTargets()
        {
            System.Action<NetworkWriter, TargetsC2S> writeC2S = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
                w.WriteUInt32(m.Sequence);
                w.WriteBoolean(m.Replace);
                WriteTargetIds(w, m.TargetIds);
            };
            System.Func<NetworkReader, TargetsC2S> readC2S = r =>
            {
                var msg = new TargetsC2S
                {
                    WireVersion = r.ReadByte(),
                    AircraftNetId = r.ReadUInt32(),
                    Station = r.ReadByte(),
                    SessionToken = r.ReadUInt64(),
                    Generation = r.ReadUInt32(),
                    Sequence = r.ReadUInt32(),
                    Replace = r.ReadBoolean(),
                };
                msg.TargetIds = ReadTargetIds(r);
                return msg;
            };
            Writer<TargetsC2S>.Write = writeC2S;
            Reader<TargetsC2S>.Read = readC2S;

            Writer<TargetsS2C>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteByte(m.Direction);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
                w.WriteUInt32(m.Sequence);
                w.WriteBoolean(m.Replace);
                WriteTargetIds(w, m.TargetIds);
            };
            Reader<TargetsS2C>.Read = r =>
            {
                var msg = new TargetsS2C
                {
                    WireVersion = r.ReadByte(),
                    AircraftNetId = r.ReadUInt32(),
                    Station = r.ReadByte(),
                    Direction = r.ReadByte(),
                    SessionToken = r.ReadUInt64(),
                    Generation = r.ReadUInt32(),
                    Sequence = r.ReadUInt32(),
                    Replace = r.ReadBoolean(),
                };
                msg.TargetIds = ReadTargetIds(r);
                return msg;
            };
        }

        private static void RegisterView()
        {
            System.Action<NetworkWriter, ViewC2S> write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
                w.WriteUInt32(m.Sequence);
                w.WriteSingle(m.PosX);
                w.WriteSingle(m.PosY);
                w.WriteSingle(m.PosZ);
                w.WriteSingle(m.FwdX);
                w.WriteSingle(m.FwdY);
                w.WriteSingle(m.FwdZ);
                w.WriteSingle(m.UpX);
                w.WriteSingle(m.UpY);
                w.WriteSingle(m.UpZ);
                w.WriteSingle(m.Fov);
                w.WriteUInt32(m.PrimaryTargetId);
            };
            System.Func<NetworkReader, ViewC2S> read = r => new ViewC2S
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                Station = r.ReadByte(),
                SessionToken = r.ReadUInt64(),
                Generation = r.ReadUInt32(),
                Sequence = r.ReadUInt32(),
                PosX = r.ReadSingle(),
                PosY = r.ReadSingle(),
                PosZ = r.ReadSingle(),
                FwdX = r.ReadSingle(),
                FwdY = r.ReadSingle(),
                FwdZ = r.ReadSingle(),
                UpX = r.ReadSingle(),
                UpY = r.ReadSingle(),
                UpZ = r.ReadSingle(),
                Fov = r.ReadSingle(),
                PrimaryTargetId = r.ReadUInt32(),
            };
            Writer<ViewC2S>.Write = write;
            Reader<ViewC2S>.Read = read;
            Writer<ViewS2C>.Write = (w, m) => write(w, new ViewC2S
            {
                WireVersion = m.WireVersion,
                AircraftNetId = m.AircraftNetId,
                Station = m.Station,
                SessionToken = m.SessionToken,
                Generation = m.Generation,
                Sequence = m.Sequence,
                PosX = m.PosX,
                PosY = m.PosY,
                PosZ = m.PosZ,
                FwdX = m.FwdX,
                FwdY = m.FwdY,
                FwdZ = m.FwdZ,
                UpX = m.UpX,
                UpY = m.UpY,
                UpZ = m.UpZ,
                Fov = m.Fov,
                PrimaryTargetId = m.PrimaryTargetId,
            });
            Reader<ViewS2C>.Read = r =>
            {
                var msg = read(r);
                return new ViewS2C
                {
                    WireVersion = msg.WireVersion,
                    AircraftNetId = msg.AircraftNetId,
                    Station = msg.Station,
                    SessionToken = msg.SessionToken,
                    Generation = msg.Generation,
                    Sequence = msg.Sequence,
                    PosX = msg.PosX,
                    PosY = msg.PosY,
                    PosZ = msg.PosZ,
                    FwdX = msg.FwdX,
                    FwdY = msg.FwdY,
                    FwdZ = msg.FwdZ,
                    UpX = msg.UpX,
                    UpY = msg.UpY,
                    UpZ = msg.UpZ,
                    Fov = msg.Fov,
                    PrimaryTargetId = msg.PrimaryTargetId,
                };
            };
        }

        private static void RegisterHitFeedback()
        {
            System.Action<NetworkWriter, HitFeedbackC2S> write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
                w.WriteSingle(m.HitX);
                w.WriteSingle(m.HitY);
                w.WriteSingle(m.HitZ);
                w.WriteUInt32(m.HitUnitId);
            };
            System.Func<NetworkReader, HitFeedbackC2S> read = r => new HitFeedbackC2S
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                Station = r.ReadByte(),
                SessionToken = r.ReadUInt64(),
                Generation = r.ReadUInt32(),
                HitX = r.ReadSingle(),
                HitY = r.ReadSingle(),
                HitZ = r.ReadSingle(),
                HitUnitId = r.ReadUInt32(),
            };
            Writer<HitFeedbackC2S>.Write = write;
            Reader<HitFeedbackC2S>.Read = read;
            Writer<HitFeedbackS2C>.Write = (w, m) => write(w, new HitFeedbackC2S
            {
                WireVersion = m.WireVersion,
                AircraftNetId = m.AircraftNetId,
                Station = m.Station,
                SessionToken = m.SessionToken,
                Generation = m.Generation,
                HitX = m.HitX,
                HitY = m.HitY,
                HitZ = m.HitZ,
                HitUnitId = m.HitUnitId,
            });
            Reader<HitFeedbackS2C>.Read = r =>
            {
                var msg = read(r);
                return new HitFeedbackS2C
                {
                    WireVersion = msg.WireVersion,
                    AircraftNetId = msg.AircraftNetId,
                    Station = msg.Station,
                    SessionToken = msg.SessionToken,
                    Generation = msg.Generation,
                    HitX = msg.HitX,
                    HitY = msg.HitY,
                    HitZ = msg.HitZ,
                    HitUnitId = msg.HitUnitId,
                };
            };
        }

        private static void RegisterHeartbeat()
        {
            Writer<HeartbeatC2S>.Write = (w, m) =>
            {
                w.WriteByte(m.WireVersion);
                w.WriteUInt32(m.AircraftNetId);
                w.WriteByte(m.Station);
                w.WriteUInt64(m.SessionToken);
                w.WriteUInt32(m.Generation);
            };
            Reader<HeartbeatC2S>.Read = r => new HeartbeatC2S
            {
                WireVersion = r.ReadByte(),
                AircraftNetId = r.ReadUInt32(),
                Station = r.ReadByte(),
                SessionToken = r.ReadUInt64(),
                Generation = r.ReadUInt32(),
            };
        }

        private static void WriteTargetIds(NetworkWriter w, uint[] targetIds)
        {
            int count = targetIds != null ? Mathf.Min(targetIds.Length, MulticrewProtocol.MaxSharedTargets) : 0;
            w.WriteByte((byte)count);
            for (int i = 0; i < count; i++)
                w.WriteUInt32(targetIds[i]);
        }

        private static uint[] ReadTargetIds(NetworkReader r)
        {
            int count = Mathf.Min(r.ReadByte(), MulticrewProtocol.MaxSharedTargets);
            var ids = new uint[count];
            for (int i = 0; i < count; i++)
                ids[i] = r.ReadUInt32();
            return ids;
        }
    }
}
