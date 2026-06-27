using HarmonyLib;
using SimpleWSO.Core;

namespace SimpleWSO.Patches
{
    /// <summary>
    /// Ensure looping missile-alarm audio stops when a threat ends even if ThreatList's
    /// itemLookup was cleared during gunner HUD handoff.
    /// </summary>
    [HarmonyPatch(typeof(ThreatList), "ThreatList_OffMissileWarning")]
    public static class ThreatListOffMissileWarningPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ThreatList __instance, MissileWarning.OffMissileWarning e)
            => ThreatListHelper.TryRemoveMissileAlarm(__instance, e.missile);
    }
}
