using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class HUDBoresightStateViewPositionPatch
{
    private const float HudDistance = 1000.0f;
    private static readonly FieldInfo ProjectedPositionField = AccessTools.Field(typeof(global::HUDBoresightState), "projectedPosition");
    private static readonly FieldInfo TargetPositionField = AccessTools.Field(typeof(global::HUDBoresightState), "targetPosition");
    private static readonly FieldInfo BoresightField = AccessTools.Field(typeof(global::HUDBoresightState), "boresight");
    private static readonly FieldInfo LineField = AccessTools.Field(typeof(global::HUDBoresightState), "line");
    private static readonly FieldInfo GunDirectionRelativeField = AccessTools.Field(typeof(global::HUDBoresightState), "gunDirectionRelative");
    private static readonly FieldInfo TargetDesignatorField = AccessTools.Field(typeof(global::HUDBoresightState), "targetDesignator");
    private static readonly FieldInfo ControlsFilterField = AccessTools.Field(typeof(global::HUDBoresightState), "controlsFilter");

    [HarmonyPatch(typeof(global::HUDBoresightState), nameof(global::HUDBoresightState.UpdateWeaponDisplay))]
    private static class UpdateWeaponDisplayPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::HUDBoresightState __instance, Aircraft aircraft, List<Unit> targetList)
        {
            var mainCamera = EventBus.MainCamera;
            var cockpitHudCamera = EventBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null || aircraft == null)
                return;

            var boresight = (Image)BoresightField.GetValue(__instance);
            var targetDesignator = (Image)TargetDesignatorField.GetValue(__instance);
            if (boresight == null)
                return;

            //boresight.transform.forward = cockpitHudCamera.transform.forward;
            var gunDirectionRelative = (Vector3)GunDirectionRelativeField.GetValue(__instance);
            var gunDirection = aircraft.transform.TransformDirection(gunDirectionRelative);
            var boresightWorldPosition = aircraft.transform.position + gunDirection * HudDistance;
            if (TryProjectToCockpitHud(boresightWorldPosition, out var boresightHudPosition))
                boresight.transform.position = boresightHudPosition;

            if (targetDesignator != null)
            {
                targetDesignator.transform.rotation = cockpitHudCamera.transform.rotation;
                targetDesignator.color = new Color(
                    0.0f,
                    1.0f,
                    0.0f,
                    Mathf.Clamp01(Vector3.Distance(targetDesignator.transform.position, boresight.transform.position) * 0.05f - 0.1f));
            }

            var velocityVector = SceneSingleton<FlightHud>.i.velocityVector;
            if (velocityVector != null)
            {
                velocityVector.color = new Color(
                    0.0f,
                    1.0f,
                    0.0f,
                    Mathf.Clamp01(FastMath.Distance(velocityVector.transform.position, boresight.transform.position) * 0.05f - 0.1f));
            }

            if (targetList.Count <= 0)
                return;

            var target = targetList[0];
            if (!aircraft.NetworkHQ.IsTargetPositionAccurate(target, 10.0f))
                return;

            var controlsFilter = (ControlsFilter)ControlsFilterField.GetValue(__instance);
            if (controlsFilter == null)
                return;

            controlsFilter.GetAim(target, out GlobalPosition? aimPoint, out GlobalPosition? _);
            if (!aimPoint.HasValue)
                return;

            UpdateLeadDisplay(__instance, target, aimPoint.Value);
        }
    }

    private static void UpdateLeadDisplay(global::HUDBoresightState state, Unit target, GlobalPosition aimPoint)
    {
        var boresight = (Image)BoresightField.GetValue(state);
        var targetPosition = (Image)TargetPositionField.GetValue(state);
        var projectedPosition = (Image)ProjectedPositionField.GetValue(state);
        var line = (Image)LineField.GetValue(state);
        var cockpitHudCamera = EventBus.CockpitHudCamera;
        if (boresight == null || targetPosition == null || projectedPosition == null || line == null || cockpitHudCamera == null)
            return;

        if (!TryProjectToCockpitHud(target.transform.position, out var targetHudPosition) ||
            !TryProjectToCockpitHud(aimPoint.ToLocalPosition(), out var aimHudPosition))
            return;

        targetPosition.transform.position = targetHudPosition;
        targetPosition.transform.rotation = cockpitHudCamera.transform.rotation;

        var projectedHudPosition = PlayerSettings.lagPip
            ? boresight.transform.position - aimHudPosition + targetHudPosition
            : aimHudPosition;
        projectedPosition.transform.position = projectedHudPosition;
        projectedPosition.transform.rotation = cockpitHudCamera.transform.rotation;

        var targetLocalPosition = PlayerSettings.lagPip ? Vector3.zero : targetPosition.transform.localPosition;
        var projectedLocalPosition = projectedPosition.transform.localPosition;
        var delta = targetLocalPosition - projectedLocalPosition;

        line.transform.localPosition = (targetLocalPosition + projectedLocalPosition) * 0.5f;
        line.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var magnitude = delta.magnitude;
        boresight.color = FastMath.InRange(projectedPosition.transform.position, boresight.transform.position, 15.0f)
            ? Color.green
            : Color.yellow;
        if (magnitude > 14.0f)
        {
            line.enabled = true;
            magnitude -= 14.0f;
        }
        else
        {
            line.enabled = false;
        }

        line.transform.localScale = new Vector3(magnitude, 1.0f, 1.0f);
    }

    private static bool TryProjectToCockpitHud(Vector3 worldPosition, out Vector3 hudPosition)
    {
        hudPosition = Vector3.zero;
        var mainCamera = EventBus.MainCamera;
        var cockpitHudCamera = EventBus.CockpitHudCamera;
        if (mainCamera == null || cockpitHudCamera == null)
            return false;

        var mainCameraLocal = mainCamera.transform.InverseTransformPoint(worldPosition);
        if (mainCameraLocal.z <= 0.0f)
            return false;

        hudPosition = cockpitHudCamera.transform.TransformPoint(mainCameraLocal).normalized * HudDistance;
        return true;
    }
}
