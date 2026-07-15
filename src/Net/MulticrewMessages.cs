namespace MulticrewNuclearOption.Core
{
    public struct HelloC2S
    {
        public byte WireVersion;
    }

    public struct HelloS2C
    {
        public byte WireVersion;
    }

    public struct PresenceC2S
    {
        public byte WireVersion;
    }

    public struct PresenceS2C
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public uint PilotPlayerNetId;
    }

    public struct PresenceRemovedS2C
    {
        public byte WireVersion;
        public uint AircraftNetId;
    }

    public struct JoinReqC2S
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public uint RequestId;
    }

    public struct JoinGrantedS2C
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public uint RequestId;
        public ulong SessionToken;
        public uint Generation;
        public uint GunnerPlayerNetId;
    }

    public struct JoinRejectedS2C
    {
        public byte WireVersion;
        public uint RequestId;
        public byte Reason;
    }

    public struct LeaveC2S
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
    }

    public struct LeaseRevokedS2C
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
        public byte Reason;
    }

    public struct LeaseActivatedS2C
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
        public uint GunnerPlayerNetId;
    }

    public struct ControlC2S
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
        public uint Sequence;
        public bool Firing;
        public float X;
        public float Y;
        public float Z;
    }

    public struct ControlS2C
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
        public uint Sequence;
        public bool Firing;
        public float X;
        public float Y;
        public float Z;
    }

    public struct TargetsC2S
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
        public uint Sequence;
        public bool Replace;
        public uint[] TargetIds;
    }

    public struct TargetsS2C
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public byte Direction;
        public ulong SessionToken;
        public uint Generation;
        public uint Sequence;
        public bool Replace;
        public uint[] TargetIds;
    }

    public struct ViewC2S
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
        public uint Sequence;
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

    public struct ViewS2C
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
        public uint Sequence;
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

    public struct HitFeedbackC2S
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
        public float HitX;
        public float HitY;
        public float HitZ;
        public uint HitUnitId;
    }

    public struct HitFeedbackS2C
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
        public float HitX;
        public float HitY;
        public float HitZ;
        public uint HitUnitId;
    }

    public struct HeartbeatC2S
    {
        public byte WireVersion;
        public uint AircraftNetId;
        public byte Station;
        public ulong SessionToken;
        public uint Generation;
    }
}
