namespace MulticrewNuclearOption.Core
{
    public static class MulticrewProtocol
    {
        public const byte WireVersion = 2;
        public const int MaxSharedTargets = 32;
        public const byte MaxStationIndex = 31;

        public const float PresenceTtlSeconds = 5f;
        public const float PresenceAnnounceSeconds = 1f;
        public const float LeaseTtlSeconds = 5f;
        public const float HeartbeatSeconds = 1f;
        public const float FireWatchdogSeconds = 0.3f;

        public const float ControlRateSeconds = 1f / 30f;
        public const float ViewRateSeconds = 1f / 15f;
        public const float MinFov = 10f;
        public const float MaxFov = 120f;
        public const float MaxCameraLocalDistance = 200f;
    }

    public static class TargetShareDirection
    {
        public const byte GunnerToPilot = 0;
        public const byte PilotToGunner = 1;
    }

    public static class JoinRejectReason
    {
        public const byte Unknown = 0;
        public const byte NotAuthenticated = 1;
        public const byte InvalidTarget = 2;
        public const byte StationBusy = 3;
        public const byte NotFriendly = 4;
        public const byte NoPilotPresence = 5;
        public const byte AlreadyLeased = 6;
    }

    public static class LeaseRevokeReason
    {
        public const byte Unknown = 0;
        public const byte ManualLeave = 1;
        public const byte Disconnect = 2;
        public const byte Timeout = 3;
        public const byte InvalidToken = 4;
        public const byte AircraftDisabled = 5;
    }
}
