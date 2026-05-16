using HarmonyLib;
using UnityEngine;

namespace NOVR.VrCamera;

internal static class CameraSelectionStatePatch
{
    [HarmonyPatch(typeof(CameraSelectionState), "UpdateOrbit")]
    private static class UpdateOrbit
    {
        [HarmonyPrefix]
        private static bool Prefix(CameraSelectionState __instance, CameraStateManager cam)
        {
            
            Traverse traverse = Traverse.Create(__instance);
            var target = traverse.Field("target").GetValue<Transform>();
            cam.cameraPivot.rotation = target.rotation * Quaternion.Euler(0.0f, 45f, 0.0f);
            return false;
        }
    }
}
