using System.Reflection;
using HarmonyLib;
using NOVR.PatchHelper;
using NOVR.VrUi.HarmonyPatches;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.HUD;

internal static class HUDBombingStatePatch
{
    private static readonly FieldInfo AlignmentBarField = AccessTools.Field(typeof(HUDBombingState), "alignmentBar");
    private static readonly FieldInfo CcipPipperField = AccessTools.Field(typeof(HUDBombingState), "ccipPipper");
    private static readonly FieldInfo CcipLineField = AccessTools.Field(typeof(HUDBombingState), "ccipLine");
    private static readonly FieldInfo CcipFallTimeField = AccessTools.Field(typeof(HUDBombingState), "ccipFallTime");
    private static readonly FieldInfo CcrpFallTimeField = AccessTools.Field(typeof(HUDBombingState), "ccrpFallTime");
    private static readonly FieldInfo DropCountdownField = AccessTools.Field(typeof(HUDBombingState), "dropCountdown");
    private static readonly FieldInfo CcrpCircleField = AccessTools.Field(typeof(HUDBombingState), "ccrpCircle");
    private static readonly FieldInfo AverageTargetPositionField = AccessTools.Field(typeof(HUDBombingState), "averageTargetPosition");
    private static readonly FieldInfo CcipImpactPointSmoothedField = AccessTools.Field(typeof(HUDBombingState), "ccipImpactPointSmoothed");

    
    
    
    [PatchPostfix(typeof(HUDBombingState), nameof(HUDBombingState.UpdateWeaponDisplay))]
    private static void UpdateWeaponDisplay(HUDBombingState __instance, Aircraft aircraft)
    {
        var mainCamera = APIBus.MainCamera;
        var cockpitHudCamera = APIBus.MainCamera;
        if (mainCamera == null || cockpitHudCamera == null || aircraft == null)
            return;

        __instance.transform.localPosition = Vector3.zero;
        __instance.transform.rotation = cockpitHudCamera.transform.rotation;

        UpdateCcrpDisplay(__instance, aircraft);
        UpdateCcipDisplay(__instance);
    }

    private static void UpdateCcrpDisplay(global::HUDBombingState state, Aircraft aircraft)
    {
        var alignmentBar = (Image)AlignmentBarField.GetValue(state);
        var ccrpCircle = (Image)CcrpCircleField.GetValue(state);
        var dropCountdown = (Text)DropCountdownField.GetValue(state);
        var ccrpFallTime = (Text)CcrpFallTimeField.GetValue(state);
        var cockpitHudCamera = APIBus.MainCamera;
        if (alignmentBar == null || cockpitHudCamera == null || !alignmentBar.gameObject.activeSelf)
            return;

        var averageTargetPosition = (GlobalPosition)AverageTargetPositionField.GetValue(state);
        var targetDelta = averageTargetPosition - aircraft.GlobalPosition();
        var horizontalTargetDelta = new Vector3(targetDelta.x, 0.0f, targetDelta.z);
        var verticalOffset = -targetDelta.y + Vector3.Project(horizontalTargetDelta, aircraft.transform.forward).y;
        var upperWorldPosition = averageTargetPosition.ToLocalPosition() + Vector3.up * verticalOffset;
        var lowerWorldPosition = averageTargetPosition.ToLocalPosition() + Vector3.up * verticalOffset * 0.9f;

        if (!VrHudProjectionHelper.TryProjectToCockpitHud(upperWorldPosition, out var upperHudPosition) ||
            !VrHudProjectionHelper.TryProjectToCockpitHud(lowerWorldPosition, out var lowerHudPosition))
        {
            alignmentBar.gameObject.SetActive(false);
            return;
        }

        alignmentBar.transform.position = upperHudPosition;
        alignmentBar.transform.rotation = VrHudProjectionHelper.GetRotationAlongHudSegment(upperHudPosition, lowerHudPosition, cockpitHudCamera);

        if (ccrpCircle != null)
            ccrpCircle.transform.rotation = cockpitHudCamera.transform.rotation;

        if (dropCountdown != null)
            dropCountdown.transform.rotation = cockpitHudCamera.transform.rotation;

        if (ccrpFallTime != null)
            ccrpFallTime.transform.rotation = cockpitHudCamera.transform.rotation;
    }

    private static void UpdateCcipDisplay(global::HUDBombingState state)
    {
        var ccipPipper = (Image)CcipPipperField.GetValue(state);
        var ccipLine = (Image)CcipLineField.GetValue(state);
        var ccipFallTime = (Text)CcipFallTimeField.GetValue(state);
        var velocityVector = SceneSingleton<FlightHud>.i.velocityVector;
        var cockpitHudCamera = APIBus.MainCamera;
        if (ccipPipper == null || ccipLine == null || cockpitHudCamera == null || velocityVector == null || !ccipPipper.enabled)
            return;

        var ccipImpactPointSmoothed = (Vector3)CcipImpactPointSmoothedField.GetValue(state);
        var impactWorldPosition = ccipImpactPointSmoothed + Datum.origin.position;
        if (!VrHudProjectionHelper.TryProjectToCockpitHud(impactWorldPosition, out var pipperHudPosition))
        {
            ccipPipper.enabled = false;
            ccipLine.enabled = false;
            return;
        }

        ccipPipper.transform.position = pipperHudPosition;
        ccipPipper.transform.rotation = cockpitHudCamera.transform.rotation;

        var velocityHudPosition = velocityVector.transform.position;
        var lineDirection = velocityHudPosition - pipperHudPosition;
        if (lineDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            ccipLine.enabled = false;
            return;
        }

        var normalizedLineDirection = lineDirection.normalized;
        var lineStart = pipperHudPosition + normalizedLineDirection * VrHudProjectionHelper.ReferencePixelsToHudDistance(22.0f);
        var lineEnd = velocityHudPosition - normalizedLineDirection * VrHudProjectionHelper.ReferencePixelsToHudDistance(8.0f);
        var lineVector = lineEnd - lineStart;
        if (Vector3.Dot(lineDirection, lineVector) < 0.0f)
        {
            ccipLine.enabled = false;
            return;
        }

        VrHudProjectionHelper.SetVerticalLine(ccipLine.transform, lineStart, lineEnd, cockpitHudCamera);

        if (ccipFallTime != null)
            ccipFallTime.transform.rotation = cockpitHudCamera.transform.rotation;
    }

}
