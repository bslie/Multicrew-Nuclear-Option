using UnityEngine;

namespace MulticrewNuclearOption.Core
{
    internal static class MulticrewValidation
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool TryNormalizeAim(float x, float y, float z, out Vector3 aim)
        {
            aim = new Vector3(x, y, z);
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
                return false;

            if (aim.sqrMagnitude < 1e-6f)
            {
                aim = Vector3.forward;
                return true;
            }

            aim.Normalize();
            return true;
        }

        public static bool TrySanitizeTargetIds(uint[] targetIds, out uint[] sanitized)
        {
            sanitized = new uint[0];
            if (targetIds == null)
                return true;

            if (targetIds.Length > MulticrewProtocol.MaxSharedTargets)
                return false;

            var unique = new uint[targetIds.Length];
            int count = 0;
            for (int i = 0; i < targetIds.Length; i++)
            {
                uint id = targetIds[i];
                if (id == 0u)
                    continue;

                bool seen = false;
                for (int j = 0; j < count; j++)
                {
                    if (unique[j] == id)
                    {
                        seen = true;
                        break;
                    }
                }

                if (seen)
                    continue;

                unique[count++] = id;
            }

            if (count == unique.Length)
            {
                sanitized = unique;
                return true;
            }

            sanitized = new uint[count];
            for (int i = 0; i < count; i++)
                sanitized[i] = unique[i];
            return true;
        }

        public static bool TryValidateView(ViewC2S msg, out ViewC2S sanitized)
        {
            sanitized = msg;
            if (!IsFinite(msg.PosX) || !IsFinite(msg.PosY) || !IsFinite(msg.PosZ))
                return false;
            if (!IsFinite(msg.FwdX) || !IsFinite(msg.FwdY) || !IsFinite(msg.FwdZ))
                return false;
            if (!IsFinite(msg.UpX) || !IsFinite(msg.UpY) || !IsFinite(msg.UpZ))
                return false;
            if (!IsFinite(msg.Fov))
                return false;

            var localPos = new Vector3(msg.PosX, msg.PosY, msg.PosZ);
            if (localPos.sqrMagnitude > MulticrewProtocol.MaxCameraLocalDistance * MulticrewProtocol.MaxCameraLocalDistance)
                return false;

            var fwd = new Vector3(msg.FwdX, msg.FwdY, msg.FwdZ);
            var up = new Vector3(msg.UpX, msg.UpY, msg.UpZ);
            if (fwd.sqrMagnitude < 1e-6f || up.sqrMagnitude < 1e-6f)
                return false;

            fwd.Normalize();
            up.Normalize();
            sanitized.FwdX = fwd.x;
            sanitized.FwdY = fwd.y;
            sanitized.FwdZ = fwd.z;
            sanitized.UpX = up.x;
            sanitized.UpY = up.y;
            sanitized.UpZ = up.z;
            sanitized.Fov = Mathf.Clamp(msg.Fov > 1f ? msg.Fov : 60f, MulticrewProtocol.MinFov, MulticrewProtocol.MaxFov);
            return true;
        }
    }
}
