using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace NOVR.VrUi.HarmonyPatches;

internal static class ThreatItemVectorLineRotationSafePatch
{
    private static readonly FieldInfo MissileIconTransformField = AccessTools.Field(typeof(global::ThreatItem), "missileIconTransform");
    private static readonly FieldInfo PlayerAircraftIconTransformField = AccessTools.Field(typeof(global::ThreatItem), "playerAircraftIconTransform");
    private static readonly FieldInfo VectorLineField = AccessTools.Field(typeof(global::ThreatItem), "vectorLine");

    [HarmonyPatch(typeof(global::ThreatItem), "AlignVectorLine")]
    private static class AlignVectorLinePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::ThreatItem __instance)
        {
            var missileIconTransform = (Transform)MissileIconTransformField.GetValue(__instance);
            var playerAircraftIconTransform = (Transform)PlayerAircraftIconTransformField.GetValue(__instance);
            var vectorLine = (GameObject)VectorLineField.GetValue(__instance);
            if (missileIconTransform == null || playerAircraftIconTransform == null || vectorLine == null)
                return false;

            var iconLayer = SceneSingleton<DynamicMap>.i.iconLayer.transform;
            var playerLocalPosition = iconLayer.InverseTransformPoint(playerAircraftIconTransform.position);
            var missileLocalPosition = iconLayer.InverseTransformPoint(missileIconTransform.position);
            var localDelta = missileLocalPosition - playerLocalPosition;

            vectorLine.SetActive(true);
            vectorLine.transform.localPosition = playerLocalPosition;
            vectorLine.transform.localEulerAngles = new Vector3(
                0.0f,
                0.0f,
                -Mathf.Atan2(localDelta.x, localDelta.y) * Mathf.Rad2Deg);
            vectorLine.transform.localScale = (Vector3.one + Vector3.up * localDelta.magnitude) / iconLayer.lossyScale.x;
            return false;
        }
    }
}
