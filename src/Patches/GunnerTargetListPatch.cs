using HarmonyLib;
using MulticrewNuclearOption.Core;
using System.Collections.Generic;

namespace MulticrewNuclearOption.Patches
{
    /// <summary>
    /// Vanilla CombatHUD selection uses CombatHUD.targetList, and in some paths mutates that
    /// list directly before calling WeaponManager.TargetListChanged(). While in gunner mode,
    /// make that HUD list the gunner-owned list and prevent the target sync from touching the
    /// pilot/current weapon station.
    /// </summary>
    public static class GunnerTargetListPatch
    {
        private static int _hudSelectionDepth;
        private static int _hudWeaponBindDepth;

        private static bool IsGunnerHud(CombatHUD hud)
            => GunnerState.Active &&
               GunnerState.TargetAircraft != null &&
               hud != null &&
               hud.aircraft == GunnerState.TargetAircraft;

        public static bool ShouldAllowWeaponStationHud(CombatHUD hud, WeaponStation weaponStation)
        {
            if (!IsGunnerHud(hud))
                return true;

            if (weaponStation == null)
                return false;

            foreach (var station in GunnerState.Stations)
            {
                if (station != null && station.Station == weaponStation)
                    return true;
            }

            return false;
        }

        public static void BeginHudSelection(CombatHUD hud)
        {
            if (!IsGunnerHud(hud)) return;
            _hudSelectionDepth++;
            Bind(hud);
        }

        public static void BeginHudWeaponBind(CombatHUD hud)
        {
            if (!IsGunnerHud(hud)) return;
            _hudWeaponBindDepth++;
            Bind(hud);
        }

        public static void EndHudSelection(CombatHUD hud)
        {
            if (!IsGunnerHud(hud)) return;
            Bind(hud);
            if (_hudSelectionDepth > 0)
                _hudSelectionDepth--;
        }

        public static void EndHudWeaponBind(CombatHUD hud)
        {
            if (!IsGunnerHud(hud)) return;
            Bind(hud);
            if (_hudWeaponBindDepth > 0)
                _hudWeaponBindDepth--;
        }

        public static void Bind(CombatHUD hud)
        {
            if (IsGunnerHud(hud))
                Reflect.SetField(hud, "targetList", GunnerState.TargetList);
        }

        public static bool IsGunnerWeaponManager(WeaponManager wm)
        {
            if (!GunnerState.Active || GunnerState.TargetAircraft == null || wm == null)
                return false;
            return Reflect.TryGetField<Aircraft>(wm, "aircraft", out var ac) &&
                   ac == GunnerState.TargetAircraft;
        }

        public static bool IsGunnerHudSelection(WeaponManager wm)
            => _hudSelectionDepth > 0 && IsGunnerWeaponManager(wm);

        public static bool IsGunnerHudWeaponBind(WeaponManager wm)
            => _hudWeaponBindDepth > 0 && IsGunnerWeaponManager(wm);
    }

    [HarmonyPatch(typeof(CombatHUD), "ShowWeaponStation")]
    public static class CombatHUDShowWeaponStationPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CombatHUD __instance, WeaponStation weaponStation, ref bool __state)
        {
            __state = false;
            if (!GunnerTargetListPatch.ShouldAllowWeaponStationHud(__instance, weaponStation))
                return false;

            GunnerTargetListPatch.BeginHudWeaponBind(__instance);
            __state = true;
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(CombatHUD __instance, bool __state)
        {
            if (__state)
                GunnerTargetListPatch.EndHudWeaponBind(__instance);
        }
    }

    [HarmonyPatch(typeof(CombatHUD), "TargetSelect")]
    public static class CombatHUDTargetSelectPatch
    {
        [HarmonyPrefix]
        public static void Prefix(CombatHUD __instance) => GunnerTargetListPatch.BeginHudSelection(__instance);

        [HarmonyPostfix]
        public static void Postfix(CombatHUD __instance) => GunnerTargetListPatch.EndHudSelection(__instance);
    }

    [HarmonyPatch(typeof(CombatHUD), "SelectUnit")]
    public static class CombatHUDSelectUnitPatch
    {
        [HarmonyPrefix]
        public static void Prefix(CombatHUD __instance) => GunnerTargetListPatch.BeginHudSelection(__instance);

        [HarmonyPostfix]
        public static void Postfix(CombatHUD __instance) => GunnerTargetListPatch.EndHudSelection(__instance);
    }

    [HarmonyPatch(typeof(CombatHUD), "DeSelectUnit")]
    public static class CombatHUDDeSelectUnitPatch
    {
        [HarmonyPrefix]
        public static void Prefix(CombatHUD __instance) => GunnerTargetListPatch.BeginHudSelection(__instance);

        [HarmonyPostfix]
        public static void Postfix(CombatHUD __instance) => GunnerTargetListPatch.EndHudSelection(__instance);
    }

    [HarmonyPatch(typeof(CombatHUD), "DeselectAll")]
    public static class CombatHUDDeselectAllPatch
    {
        [HarmonyPrefix]
        public static void Prefix(CombatHUD __instance) => GunnerTargetListPatch.BeginHudSelection(__instance);

        [HarmonyPostfix]
        public static void Postfix(CombatHUD __instance) => GunnerTargetListPatch.EndHudSelection(__instance);
    }

    [HarmonyPatch(typeof(WeaponManager), "AddTargetList")]
    public static class WeaponManagerAddTargetListPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponManager __instance, Unit target)
        {
            if (!GunnerTargetListPatch.IsGunnerHudSelection(__instance)) return true;
            if (target != null && !GunnerState.TargetList.Contains(target))
                GunnerState.TargetList.Insert(0, target);
            return false;
        }
    }

    [HarmonyPatch(typeof(WeaponManager), "GetTargetList")]
    public static class WeaponManagerGetTargetListPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponManager __instance, ref List<Unit> __result)
        {
            if (!GunnerTargetListPatch.IsGunnerHudWeaponBind(__instance))
                return true;

            __result = GunnerState.TargetList;
            return false;
        }
    }

    [HarmonyPatch(typeof(WeaponManager), "RemoveTargetList")]
    public static class WeaponManagerRemoveTargetListPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponManager __instance, Unit target)
        {
            if (!GunnerTargetListPatch.IsGunnerHudSelection(__instance)) return true;
            GunnerState.TargetList.Remove(target);
            return false;
        }
    }

    [HarmonyPatch(typeof(WeaponManager), "ClearTargetList")]
    public static class WeaponManagerClearTargetListPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponManager __instance)
        {
            if (!GunnerTargetListPatch.IsGunnerHudSelection(__instance)) return true;
            GunnerState.TargetList.Clear();
            return false;
        }
    }

    [HarmonyPatch(typeof(WeaponManager), "SetTargetList")]
    public static class WeaponManagerSetTargetListPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponManager __instance)
        {
            // This is the replicated aircraft/pilot target-list path. It should update the
            // aircraft weapon manager, but never the gunner's private selection while gunning.
            return !GunnerTargetListPatch.IsGunnerWeaponManager(__instance);
        }
    }

    [HarmonyPatch(typeof(WeaponManager), "TargetListChanged")]
    public static class WeaponManagerTargetListChangedPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponManager __instance)
        {
            // Prevent vanilla from pushing the gunner target list into the aircraft's current
            // weapon station (which belongs to the pilot's selection).
            return !GunnerTargetListPatch.IsGunnerHudSelection(__instance);
        }
    }
}
