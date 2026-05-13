using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class FlightHudVelocityVectorPatch
{
    private const float HudDistance = 1000.0f;
    private const float VelocityVectorProjectionDistance = 1000.0f;
    private static readonly FieldInfo CockpitTransformField = AccessTools.Field(typeof(global::FlightHud), "cockpitTransform");
    private static readonly FieldInfo CockpitRbField = AccessTools.Field(typeof(global::FlightHud), "cockpitRB");

    [HarmonyPatch(typeof(global::FlightHud), "Update")]
    private static class UpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::FlightHud __instance)
        {
            var velocityVector = __instance.velocityVector;
            if (velocityVector == null)
                return;

            var cockpitTransform = (Transform)CockpitTransformField.GetValue(__instance);
            var cockpitRb = (Rigidbody)CockpitRbField.GetValue(__instance);
            if (cockpitTransform == null || cockpitRb == null)
                return;

            var velocity = cockpitRb.velocity;
            velocityVector.gameObject.SetActive(velocity.magnitude > 10.0f);
            if (!velocityVector.gameObject.activeSelf)
                return;

            var mainCamera = EventBus.MainCamera;
            var cockpitHudCamera = EventBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null)
                return;

            var velocityWorldPosition = cockpitTransform.position + velocity * VelocityVectorProjectionDistance;
            var mainCameraLocalPosition = mainCamera.transform.InverseTransformPoint(velocityWorldPosition);
            if (mainCameraLocalPosition.z <= 0.0f)
            {
                velocityVector.enabled = false;
                return;
            }

            velocityVector.enabled = true;
            velocityVector.transform.position = cockpitHudCamera.transform.TransformPoint(mainCameraLocalPosition).normalized * HudDistance;
            velocityVector.transform.rotation = cockpitHudCamera.transform.rotation;
        }
    }
}
