using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class JammedMarkerRotationSafePatch
{
    private static readonly FieldInfo VectorLineField = AccessTools.Field(typeof(global::JammedMarker), "vectorLine");
    private static readonly FieldInfo VectorLineImageField = AccessTools.Field(typeof(global::JammedMarker), "vectorLineImage");
    private static readonly FieldInfo RadarField = AccessTools.Field(typeof(global::JammedMarker), "radar");
    private static readonly FieldInfo UnitField = AccessTools.Field(typeof(global::JammedMarker), "unit");
    private static readonly FieldInfo JammedByField = AccessTools.Field(typeof(global::JammedMarker), "jammedBy");
    private static readonly FieldInfo MapIconField = AccessTools.Field(typeof(global::JammedMarker), "mapIcon");
    private static readonly FieldInfo JammedByIconField = AccessTools.Field(typeof(global::JammedMarker), "jammedByIcon");

    [HarmonyPatch(typeof(global::JammedMarker), "Update")]
    private static class UpdatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::JammedMarker __instance)
        {
            var unit = (Unit)UnitField.GetValue(__instance);
            var mapIcon = (UnitMapIcon)MapIconField.GetValue(__instance);
            var radar = (Radar)RadarField.GetValue(__instance);
            var jammedBy = (Unit)JammedByField.GetValue(__instance);
            if (unit == null || mapIcon == null || unit.disabled || radar == null || jammedBy == null || jammedBy.disabled || !radar.IsJammed())
                return true;

            var map = SceneSingleton<DynamicMap>.i;
            var iconLayer = map.iconLayer.transform;
            var jammedLocalPosition = iconLayer.InverseTransformPoint(mapIcon.transform.position);
            
            __instance.transform.localPosition = jammedLocalPosition;
            __instance.transform.localScale = Vector3.one / map.mapImage.transform.localScale.x;

            var vectorLineImage = (Image)VectorLineImageField.GetValue(__instance);
            var vectorLine = (GameObject)VectorLineField.GetValue(__instance);
            var jammedByIcon = (UnitMapIcon)JammedByIconField.GetValue(__instance);
            if (jammedByIcon == null || vectorLineImage == null || vectorLine == null)
            {
                if (vectorLineImage != null)
                    vectorLineImage.enabled = false;
                return false;
            }

            var jammerLocalPosition = iconLayer.InverseTransformPoint(jammedByIcon.transform.position);
            var localDelta = jammerLocalPosition - jammedLocalPosition;
            vectorLineImage.enabled = true;
            vectorLine.transform.localPosition = jammedLocalPosition;
            vectorLine.transform.localEulerAngles = new Vector3(
                0.0f,
                0.0f,
                -Mathf.Atan2(localDelta.x, localDelta.y) * Mathf.Rad2Deg);
            vectorLine.transform.localScale = new Vector3(1.0f, localDelta.magnitude, 1.0f);
            return false;
        }
    }
}
