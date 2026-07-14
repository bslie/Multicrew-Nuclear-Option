using System.Collections;

namespace MulticrewNuclearOption.Core
{
    /// <summary>
    /// Helpers for ThreatList missile-alarm audio. Vanilla only stops looping alarms inside
    /// ThreatList_OffMissileWarning when itemLookup still has the missile; HUD rebinding can
    /// desync that map while MissileWarning keeps firing off events.
    /// </summary>
    public static class ThreatListHelper
    {
        public static void TryRemoveMissileAlarm(ThreatList threatList, Missile missile)
        {
            if (threatList == null || missile == null) return;

            string seekerType = missile.GetSeekerType();
            if (string.IsNullOrEmpty(seekerType)) return;

            if (!Reflect.TryGetField<IDictionary>(threatList, "alarmLookup", out var lookup) ||
                lookup == null ||
                !lookup.Contains(seekerType))
                return;

            try
            {
                Reflect.Call(lookup[seekerType], "RemoveMissile", missile);
            }
            catch
            {
                // Best-effort; alarmLookup may be mid-rebind.
            }
        }
    }
}
