namespace MulticrewNuclearOption.Core
{
    /// <summary>
    /// Wire messages exchanged between a gunner client and the aircraft owner (relayed by the
    /// server). Kept as flat structs of primitives; serializers are registered manually in
    /// MulticrewNet (no Mirage weaver at mod build time).
    ///
    /// Direction for all of these: gunner-client -> server -> aircraft owner-client.
    /// </summary>
    public struct GunnerJoinMsg
    {
        public uint AircraftNetId;
        public byte Station;
        public uint GunnerPlayerNetId;
    }

    public struct GunnerLeaveMsg
    {
        public uint AircraftNetId;
        public byte Station;
    }

    /// <summary>Owner/client presence signal used to prove a player-piloted aircraft can receive gunner input.</summary>
    public struct WsoPresenceMsg
    {
        public uint AircraftNetId;
        public byte Protocol;
    }

    /// <summary>Reliable station control frame: aim direction, fire state, and target snapshot.</summary>
    public struct GunnerFireMsg
    {
        public uint AircraftNetId;
        public byte Station;
        public bool Firing;
        public float X;
        public float Y;
        public float Z;
        // Historical name: these are Unit persistent IDs, not Mirage NetIds.
        public uint[] TargetNetIds;
    }

    /// <summary>Gunner camera pose for reconstructing the gunner view on the pilot MFD.</summary>
    public struct GunnerViewStateMsg
    {
        public uint AircraftNetId;
        public byte Station;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float FwdX;
        public float FwdY;
        public float FwdZ;
        public float UpX;
        public float UpY;
        public float UpZ;
        public float Fov;
        public uint PrimaryTargetId;
    }

    /// <summary>Owner -> gunner hit feedback for remote gunner clients.</summary>
    public struct GunnerHitFeedbackMsg
    {
        public uint AircraftNetId;
        public byte Station;
        public float HitX;
        public float HitY;
        public float HitZ;
        public uint HitUnitId;
    }

    /// <summary>Explicit target-list handoff between pilot and gunner.</summary>
    public struct TargetShareMsg
    {
        public const byte GunnerToPilot = 0;
        public const byte PilotToGunner = 1;

        public uint AircraftNetId;
        public byte Direction;
        public bool Replace;
        // Historical name: these are Unit persistent IDs, not Mirage NetIds.
        public uint[] TargetNetIds;
    }
}
