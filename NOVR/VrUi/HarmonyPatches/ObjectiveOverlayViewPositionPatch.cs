using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

// Stock ObjectiveOverlay positions waypoint markers with flat-screen pixel coordinates, which land far off-HUD
// once the canvas is world space. Re-project them through the cockpit HUD camera instead.
internal static class ObjectiveOverlayViewPositionPatch
{
    private const float DotModeMaxAngleDegrees = 10.0f;
    private const float DotLabelOffsetPixels = 25.0f;
    private const float SizeIndicatorScale = 0.18f;

    private static readonly FieldInfo ObjectivePointerField = AccessTools.Field(typeof(global::ObjectiveOverlay), "objectivePointer");
    private static readonly FieldInfo ObjectiveDotField = AccessTools.Field(typeof(global::ObjectiveOverlay), "objectiveDot");
    private static readonly FieldInfo SizeIndicatorField = AccessTools.Field(typeof(global::ObjectiveOverlay), "sizeIndicator");
    private static readonly FieldInfo ObjectiveInfoField = AccessTools.Field(typeof(global::ObjectiveOverlay), "objectiveInfo");
    private static readonly FieldInfo PointerTailField = AccessTools.Field(typeof(global::ObjectiveOverlay), "pointerTail");
    private static readonly FieldInfo HiddenField = AccessTools.Field(typeof(global::ObjectiveOverlay), "hidden");

    private static bool VrCamerasAvailable => APIBus.MainCamera != null && APIBus.CockpitHudCamera != null;

    [HarmonyPatch(typeof(global::ObjectiveOverlay), nameof(global::ObjectiveOverlay.UpdateOverlay))]
    private static class UpdateOverlayPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::ObjectiveOverlay __instance, global::MissionPosition.PositionResult result)
        {
            var mainCamera = APIBus.MainCamera;
            var cockpitHudCamera = APIBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null)
                return true;

            var objectivePointer = ObjectivePointerField.GetValue(__instance) as Image;
            var objectiveDot = ObjectiveDotField.GetValue(__instance) as Image;
            var sizeIndicator = SizeIndicatorField.GetValue(__instance) as Image;
            var objectiveInfo = ObjectiveInfoField.GetValue(__instance) as TextMeshProUGUI;
            var pointerTail = PointerTailField.GetValue(__instance) as Transform;
            if (objectivePointer == null || objectiveDot == null || sizeIndicator == null || objectiveInfo == null || pointerTail == null)
                return true;

            PrepareForVr(__instance, objectiveInfo);
            HiddenField.SetValue(__instance, false);
            objectiveInfo.enabled = true;

            var hudRotation = cockpitHudCamera.transform.rotation;
            var worldPosition = result.Position.ToLocalPosition();
            var offScreen = VrHudProjection.PinToScreenEdge(worldPosition, out var hudPosition, out var arrowAngle);

            objectivePointer.transform.position = hudPosition;
            objectivePointer.transform.rotation = hudRotation * Quaternion.Euler(0.0f, 0.0f, arrowAngle * Mathf.Rad2Deg - 90.0f);
            objectiveDot.transform.position = hudPosition;
            objectiveDot.transform.rotation = hudRotation;

            var dotMode = Vector3.Angle(mainCamera.transform.forward, result.Direction) <= DotModeMaxAngleDegrees;
            objectivePointer.enabled = !dotMode;
            objectiveDot.enabled = dotMode;

            objectiveInfo.transform.position = dotMode
                ? hudPosition - cockpitHudCamera.transform.up * VrHudProjection.ReferencePixelsToHudDistance(DotLabelOffsetPixels)
                : pointerTail.position;
            objectiveInfo.transform.rotation = hudRotation;

            UpdateSizeIndicator(sizeIndicator, result, worldPosition, hudPosition, hudRotation, offScreen, mainCamera);

            var label = result.Objective != null ? result.Objective.SavedObjective.DisplayName : "Waypoint";
            objectiveInfo.text = label + " " + global::UnitConverter.DistanceReading(result.Distance);
            objectiveInfo.fontSize = (int)global::PlayerSettings.overlayTextSize;
            return false;
        }
    }

    // The stock de-overlap pass writes label positions as Vector2, which zeroes their depth in world space.
    [HarmonyPatch(typeof(global::ObjectiveOverlayManager), "StopTextOverlap")]
    private static class StopTextOverlapPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !VrCamerasAvailable;
    }

    // Overlays are instantiated at runtime from a prefab, and the label is reparented out of the overlay,
    // so neither picks up the VR UI layer. The label prefab also has a zero Z scale, which breaks the SDF
    // perspective filter under the perspective HUD camera and renders every glyph as a solid block.
    private static void PrepareForVr(global::ObjectiveOverlay overlay, TextMeshProUGUI objectiveInfo)
    {
        var vrUiLayer = LayerHelper.GetVrUiLayer();
        if (objectiveInfo.gameObject.layer == (int)vrUiLayer)
            return;

        LayerHelper.SetLayerRecursive(overlay.transform, vrUiLayer);
        LayerHelper.SetLayerRecursive(objectiveInfo.transform, vrUiLayer);
        var labelScale = objectiveInfo.transform.localScale;
        if (Mathf.Approximately(labelScale.z, 0.0f))
            objectiveInfo.transform.localScale = new Vector3(labelScale.x, labelScale.y, 1.0f);
    }

    private static void UpdateSizeIndicator(Image sizeIndicator, global::MissionPosition.PositionResult result,
        Vector3 worldPosition, Vector3 hudPosition, Quaternion hudRotation, bool offScreen, Camera mainCamera)
    {
        var range = result.Range.GetValueOrDefault();
        var distance = result.Distance == 0.0f ? 0.01f : result.Distance;
        var nativeDiameter = sizeIndicator.rectTransform.rect.height;
        var parent = sizeIndicator.transform.parent;
        var parentScale = parent != null ? parent.lossyScale.y : 1.0f;

        if (offScreen || range <= 0.0f || nativeDiameter <= Mathf.Epsilon || parentScale <= Mathf.Epsilon ||
            !VrHudProjection.TryProjectToCockpitHud(worldPosition + mainCamera.transform.right * range, out var edgeHudPosition))
        {
            sizeIndicator.enabled = false;
            return;
        }

        var hudRadius = Vector3.Distance(hudPosition, edgeHudPosition);
        sizeIndicator.enabled = true;
        sizeIndicator.transform.position = hudPosition;
        sizeIndicator.transform.rotation = hudRotation;
        sizeIndicator.transform.localScale = Vector3.one * (SizeIndicatorScale * 2.0f * hudRadius / (nativeDiameter * parentScale));
        sizeIndicator.color = sizeIndicator.color.WithAlpha(Mathf.Clamp01(range * 20.0f / distance - 0.5f));
    }
}
