using HarmonyLib;

namespace NOVR.VrCamera;

[HarmonyPatch(typeof(global::CameraStateManager), "OnEnable")]
internal static class CameraStateManagerMainCameraPatch
{
    [HarmonyPostfix]
    private static void Postfix(global::CameraStateManager __instance)
    {
        var trackedMainCamera = VrCameraManager.GetTrackedMainCamera(__instance.gameObject);
        if (trackedMainCamera != null)
        {
            __instance.mainCamera = trackedMainCamera;
        }
    }
}
