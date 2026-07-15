using System.Collections.Generic;

namespace MulticrewNuclearOption.Core
{
    public enum GunnerSessionState
    {
        Inactive = 0,
        JoinPending = 1,
        Active = 2,
    }

    /// <summary>
    /// Local-machine gunner state. Describes what station (if any) THIS client is
    /// currently manning, on which aircraft.
    /// </summary>
    public static class GunnerState
    {
        public static bool Active;
        public static Aircraft TargetAircraft;
        public static List<TurretStation> Stations = new List<TurretStation>();
        public static List<Unit> TargetList = new List<Unit>();
        public static int StationIndex = -1;
        public static int CameraPositionIndex;

        public static GunnerSessionState SessionState;
        public static uint PendingRequestId;
        public static ulong SessionToken;
        public static uint SessionGeneration;

        public static TurretStation Current =>
            (StationIndex >= 0 && StationIndex < Stations.Count) ? Stations[StationIndex] : null;

        public static void Reset()
        {
            Active = false;
            TargetAircraft = null;
            Stations.Clear();
            TargetList.Clear();
            StationIndex = -1;
            CameraPositionIndex = 0;
            SessionState = GunnerSessionState.Inactive;
            PendingRequestId = 0u;
            SessionToken = 0UL;
            SessionGeneration = 0u;
        }

        /// <summary>First non-disabled target in the gunner list, or null.</summary>
        public static Unit PrimaryTarget()
        {
            if (TargetList == null) return null;
            foreach (var target in TargetList)
            {
                if (target != null && !target.disabled)
                    return target;
            }
            return null;
        }

    }
}
