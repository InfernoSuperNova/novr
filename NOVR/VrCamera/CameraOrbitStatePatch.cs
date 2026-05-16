using HarmonyLib;

namespace NOVR.VrCamera;

internal static class CameraOrbitStatePatch
{
    [HarmonyPatch(typeof(CameraOrbitState), "CameraMotion")]
    private static class CameraMotionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CameraStateManager cam)
        {
            if (cam?.cameraPivot == null)
                return true;

            cam.transform.SetPositionAndRotation(cam.cameraPivot.position, cam.cameraPivot.rotation);
            return false;
        }
    }
}
