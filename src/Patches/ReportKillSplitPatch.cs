using HarmonyLib;
using MulticrewNuclearOption.Gunner;
using NuclearOption.Networking;

namespace MulticrewNuclearOption.Patches
{
    [HarmonyPatch(typeof(FactionHQ), nameof(FactionHQ.ReportKillAction))]
    internal static class ReportKillSplitPatch
    {
        private static bool _inSplit;

        static bool Prefix(FactionHQ __instance, Player player, Unit target, ref float factor, ref bool __state)
        {
            if (_inSplit)
                return true;

            if (!GunnerKillCredit.ShouldSplitReportKill(target, ref factor))
                return true;

            __state = true;
            _inSplit = true;
            GunnerKillCredit.EnterSplitBypass();
            try
            {
                __instance.ReportKillAction(player, target, factor);
                if (GunnerKillCredit.TryGetGunnerForSecondReward(target, out Player gunnerPlayer) && gunnerPlayer != null)
                    __instance.ReportKillAction(gunnerPlayer, target, factor);
            }
            finally
            {
                GunnerKillCredit.ExitSplitBypass();
                _inSplit = false;
            }

            return false;
        }

        static void Finalizer(bool __state)
        {
            if (__state)
                GunnerKillCredit.FlushQueuedGunnerRewards();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AddScore))]
    internal static class GunnerSplitScorePatch
    {
        static void Prefix(Player __instance, ref float score)
        {
            GunnerKillCredit.ShouldSplitScore(__instance, ref score);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AddAllocation))]
    internal static class GunnerSplitAllocationPatch
    {
        static void Prefix(Player __instance, ref float amount)
        {
            GunnerKillCredit.ShouldSplitAllocation(__instance, ref amount);
        }
    }

    [HarmonyPatch(typeof(Unit), nameof(Unit.ReportKilled))]
    internal static class UnitReportKilledPatch
    {
        static void Prefix(Unit __instance)
        {
            if (__instance == null)
                return;

            GunnerKillCredit.TryBeginSplitRewards(__instance);
        }

        static void Finalizer()
        {
            GunnerKillCredit.FlushQueuedGunnerRewards();
        }
    }
}
