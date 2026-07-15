using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace MulticrewNuclearOption.Core
{
    internal readonly struct StationKey : IEquatable<StationKey>
    {
        public readonly uint AircraftNetId;
        public readonly byte Station;

        public StationKey(uint aircraftNetId, byte station)
        {
            AircraftNetId = aircraftNetId;
            Station = station;
        }

        public long Packed => ((long)AircraftNetId << 8) | Station;

        public static StationKey FromPacked(long key)
            => new StationKey((uint)(key >> 8), (byte)(key & 0xFF));

        public bool Equals(StationKey other)
            => AircraftNetId == other.AircraftNetId && Station == other.Station;

        public override bool Equals(object obj) => obj is StationKey other && Equals(other);

        public override int GetHashCode() => (int)(AircraftNetId * 397) ^ Station;
    }

    internal sealed class MulticrewServerSessions
    {
        private sealed class PresenceEntry
        {
            public uint AircraftNetId;
            public uint PilotPlayerNetId;
            public float LastSeen;
        }

        internal sealed class StationLease
        {
            public StationKey Key;
            public ulong SessionToken;
            public uint Generation;
            public INetworkPlayer GunnerPeer;
            public Player GunnerPlayer;
            public uint GunnerPlayerNetId;
            public INetworkPlayer OwnerPeer;
            public Player PilotPlayer;
            public float LastHeartbeat;
            public float LastControl;
            public uint LastControlSequence;
            public uint LastTargetSequence;
            public uint LastViewSequence;
            public bool Firing;
        }

        private NetworkServer _server;
        private readonly HashSet<INetworkPlayer> _modPeers = new HashSet<INetworkPlayer>();
        private readonly Dictionary<StationKey, StationLease> _leases = new Dictionary<StationKey, StationLease>();
        private readonly Dictionary<INetworkPlayer, StationLease> _gunnerLeases = new Dictionary<INetworkPlayer, StationLease>();
        private readonly Dictionary<uint, PresenceEntry> _presenceByAircraft = new Dictionary<uint, PresenceEntry>();
        private readonly Dictionary<INetworkPlayer, uint> _presenceByPeer = new Dictionary<INetworkPlayer, uint>();
        private uint _nextGeneration = 1;

        public void Bind(NetworkServer server) => _server = server;

        public void Reset()
        {
            _modPeers.Clear();
            _leases.Clear();
            _gunnerLeases.Clear();
            _presenceByAircraft.Clear();
            _presenceByPeer.Clear();
            _nextGeneration = 1;
            _server = null;
        }

        public void OnDisconnected(INetworkPlayer player)
        {
            if (player == null) return;

            _modPeers.Remove(player);
            RemovePresenceForPeer(player);

            if (_gunnerLeases.TryGetValue(player, out var lease))
                RevokeLease(lease, LeaseRevokeReason.Disconnect);
        }

        public void Tick(float now)
        {
            var expiredLeases = new List<StationLease>();
            foreach (var lease in _leases.Values)
            {
                if (now - lease.LastHeartbeat > MulticrewProtocol.LeaseTtlSeconds)
                    expiredLeases.Add(lease);
                else if (lease.Firing && now - lease.LastControl > MulticrewProtocol.FireWatchdogSeconds)
                    lease.Firing = false;
            }

            foreach (var lease in expiredLeases)
                RevokeLease(lease, LeaseRevokeReason.Timeout);

            var expiredPresence = new List<uint>();
            foreach (var pair in _presenceByAircraft)
            {
                if (now - pair.Value.LastSeen > MulticrewProtocol.PresenceTtlSeconds)
                    expiredPresence.Add(pair.Key);
            }

            foreach (uint aircraftNetId in expiredPresence)
                RemovePresence(aircraftNetId);
        }

        public void HandleHello(INetworkPlayer sender, HelloC2S msg)
        {
            if (!TryGetPlayer(sender, out _))
                return;
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;

            _modPeers.Add(sender);
            SendToPeer(sender, new HelloS2C { WireVersion = MulticrewProtocol.WireVersion }, Channel.Reliable);
        }

        public void HandlePresence(INetworkPlayer sender, PresenceC2S msg)
        {
            if (!IsKnownModPeer(sender) || msg.WireVersion != MulticrewProtocol.WireVersion)
                return;
            if (!TryGetPlayer(sender, out Player pilot) || pilot == null)
                return;

            Aircraft aircraft = pilot.Aircraft;
            if (aircraft == null || aircraft.disabled)
            {
                RemovePresenceForPeer(sender);
                return;
            }

            if (!ReferenceEquals(aircraft.Identity != null ? aircraft.Identity.Owner : null, sender))
                return;

            float now = Time.unscaledTime;
            uint aircraftNetId = aircraft.NetId;
            uint pilotNetId = pilot.NetId;

            if (_presenceByPeer.TryGetValue(sender, out uint previousAircraft) && previousAircraft != aircraftNetId)
                RemovePresence(previousAircraft);

            _presenceByPeer[sender] = aircraftNetId;
            _presenceByAircraft[aircraftNetId] = new PresenceEntry
            {
                AircraftNetId = aircraftNetId,
                PilotPlayerNetId = pilotNetId,
                LastSeen = now,
            };

            BroadcastToModPeers(new PresenceS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = aircraftNetId,
                PilotPlayerNetId = pilotNetId,
            }, Channel.Reliable);
        }

        public void HandleJoin(INetworkPlayer sender, JoinReqC2S msg)
        {
            if (!IsKnownModPeer(sender) || msg.WireVersion != MulticrewProtocol.WireVersion)
                return;
            if (!TryGetPlayer(sender, out Player gunnerPlayer) || gunnerPlayer == null)
            {
                RejectJoin(sender, msg.RequestId, JoinRejectReason.NotAuthenticated);
                return;
            }

            if (msg.AircraftNetId == 0u || msg.Station > MulticrewProtocol.MaxStationIndex)
            {
                RejectJoin(sender, msg.RequestId, JoinRejectReason.InvalidTarget);
                return;
            }

            if (!TryResolveAircraft(msg.AircraftNetId, out Aircraft aircraft, out NetworkIdentity identity))
            {
                RejectJoin(sender, msg.RequestId, JoinRejectReason.InvalidTarget);
                return;
            }

            if (!IsFriendly(gunnerPlayer, aircraft))
            {
                RejectJoin(sender, msg.RequestId, JoinRejectReason.NotFriendly);
                return;
            }

            if (!HasFreshPresence(aircraft.NetId))
            {
                RejectJoin(sender, msg.RequestId, JoinRejectReason.NoPilotPresence);
                return;
            }

            var key = new StationKey(msg.AircraftNetId, msg.Station);
            if (_leases.TryGetValue(key, out var existing) &&
                !ReferenceEquals(existing.GunnerPeer, sender))
            {
                RejectJoin(sender, msg.RequestId, JoinRejectReason.StationBusy);
                return;
            }

            if (_gunnerLeases.TryGetValue(sender, out var activeLease) &&
                !activeLease.Key.Equals(key))
            {
                RejectJoin(sender, msg.RequestId, JoinRejectReason.AlreadyLeased);
                return;
            }

            INetworkPlayer ownerPeer = identity.Owner;
            if (ownerPeer == null)
            {
                RejectJoin(sender, msg.RequestId, JoinRejectReason.InvalidTarget);
                return;
            }

            float now = Time.unscaledTime;
            var lease = existing ?? new StationLease();
            lease.Key = key;
            lease.SessionToken = NewToken();
            lease.Generation = _nextGeneration++;
            lease.GunnerPeer = sender;
            lease.GunnerPlayer = gunnerPlayer;
            lease.GunnerPlayerNetId = gunnerPlayer.NetId;
            lease.OwnerPeer = ownerPeer;
            lease.PilotPlayer = aircraft.Player;
            lease.LastHeartbeat = now;
            lease.LastControl = now;
            lease.LastControlSequence = 0;
            lease.LastTargetSequence = 0;
            lease.LastViewSequence = 0;
            lease.Firing = false;

            _leases[key] = lease;
            _gunnerLeases[sender] = lease;

            var granted = new JoinGrantedS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = key.AircraftNetId,
                Station = key.Station,
                RequestId = msg.RequestId,
                SessionToken = lease.SessionToken,
                Generation = lease.Generation,
                GunnerPlayerNetId = lease.GunnerPlayerNetId,
            };
            SendToPeer(sender, granted, Channel.Reliable);

            SendToPeer(ownerPeer, new LeaseActivatedS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = key.AircraftNetId,
                Station = key.Station,
                SessionToken = lease.SessionToken,
                Generation = lease.Generation,
                GunnerPlayerNetId = lease.GunnerPlayerNetId,
            }, Channel.Reliable);
        }

        public void HandleLeave(INetworkPlayer sender, LeaveC2S msg)
        {
            if (!TryGetLeaseForSender(sender, msg.AircraftNetId, msg.Station, msg.SessionToken, msg.Generation, out var lease))
                return;

            RevokeLease(lease, LeaseRevokeReason.ManualLeave);
        }

        public void HandleHeartbeat(INetworkPlayer sender, HeartbeatC2S msg)
        {
            if (!TryGetLeaseForSender(sender, msg.AircraftNetId, msg.Station, msg.SessionToken, msg.Generation, out var lease))
                return;

            lease.LastHeartbeat = Time.unscaledTime;
        }

        public void HandleControl(INetworkPlayer sender, ControlC2S msg)
        {
            if (!TryGetLeaseForSender(sender, msg.AircraftNetId, msg.Station, msg.SessionToken, msg.Generation, out var lease))
                return;
            if (msg.Sequence <= lease.LastControlSequence)
                return;
            if (!MulticrewValidation.TryNormalizeAim(msg.X, msg.Y, msg.Z, out Vector3 aim))
                return;

            float now = Time.unscaledTime;
            lease.LastControlSequence = msg.Sequence;
            lease.LastControl = now;
            lease.LastHeartbeat = now;
            lease.Firing = msg.Firing;

            SendToPeer(lease.OwnerPeer, new ControlS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = lease.Key.AircraftNetId,
                Station = lease.Key.Station,
                SessionToken = lease.SessionToken,
                Generation = lease.Generation,
                Sequence = msg.Sequence,
                Firing = msg.Firing,
                X = aim.x,
                Y = aim.y,
                Z = aim.z,
            }, Channel.Unreliable);
        }

        public void HandleTargets(INetworkPlayer sender, TargetsC2S msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;
            if (!MulticrewValidation.TrySanitizeTargetIds(msg.TargetIds, out uint[] targetIds))
                return;

            byte direction;
            StationLease lease = null;
            INetworkPlayer recipient;

            if (TryGetLeaseForSender(sender, msg.AircraftNetId, msg.Station, msg.SessionToken, msg.Generation, out lease))
            {
                direction = TargetShareDirection.GunnerToPilot;
                recipient = lease.OwnerPeer;
                if (msg.Sequence <= lease.LastTargetSequence)
                    return;
                lease.LastTargetSequence = msg.Sequence;
                lease.LastHeartbeat = Time.unscaledTime;
            }
            else if (TryResolveAircraft(msg.AircraftNetId, out _, out NetworkIdentity identity) &&
                     ReferenceEquals(identity.Owner, sender))
            {
                bool sent = false;
                foreach (var activeLease in _leases.Values)
                {
                    if (activeLease.Key.AircraftNetId != msg.AircraftNetId)
                        continue;

                    SendToPeer(activeLease.GunnerPeer, new TargetsS2C
                    {
                        WireVersion = MulticrewProtocol.WireVersion,
                        AircraftNetId = msg.AircraftNetId,
                        Station = activeLease.Key.Station,
                        Direction = TargetShareDirection.PilotToGunner,
                        SessionToken = activeLease.SessionToken,
                        Generation = activeLease.Generation,
                        Sequence = msg.Sequence,
                        Replace = msg.Replace,
                        TargetIds = targetIds,
                    }, Channel.Reliable);
                    sent = true;
                }

                if (!sent)
                    return;
                return;
            }
            else
            {
                return;
            }

            SendToPeer(recipient, new TargetsS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = msg.AircraftNetId,
                Station = msg.Station,
                Direction = direction,
                SessionToken = lease != null ? lease.SessionToken : 0UL,
                Generation = lease != null ? lease.Generation : 0u,
                Sequence = msg.Sequence,
                Replace = msg.Replace,
                TargetIds = targetIds,
            }, Channel.Reliable);
        }

        public void HandleView(INetworkPlayer sender, ViewC2S msg)
        {
            if (!TryGetLeaseForSender(sender, msg.AircraftNetId, msg.Station, msg.SessionToken, msg.Generation, out var lease))
                return;
            if (msg.Sequence <= lease.LastViewSequence)
                return;
            if (!MulticrewValidation.TryValidateView(msg, out ViewC2S sanitized))
                return;

            lease.LastViewSequence = msg.Sequence;
            lease.LastHeartbeat = Time.unscaledTime;

            SendToPeer(lease.OwnerPeer, new ViewS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = sanitized.AircraftNetId,
                Station = sanitized.Station,
                SessionToken = lease.SessionToken,
                Generation = lease.Generation,
                Sequence = sanitized.Sequence,
                PosX = sanitized.PosX,
                PosY = sanitized.PosY,
                PosZ = sanitized.PosZ,
                FwdX = sanitized.FwdX,
                FwdY = sanitized.FwdY,
                FwdZ = sanitized.FwdZ,
                UpX = sanitized.UpX,
                UpY = sanitized.UpY,
                UpZ = sanitized.UpZ,
                Fov = sanitized.Fov,
                PrimaryTargetId = sanitized.PrimaryTargetId,
            }, Channel.Unreliable);
        }

        public void HandleHitFeedback(INetworkPlayer sender, HitFeedbackC2S msg)
        {
            if (msg.WireVersion != MulticrewProtocol.WireVersion)
                return;
            if (!TryResolveAircraft(msg.AircraftNetId, out _, out NetworkIdentity identity))
                return;
            if (!ReferenceEquals(identity.Owner, sender))
                return;
            if (!TryGetActiveLease(msg.AircraftNetId, msg.Station, out var lease))
                return;
            if (!MulticrewValidation.IsFinite(msg.HitX) || !MulticrewValidation.IsFinite(msg.HitY) || !MulticrewValidation.IsFinite(msg.HitZ))
                return;

            SendToPeer(lease.GunnerPeer, new HitFeedbackS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = msg.AircraftNetId,
                Station = msg.Station,
                SessionToken = lease.SessionToken,
                Generation = lease.Generation,
                HitX = msg.HitX,
                HitY = msg.HitY,
                HitZ = msg.HitZ,
                HitUnitId = msg.HitUnitId,
            }, Channel.Unreliable);
        }

        public bool HasFreshPresence(uint aircraftNetId)
        {
            if (!_presenceByAircraft.TryGetValue(aircraftNetId, out var entry))
                return false;
            return Time.unscaledTime - entry.LastSeen <= MulticrewProtocol.PresenceTtlSeconds;
        }

        public bool TryGetGunnerNetId(uint aircraftNetId, byte station, out uint gunnerNetId)
        {
            var key = new StationKey(aircraftNetId, station);
            if (_leases.TryGetValue(key, out var lease))
            {
                gunnerNetId = lease.GunnerPlayerNetId;
                return gunnerNetId != 0u;
            }

            gunnerNetId = 0u;
            return false;
        }

        public void OnAircraftDisabled(uint aircraftNetId)
        {
            RemovePresence(aircraftNetId);

            var stale = new List<StationLease>();
            foreach (var lease in _leases.Values)
            {
                if (lease.Key.AircraftNetId == aircraftNetId)
                    stale.Add(lease);
            }

            foreach (var lease in stale)
                RevokeLease(lease, LeaseRevokeReason.AircraftDisabled);
        }

        private INetworkPlayer FindActiveGunnerPeer(uint aircraftNetId, byte station)
        {
            var key = new StationKey(aircraftNetId, station);
            return _leases.TryGetValue(key, out var lease) ? lease.GunnerPeer : null;
        }

        private bool TryGetLeaseForSender(
            INetworkPlayer sender,
            uint aircraftNetId,
            byte station,
            ulong sessionToken,
            uint generation,
            out StationLease lease)
        {
            lease = null;
            if (!TryGetLease(aircraftNetId, station, sessionToken, generation, out lease))
                return false;
            return ReferenceEquals(lease.GunnerPeer, sender);
        }

        private bool TryGetActiveLease(uint aircraftNetId, byte station, out StationLease lease)
        {
            lease = null;
            var key = new StationKey(aircraftNetId, station);
            return _leases.TryGetValue(key, out lease);
        }

        private bool TryGetLease(uint aircraftNetId, byte station, ulong sessionToken, uint generation, out StationLease lease)
        {
            lease = null;
            var key = new StationKey(aircraftNetId, station);
            if (!_leases.TryGetValue(key, out lease))
                return false;
            if (lease.SessionToken != sessionToken || lease.Generation != generation)
            {
                lease = null;
                return false;
            }

            return true;
        }

        private void RevokeLease(StationLease lease, byte reason)
        {
            if (lease == null) return;

            _leases.Remove(lease.Key);
            if (lease.GunnerPeer != null)
                _gunnerLeases.Remove(lease.GunnerPeer);

            var revoked = new LeaseRevokedS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = lease.Key.AircraftNetId,
                Station = lease.Key.Station,
                SessionToken = lease.SessionToken,
                Generation = lease.Generation,
                Reason = reason,
            };

            if (lease.GunnerPeer != null)
                SendToPeer(lease.GunnerPeer, revoked, Channel.Reliable);
            if (lease.OwnerPeer != null && !ReferenceEquals(lease.OwnerPeer, lease.GunnerPeer))
                SendToPeer(lease.OwnerPeer, revoked, Channel.Reliable);
        }

        private void RejectJoin(INetworkPlayer sender, uint requestId, byte reason)
        {
            SendToPeer(sender, new JoinRejectedS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                RequestId = requestId,
                Reason = reason,
            }, Channel.Reliable);
        }

        private void RemovePresenceForPeer(INetworkPlayer peer)
        {
            if (peer == null) return;
            if (_presenceByPeer.TryGetValue(peer, out uint aircraftNetId))
            {
                _presenceByPeer.Remove(peer);
                RemovePresence(aircraftNetId);
            }
        }

        private void RemovePresence(uint aircraftNetId)
        {
            if (!_presenceByAircraft.Remove(aircraftNetId))
                return;

            BroadcastToModPeers(new PresenceRemovedS2C
            {
                WireVersion = MulticrewProtocol.WireVersion,
                AircraftNetId = aircraftNetId,
            }, Channel.Reliable);
        }

        private bool IsKnownModPeer(INetworkPlayer sender)
            => sender != null && sender.IsAuthenticated && _modPeers.Contains(sender);

        private bool IsFriendly(Player gunner, Aircraft aircraft)
        {
            if (gunner == null || aircraft == null)
                return false;
            if (gunner.HQ == null)
                return true;
            return aircraft.NetworkHQ == gunner.HQ;
        }

        private bool TryResolveAircraft(uint netId, out Aircraft aircraft, out NetworkIdentity identity)
        {
            aircraft = null;
            identity = null;
            if (_server == null || _server.World == null)
                return false;
            if (!_server.World.TryGetIdentity(netId, out identity) || identity == null)
                return false;

            aircraft = identity.GetComponent<Aircraft>();
            return aircraft != null && !aircraft.disabled;
        }

        private static bool TryGetPlayer(INetworkPlayer peer, out Player player)
            => PlayerHelper.TryGetPlayer(peer, out player);

        private void SendToPeer<T>(INetworkPlayer peer, T message, Channel channel)
        {
            if (peer == null || !peer.IsConnected)
                return;

            try
            {
                peer.Send(message, channel);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Net] SendToPeer<{typeof(T).Name}> failed: {e.Message}");
            }
        }

        private void BroadcastToModPeers<T>(T message, Channel channel)
        {
            foreach (var peer in _modPeers)
                SendToPeer(peer, message, channel);
        }

        private static ulong NewToken()
        {
            var bytes = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}
