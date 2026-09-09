using HarmonyLib;

namespace NOVR.VrCamera;

[HarmonyPatch(typeof(CameraStateManager), "OnEnable")]
internal static class CameraStateManagerMainCameraPatch
{
    [HarmonyPostfix]
    private static void Postfix(CameraStateManager __instance)
    {
        var trackedMainCamera = VrCameraManager.GetTrackedMainCamera(__instance.gameObject);
        if (trackedMainCamera != null)
        {
            __instance.mainCamera = trackedMainCamera;
        }
    }
}
