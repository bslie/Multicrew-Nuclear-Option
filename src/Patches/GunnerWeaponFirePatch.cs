using HarmonyLib;
using MulticrewNuclearOption.Gunner;

namespace MulticrewNuclearOption.Patches
{
    /// <summary>
    /// While a gunner station is under mod control, block vanilla WeaponManager / turret
    /// auto-fire from also calling WeaponStation.Fire on that station.
    /// </summary>
    [HarmonyPatch(typeof(WeaponStation), "Fire", typeof(Unit), typeof(Unit))]
    public static class GunnerWeaponFirePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponStation __instance, Unit owner)
            => GunnerWeaponFire.AllowVanillaFire(__instance, owner as Aircraft);
    }
}
