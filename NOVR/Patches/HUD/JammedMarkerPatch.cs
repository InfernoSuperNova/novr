using System.Reflection;
using HarmonyLib;
using NOVR.PatchHelper;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.HUD;

internal static class JammedMarkerPatch
{
    private static readonly FieldInfo VectorLineField = AccessTools.Field(typeof(JammedMarker), "vectorLine");
    private static readonly FieldInfo VectorLineImageField = AccessTools.Field(typeof(JammedMarker), "vectorLineImage");
    private static readonly FieldInfo RadarField = AccessTools.Field(typeof(JammedMarker), "radar");
    private static readonly FieldInfo UnitField = AccessTools.Field(typeof(JammedMarker), "unit");
    private static readonly FieldInfo JammedByField = AccessTools.Field(typeof(JammedMarker), "jammedBy");
    private static readonly FieldInfo MapIconField = AccessTools.Field(typeof(JammedMarker), "mapIcon");
    private static readonly FieldInfo JammedByIconField = AccessTools.Field(typeof(JammedMarker), "jammedByIcon");

    
    [PatchPrefix(typeof(JammedMarker), "Update")]
    private static bool Update(JammedMarker __instance)
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
