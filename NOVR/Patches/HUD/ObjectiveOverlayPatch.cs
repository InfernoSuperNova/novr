using System.Reflection;
using HarmonyLib;
using NOVR.PatchHelper;
using NOVR.VrUi.HarmonyPatches;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.HUD;

internal static class ObjectiveOverlayPatch
{
    private static readonly FieldInfo ObjectivePointerField = AccessTools.Field(typeof(ObjectiveOverlay), "objectivePointer");
    private static readonly FieldInfo ObjectiveDotField = AccessTools.Field(typeof(ObjectiveOverlay), "objectiveDot");
    private static readonly FieldInfo SizeIndicatorField = AccessTools.Field(typeof(ObjectiveOverlay), "sizeIndicator");
    private static readonly FieldInfo ObjectiveInfoField = AccessTools.Field(typeof(ObjectiveOverlay), "objectiveInfo");
    private static readonly FieldInfo PointerTailField = AccessTools.Field(typeof(ObjectiveOverlay), "pointerTail");
    private static readonly FieldInfo HiddenField = AccessTools.Field(typeof(ObjectiveOverlay), "hidden");

    // Move the dot and ring to the layer the arrow uses, this stops the marker from being misaligned and skewing when off center of the HUD.
    private static void PlaceDotAndRing(Image pointer, Image dot, Image ring, Vector3 hudPosition, float ringScale, Camera hudCamera, Aircraft? aircraft)
    {
        var iconLayer = pointer.transform.parent;
        if (iconLayer != null)
        {
            if (dot.transform.parent != iconLayer) dot.transform.SetParent(iconLayer, false);
            if (ring.transform.parent != iconLayer) ring.transform.SetParent(iconLayer, false);
        }

        // Keep the ring aligned with world up to stop it from rolling with the head.
        var up = aircraft != null ? aircraft.transform.InverseTransformDirection(Vector3.up) : hudCamera.transform.up;
        var towardMarker = hudPosition - hudCamera.transform.position;
        var rotation = towardMarker.sqrMagnitude > 1e-6f
            ? Quaternion.LookRotation(towardMarker, up)
            : Quaternion.identity;

        dot.transform.SetPositionAndRotation(hudPosition, rotation);
        ring.transform.SetPositionAndRotation(hudPosition, rotation);
        ring.transform.localScale = Vector3.one * ringScale;
    }

    [PatchPrefix(typeof(ObjectiveOverlay), nameof(ObjectiveOverlay.UpdateOverlay))]
    private static bool UpdateOverlay(ObjectiveOverlay __instance, MissionPosition.PositionResult result)
    {
        var mainCamera = APIBus.MainCamera;
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (mainCamera == null || cockpitHudCamera == null)
            return true;

        var objectivePointer = (Image)ObjectivePointerField.GetValue(__instance);
        var objectiveDot = (Image)ObjectiveDotField.GetValue(__instance);
        var sizeIndicator = (Image)SizeIndicatorField.GetValue(__instance);
        var objectiveInfo = (TMP_Text)ObjectiveInfoField.GetValue(__instance);
        var pointerTail = (Transform)PointerTailField.GetValue(__instance);

        if (objectivePointer == null || objectiveDot == null || sizeIndicator == null || objectiveInfo == null || pointerTail == null)
            return true;

        HiddenField.SetValue(__instance, false);
        objectivePointer.enabled = true;
        objectiveInfo.enabled = true;

        var worldPosition = result.Position.ToLocalPosition();
        var isOffScreen = VrHudProjectionHelper.PinToScreenEdge(worldPosition, out var hudPosition, out var arrowAngle);
        var angleToTarget = Vector3.Angle(mainCamera.transform.forward, result.Direction);

        objectivePointer.transform.position = hudPosition;
        objectiveDot.transform.position = hudPosition;

        if (isOffScreen)
        {
            sizeIndicator.enabled = false;
            objectivePointer.transform.localEulerAngles = new Vector3(0f, 0f, arrowAngle * Mathf.Rad2Deg - 90f);
        }
        else
        {
            sizeIndicator.enabled = true;
        }

        if (angleToTarget > 10f)
        {
            objectivePointer.enabled = true;
            objectiveDot.enabled = false;
            __instance.TextNoOverlap.SetTarget(pointerTail.position);
        }
        else
        {
            objectivePointer.enabled = false;
            objectiveDot.enabled = true;
            __instance.TextNoOverlap.SetTarget(objectiveDot.transform.position - Vector3.up * 25f);
        }

        var range = result.Range.GetValueOrDefault();
        var invDistance = 1f / (result.Distance != 0f ? result.Distance : 0.01f);
        var sizeRangeFactor = range * 20f * invDistance - 0.5f;
        var sizeColor = sizeIndicator.color;
        sizeColor.a = Mathf.Clamp01(sizeRangeFactor);
        sizeIndicator.color = sizeColor;
        var playerAircraft = SceneSingleton<CombatHUD>.i != null ? SceneSingleton<CombatHUD>.i.aircraft : null;
        PlaceDotAndRing(objectivePointer, objectiveDot, sizeIndicator, hudPosition, 35f * range * invDistance, cockpitHudCamera, playerAircraft);

        var label = result.Objective?.SavedObjective.DisplayName ?? "Waypoint";
        objectiveInfo.text = $"{label} {UnitConverter.DistanceReading(result.Distance)}";
        objectiveInfo.fontSize = (int)PlayerSettings.overlayTextSize;

        return false;
    }
}
