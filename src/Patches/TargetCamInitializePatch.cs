using HarmonyLib;
using MulticrewNuclearOption.Core;
using MulticrewNuclearOption.Gunner;
using Mirage;

namespace MulticrewNuclearOption.Patches
{
    [HarmonyPatch(typeof(TargetCam), "Initialize")]
    internal static class TargetCamInitializePatch
    {
        static void Prefix()
        {
            if (GunnerState.Active && GunnerState.TargetAircraft != null)
                GunnerCockpitDisplay.BypassTargetCamAuthority = true;
        }

        static void Postfix()
        {
            GunnerCockpitDisplay.BypassTargetCamAuthority = false;
        }
    }

    [HarmonyPatch(typeof(NetworkIdentity), "get_HasAuthority")]
    internal static class TargetCamAuthorityBypassPatch
    {
        static void Postfix(ref bool __result)
        {
            if (GunnerCockpitDisplay.BypassTargetCamAuthority)
                __result = true;
        }
    }
}
