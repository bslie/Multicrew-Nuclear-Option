using HarmonyLib;
using MulticrewNuclearOption.Core;
using MulticrewNuclearOption.Gunner;
using NuclearOption.Networking;
using UnityEngine;

namespace MulticrewNuclearOption.Patches
{
    [HarmonyPatch(typeof(MessageManager), "UserCode_RpcKillMessage_635947223")]
    internal static class KillFeedPreparePatch
    {
        static void Prefix(PersistentID killerID, PersistentID killedID)
        {
            KillFeedGunnerPatch.Prepare(killerID, killedID);
        }
    }

    internal static class KillFeedGunnerPatch
    {
        private static PersistentID _killerId;
        private static PersistentID _killedId;

        public static void Prepare(PersistentID killerId, PersistentID killedId)
        {
            _killerId = killerId;
            _killedId = killedId;
        }

        [HarmonyPatch(typeof(GameplayUI), nameof(GameplayUI.KillFeed))]
        [HarmonyPrefix]
        public static void Prefix(ref string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            string pilotName = ResolvePilotName(_killerId);
            GunnerKillCredit.TryGetKillFeedOverride(_killerId, _killedId, pilotName, ref message);
        }

        private static string ResolvePilotName(PersistentID killerId)
        {
            if (!killerId.IsValid)
                return null;

            if (!UnitRegistry.TryGetPersistentUnit(killerId, out PersistentUnit killerUnit) || killerUnit == null)
                return null;

            if (Reflect.TryGetField<Player>(killerUnit, "player", out var killerPlayer) && killerPlayer != null)
                return killerPlayer.GetNameOrCensored();

            if (killerUnit.unit is Aircraft aircraft && aircraft.Player != null)
                return aircraft.Player.GetNameOrCensored();

            return killerUnit.unitName;
        }
    }
}
