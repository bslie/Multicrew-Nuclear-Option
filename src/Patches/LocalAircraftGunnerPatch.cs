using HarmonyLib;
using MulticrewNuclearOption.Core;

namespace MulticrewNuclearOption.Patches
{
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsLocalAircraft), new[] { typeof(Aircraft) })]
    internal static class LocalAircraftGunnerPatch
    {
        static void Postfix(Aircraft aircraftToCheck, ref bool __result)
        {
            if (__result)
                return;

            if (GunnerState.Active &&
                GunnerState.TargetAircraft != null &&
                aircraftToCheck != null &&
                aircraftToCheck == GunnerState.TargetAircraft)
            {
                __result = true;
            }
        }
    }
}
