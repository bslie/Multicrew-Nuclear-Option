using HarmonyLib;
using MulticrewNuclearOption.Core;
using MulticrewNuclearOption.Gunner;
using UnityEngine;

namespace MulticrewNuclearOption.Patches
{
    [HarmonyPatch(typeof(Unit), "UserCode_CmdClaimHit_-1122942669")]
    internal static class GunnerHitClaimPatch
    {
        static void Postfix(Unit __instance, PersistentID hitID, byte weaponStationIndex)
        {
            Aircraft aircraft = __instance as Aircraft;
            if (aircraft == null)
                return;

            Unit hitUnit = null;
            UnitRegistry.TryGetUnit(hitID, out hitUnit);
            if (hitUnit == null)
                return;

            GunnerKillCredit.RecordHit(aircraft.NetId, weaponStationIndex, hitUnit);
        }
    }

    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DisplayHit))]
    internal static class GunnerOwnerHitFeedbackPatch
    {
        static void Postfix(GlobalPosition hitPosition, Unit hitUnit)
        {
            var hud = CombatHUD.i;
            if (hud == null || hud.aircraft == null || !hud.aircraft.LocalSim)
                return;

            if (!MulticrewNet.TryGetFiringGunnerStation(hud.aircraft.NetId, out byte station))
                return;

            if (!MulticrewNet.IsRemoteGunnerStation(hud.aircraft.NetId, station))
                return;

            MulticrewNet.SendHitFeedbackFromOwner(hud.aircraft.NetId, station, hitPosition, hitUnit);
        }
    }
}
