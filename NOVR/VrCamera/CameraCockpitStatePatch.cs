using System.Reflection;
using HarmonyLib;

namespace NOVR.VrCamera;

public class CameraCockpitStatePatch
{
    [HarmonyPatch(typeof(CameraCockpitState), "UpdateState")]
    private static class UpdateStatePatch
    {
        private static readonly FieldInfo PanViewField = AccessTools.Field(typeof(CameraCockpitState), "panView");
        private static readonly FieldInfo TiltViewField = AccessTools.Field(typeof(CameraCockpitState), "tiltView");
        
        
        [HarmonyPostfix]
        private static void Postfix(CameraCockpitState __instance, CameraStateManager cam)
        {
            PanViewField.SetValue(__instance, 0.0f);
            TiltViewField.SetValue(__instance, 0.0f);

            if (GameManager.flightControlsEnabled && !DynamicMap.mapMaximized)
            {
                VrZoomController.UpdateZoomInput(GameManager.playerInput.GetAxis("Zoom View"));
            }
        }
    }

    [HarmonyPatch(typeof(CameraCockpitState), "EnterState")]
    private static class EnterStatePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            VrZoomController.ResetZoom();
        }
    }

    [HarmonyPatch(typeof(CameraCockpitState), "LeaveState")]
    private static class LeaveStatePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            VrZoomController.ResetZoom();
        }
    }
}
