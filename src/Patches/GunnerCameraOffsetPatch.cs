using HarmonyLib;
using MulticrewNuclearOption.Core;
using UnityEngine;

namespace MulticrewNuclearOption.Patches
{
    [HarmonyPatch(typeof(CameraCockpitState), "UpdateState")]
    public static class GunnerCameraOffsetPatch
    {
        [HarmonyPostfix]
        public static void Postfix(CameraStateManager cam)
        {
            if (!GunnerState.Active || GunnerState.TargetAircraft == null || cam == null)
                return;
            if (cam.followingUnit != GunnerState.TargetAircraft || cam.cameraPivot == null)
                return;

            Vector3 offset = MulticrewConfig.GetCameraOffset(
                GunnerState.TargetAircraft,
                GunnerState.CameraPositionIndex);
            if (offset == Vector3.zero)
                return;

            Transform aircraftTransform = GunnerState.TargetAircraft.transform;
            cam.cameraPivot.position += aircraftTransform.TransformVector(offset);
        }
    }
}
