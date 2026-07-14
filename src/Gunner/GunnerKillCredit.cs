using System.Collections.Generic;
using MulticrewNuclearOption.Core;
using NuclearOption.Networking;
using UnityEngine;

namespace MulticrewNuclearOption.Gunner
{
    /// <summary>
    /// Tracks gunner weapon-station attribution for kill rewards and kill-feed labels.
    /// </summary>
    public static class GunnerKillCredit
    {
        private const float SplitFactor = 0.5f;

        private sealed class Attribution
        {
            public uint AircraftNetId;
            public byte Station;
            public uint GunnerPlayerNetId;
            public string GunnerName;
        }

        private static readonly Dictionary<uint, Attribution> _unitAttribution = new Dictionary<uint, Attribution>();
        private static readonly Dictionary<uint, Attribution> _pendingKillFeed = new Dictionary<uint, Attribution>();
        private static bool _splitRewardsActive;
        private static Player _splitGunnerPlayer;
        private static float _queuedGunnerScore;
        private static float _queuedGunnerAllocation;
        private static int _splitBypassDepth;

        public static void RegisterGunnerStation(uint aircraftNetId, byte station, uint gunnerPlayerNetId)
        {
            long key = ((long)aircraftNetId << 8) | station;
            MulticrewNet.RegisterGunnerPlayer(key, gunnerPlayerNetId);
        }

        public static void UnregisterGunnerStation(uint aircraftNetId, byte station)
        {
            long key = ((long)aircraftNetId << 8) | station;
            MulticrewNet.UnregisterGunnerPlayer(key);
        }

        public static void RecordHit(uint aircraftNetId, byte station, Unit hitUnit)
        {
            if (hitUnit == null || !MulticrewNet.IsRemoteGunnerStation(aircraftNetId, station))
                return;

            if (!MulticrewNet.TryGetGunnerPlayerNetId(aircraftNetId, station, out uint gunnerNetId))
                return;

            _unitAttribution[hitUnit.persistentID.Id] = new Attribution
            {
                AircraftNetId = aircraftNetId,
                Station = station,
                GunnerPlayerNetId = gunnerNetId,
                GunnerName = ResolvePlayerName(gunnerNetId),
            };
        }

        public static void ClearUnit(uint persistentId)
        {
            _unitAttribution.Remove(persistentId);
            _pendingKillFeed.Remove(persistentId);
        }

        public static void Reset()
        {
            _unitAttribution.Clear();
            _pendingKillFeed.Clear();
            _splitRewardsActive = false;
            _splitGunnerPlayer = null;
            _queuedGunnerScore = 0f;
            _queuedGunnerAllocation = 0f;
            _splitBypassDepth = 0;
        }

        public static bool TryBeginSplitRewards(Unit killedUnit)
        {
            _splitGunnerPlayer = null;
            _queuedGunnerScore = 0f;
            _queuedGunnerAllocation = 0f;
            _splitRewardsActive = false;

            if (killedUnit == null)
                return false;

            if (!_unitAttribution.TryGetValue(killedUnit.persistentID.Id, out var attribution))
                return false;

            Aircraft aircraft = ResolveAircraft(attribution.AircraftNetId);
            Player pilotPlayer = aircraft != null ? aircraft.Player : null;
            Player gunnerPlayer = ResolvePlayer(attribution.GunnerPlayerNetId);
            if (pilotPlayer == null || gunnerPlayer == null || gunnerPlayer == pilotPlayer)
                return false;

            _splitGunnerPlayer = gunnerPlayer;
            _splitRewardsActive = true;
            _pendingKillFeed[killedUnit.persistentID.Id] = attribution;
            return true;
        }

        public static bool ShouldSplitScore(Player player, ref float score)
        {
            if (_splitBypassDepth > 0 || !_splitRewardsActive || _splitGunnerPlayer == null || player == null)
                return false;

            if (player == _splitGunnerPlayer)
                return false;

            float half = score * SplitFactor;
            score = half;
            _queuedGunnerScore += half;
            return true;
        }

        public static bool ShouldSplitAllocation(Player player, ref float allocation)
        {
            if (_splitBypassDepth > 0 || !_splitRewardsActive || _splitGunnerPlayer == null || player == null)
                return false;

            if (player == _splitGunnerPlayer)
                return false;

            float half = allocation * SplitFactor;
            allocation = half;
            _queuedGunnerAllocation += half;
            return true;
        }

        public static void EnterSplitBypass() => _splitBypassDepth++;

        public static void ExitSplitBypass()
        {
            if (_splitBypassDepth > 0)
                _splitBypassDepth--;
        }

        public static void FlushQueuedGunnerRewards()
        {
            if (_splitGunnerPlayer == null)
            {
                _splitRewardsActive = false;
                return;
            }

            if (_queuedGunnerScore > 0f)
                _splitGunnerPlayer.AddScore(_queuedGunnerScore);
            if (_queuedGunnerAllocation > 0f)
                _splitGunnerPlayer.AddAllocation(_queuedGunnerAllocation);

            _queuedGunnerScore = 0f;
            _queuedGunnerAllocation = 0f;
            _splitGunnerPlayer = null;
            _splitRewardsActive = false;
        }

        public static bool TryGetKillFeedOverride(PersistentID killerId, PersistentID killedId, string pilotName, ref string message)
        {
            if (!killerId.IsValid || !killedId.IsValid)
                return false;

            if (!_pendingKillFeed.TryGetValue(killedId.Id, out var attribution))
                return false;

            if (string.IsNullOrEmpty(attribution.GunnerName))
                return false;

            string gunnerName = attribution.GunnerName;
            _pendingKillFeed.Remove(killedId.Id);

            if (string.IsNullOrEmpty(pilotName))
                pilotName = "Pilot";

            int verbIndex = message.IndexOf(' ');
            if (verbIndex < 0)
            {
                message = $"{pilotName} + {gunnerName} {message}";
                return true;
            }

            string verbAndRest = message.Substring(verbIndex).TrimStart();
            message = $"{pilotName} + {gunnerName} {verbAndRest}";
            return true;
        }

        public static bool ShouldSplitReportKill(Unit target, ref float factor)
        {
            if (!_splitRewardsActive || _splitGunnerPlayer == null || target == null)
                return false;

            factor *= SplitFactor;
            return true;
        }

        public static bool TryGetGunnerForSecondReward(Unit target, out Player gunnerPlayer)
        {
            gunnerPlayer = _splitGunnerPlayer;
            return gunnerPlayer != null && target != null && _unitAttribution.ContainsKey(target.persistentID.Id);
        }

        private static Player ResolvePlayer(uint playerNetId)
        {
            if (playerNetId == 0u)
                return null;

            foreach (var player in Object.FindObjectsOfType<Player>())
            {
                if (player != null && player.NetId == playerNetId)
                    return player;
            }

            return null;
        }

        private static string ResolvePlayerName(uint playerNetId)
        {
            Player player = ResolvePlayer(playerNetId);
            if (player == null)
                return null;

            return player.GetNameOrCensored();
        }

        private static Aircraft ResolveAircraft(uint aircraftNetId)
        {
            if (aircraftNetId == 0u)
                return null;

            foreach (var aircraft in Object.FindObjectsOfType<Aircraft>())
            {
                if (aircraft != null && aircraft.NetId == aircraftNetId)
                    return aircraft;
            }

            return null;
        }
    }
}
