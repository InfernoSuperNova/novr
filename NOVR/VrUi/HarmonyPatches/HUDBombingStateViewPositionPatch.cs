using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class HUDBombingStateViewPositionPatch
{
    private static readonly FieldInfo AlignmentBarField = AccessTools.Field(typeof(global::HUDBombingState), "alignmentBar");
    private static readonly FieldInfo CcipPipperField = AccessTools.Field(typeof(global::HUDBombingState), "ccipPipper");
    private static readonly FieldInfo CcipLineField = AccessTools.Field(typeof(global::HUDBombingState), "ccipLine");
    private static readonly FieldInfo CcipFallTimeField = AccessTools.Field(typeof(global::HUDBombingState), "ccipFallTime");
    private static readonly FieldInfo CcrpFallTimeField = AccessTools.Field(typeof(global::HUDBombingState), "ccrpFallTime");
    private static readonly FieldInfo DropCountdownField = AccessTools.Field(typeof(global::HUDBombingState), "dropCountdown");
    private static readonly FieldInfo CcrpCircleField = AccessTools.Field(typeof(global::HUDBombingState), "ccrpCircle");
    private static readonly FieldInfo AverageTargetPositionField = AccessTools.Field(typeof(global::HUDBombingState), "averageTargetPosition");
    private static readonly FieldInfo CcipImpactPointSmoothedField = AccessTools.Field(typeof(global::HUDBombingState), "ccipImpactPointSmoothed");

    [HarmonyPatch(typeof(global::HUDBombingState), nameof(global::HUDBombingState.UpdateWeaponDisplay))]
    private static class UpdateWeaponDisplayPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::HUDBombingState __instance, Aircraft aircraft)
        {
            var mainCamera = APIBus.MainCamera;
            var cockpitHudCamera = APIBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null || aircraft == null)
                return;

            __instance.transform.localPosition = Vector3.zero;
            __instance.transform.rotation = cockpitHudCamera.transform.rotation;

            UpdateCcrpDisplay(__instance, aircraft);
            UpdateCcipDisplay(__instance);
        }
    }

    private static void UpdateCcrpDisplay(global::HUDBombingState state, Aircraft aircraft)
    {
        var alignmentBar = (Image)AlignmentBarField.GetValue(state);
        var ccrpCircle = (Image)CcrpCircleField.GetValue(state);
        var dropCountdown = (Text)DropCountdownField.GetValue(state);
        var ccrpFallTime = (Text)CcrpFallTimeField.GetValue(state);
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (alignmentBar == null || cockpitHudCamera == null || !alignmentBar.gameObject.activeSelf)
            return;

        var averageTargetPosition = (GlobalPosition)AverageTargetPositionField.GetValue(state);
        var targetDelta = averageTargetPosition - aircraft.GlobalPosition();
        var horizontalTargetDelta = new Vector3(targetDelta.x, 0.0f, targetDelta.z);
        var verticalOffset = -targetDelta.y + Vector3.Project(horizontalTargetDelta, aircraft.transform.forward).y;
        var upperWorldPosition = averageTargetPosition.ToLocalPosition() + Vector3.up * verticalOffset;
        var lowerWorldPosition = averageTargetPosition.ToLocalPosition() + Vector3.up * verticalOffset * 0.9f;

        if (!VrHudProjection.TryProjectToCockpitHud(upperWorldPosition, out var upperHudPosition) ||
            !VrHudProjection.TryProjectToCockpitHud(lowerWorldPosition, out var lowerHudPosition))
        {
            alignmentBar.gameObject.SetActive(false);
            return;
        }

        alignmentBar.transform.position = upperHudPosition;
        alignmentBar.transform.rotation = VrHudProjection.GetRotationAlongHudSegment(upperHudPosition, lowerHudPosition, cockpitHudCamera);

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
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (ccipPipper == null || ccipLine == null || cockpitHudCamera == null || velocityVector == null || !ccipPipper.enabled)
            return;

        var ccipImpactPointSmoothed = (Vector3)CcipImpactPointSmoothedField.GetValue(state);
        var impactWorldPosition = ccipImpactPointSmoothed + Datum.origin.position;
        if (!VrHudProjection.TryProjectToCockpitHud(impactWorldPosition, out var pipperHudPosition))
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
        var lineStart = pipperHudPosition + normalizedLineDirection * VrHudProjection.ReferencePixelsToHudDistance(22.0f);
        var lineEnd = velocityHudPosition - normalizedLineDirection * VrHudProjection.ReferencePixelsToHudDistance(8.0f);
        var lineVector = lineEnd - lineStart;
        if (Vector3.Dot(lineDirection, lineVector) < 0.0f)
        {
            ccipLine.enabled = false;
            return;
        }

        VrHudProjection.SetVerticalLine(ccipLine.transform, lineStart, lineEnd, cockpitHudCamera);

        if (ccipFallTime != null)
            ccipFallTime.transform.rotation = cockpitHudCamera.transform.rotation;
    }

}
