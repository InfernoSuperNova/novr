using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using NOVR.PatchHelper;
using NOVR.VrUi.HarmonyPatches;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.HUD;

// Ensures our hud markers are in our VR UI camera's space
internal static class HUDUnitMarkerPatch
{
    private const float CockpitHudPositionScale = 0.01f;
    private const float NearMarkerBlendStartMeters = 2.0f;
    private const float NearMarkerFullScaleMeters = 1.0f;

    private static readonly FieldInfo HiddenField = AccessTools.Field(typeof(HUDUnitMarker), "hidden");
    private static readonly FieldInfo TransformField = AccessTools.Field(typeof(HUDUnitMarker), "_transform");
    private static readonly FieldInfo IconField = AccessTools.Field(typeof(HUDUnitMarker), "icon");
    private static readonly FieldInfo TimeCreatedField = AccessTools.Field(typeof(HUDUnitMarker), "timeCreated");
    private static readonly FieldInfo ColorField = AccessTools.Field(typeof(HUDUnitMarker), "color");
    private static readonly FieldInfo FlashingField = AccessTools.Field(typeof(HUDUnitMarker), "flashing");
    private static readonly FieldInfo MaximizedField = AccessTools.Field(typeof(HUDUnitMarker), "maximized");
    private static readonly FieldInfo AlwaysMaximizedField = AccessTools.Field(typeof(HUDUnitMarker), "alwaysMaximized");
    private static readonly FieldInfo CustomScaleField = AccessTools.Field(typeof(HUDUnitMarker), "customScale");
    private static readonly FieldInfo DistanceScaleField = AccessTools.Field(typeof(HUDUnitMarker), "distanceScale");
    private static readonly FieldInfo TargetArrowField = AccessTools.Field(typeof(CombatHUD), "targetArrow");
    private static readonly FieldInfo TargetArrowTailField = AccessTools.Field(typeof(CombatHUD), "targetArrowTail");
    private static readonly FieldInfo TargetTextField = AccessTools.Field(typeof(CombatHUD), "targetText");
    private static readonly FieldInfo TargetInfoField = AccessTools.Field(typeof(CombatHUD), "targetInfo");
    private static readonly Dictionary<int, Vector3> BaseLocalScales = new();

    private static bool GetHidden(HUDUnitMarker marker) => (bool)HiddenField.GetValue(marker);
    private static Transform GetTransform(HUDUnitMarker marker) => (Transform)TransformField.GetValue(marker);
    private static Sprite GetIcon(HUDUnitMarker marker) => (Sprite)IconField.GetValue(marker);
    private static float GetTimeCreated(HUDUnitMarker marker) => (float)TimeCreatedField.GetValue(marker);
    private static Color GetColor(HUDUnitMarker marker) => (Color)ColorField.GetValue(marker);
    private static bool GetFlashing(HUDUnitMarker marker) => (bool)FlashingField.GetValue(marker);
    private static bool GetMaximized(HUDUnitMarker marker) => (bool)MaximizedField.GetValue(marker);
    private static bool GetAlwaysMaximized(HUDUnitMarker marker) => (bool)AlwaysMaximizedField.GetValue(marker);
    private static float GetCustomScale(HUDUnitMarker marker) => (float)CustomScaleField.GetValue(marker);
    private static float GetDistanceScale(HUDUnitMarker marker) => (float)DistanceScaleField.GetValue(marker);
    
    [PatchPrefix(typeof(HUDUnitMarker), nameof(HUDUnitMarker.UpdatePosition))]
    private static bool UpdatePosition(HUDUnitMarker __instance, FactionHQ hq, ref GlobalPosition viewPosition, ref Vector3 cameraForward)
    {
        var hudCamera = APIBus.CockpitHudCamera;
        var markerTransform = GetTransform(__instance);
        markerTransform.rotation = hudCamera.transform.rotation;

        RotateTargetInfoToHudCamera(hudCamera);

        if (GetHidden(__instance))
            return false;

        if (!TryGetKnownPosition(__instance, hq, out var knownPosition))
            return false;

        if (__instance.selected)
        {
            UpdateSelectedMarker(__instance, knownPosition, hudCamera);
            return false;
        }

        if (IsBehindMainCamera(knownPosition))
        {
            __instance.image.enabled = false;
            return false;
        }

        UpdateUnselectedMarker(__instance, knownPosition);
        return false;
    }

    [PatchPostfix(typeof(HUDUnitMarker), nameof(HUDUnitMarker.UpdateVisibility))]
    private static void UpdateVisibility(HUDUnitMarker __instance)
    {
        NormalizeAngularScale(__instance);
    }

    [PatchPostfix(typeof(HUDUnitMarker), nameof(HUDUnitMarker.SelectMarker))]
    private static void SelectMarker(HUDUnitMarker __instance)
    {
        NormalizeAngularScale(__instance);
    }

    [PatchPostfix(typeof(HUDUnitMarker), nameof(HUDUnitMarker.DeselectMarker))]
    private static void DeselectMarker(HUDUnitMarker __instance)
    {
        NormalizeAngularScale(__instance);
    }

    private static void RotateTargetInfoToHudCamera(Component hudCamera)
    {
        var targetInfo = (Text)TargetInfoField.GetValue(SceneSingleton<CombatHUD>.i);
        if (targetInfo != null)
        {
            targetInfo.transform.rotation = hudCamera.transform.rotation;
        }
    }

    private static bool TryGetKnownPosition(HUDUnitMarker marker, FactionHQ hq, out GlobalPosition knownPosition)
    {
        knownPosition = marker.unit.GlobalPosition();
        return !marker.outdated || hq.TryGetKnownPosition(marker.unit, out knownPosition);
    }

    private static void UpdateSelectedMarker(HUDUnitMarker marker, GlobalPosition knownPosition, Component hudCamera)
    {
        var localPos = knownPosition.ToLocalPosition();
        if (VrHudProjectionHelper.PinToScreenEdge(localPos, out var rayToScreen, out _))
        {
            marker.image.enabled = false;
            if (VrHudProjectionHelper.TryProjectDirectionToCockpitHud(localPos, out var targetHudPosition))
                SetTargetArrow(
                    SceneSingleton<CombatHUD>.i,
                    true,
                    ToCockpitHudWorldPosition(rayToScreen, localPos),
                    ToCockpitHudWorldPosition(targetHudPosition, localPos),
                    -hudCamera.transform.forward,
                    hudCamera);
        }
        else
        {
            marker.image.enabled = true;
            if (VrHudProjectionHelper.TryProjectToCockpitHud(localPos, out var targetHudPosition))
                GetTransform(marker).position = ToCockpitHudWorldPosition(targetHudPosition, localPos);
            SetTargetArrow(SceneSingleton<CombatHUD>.i, false, Vector3.zero, Vector3.zero, Vector3.zero, hudCamera);
        }

        ApplyAngularScale(marker, GetTransform(marker).position);
        UpdateTargetInfo(marker, hudCamera);
        UpdateSelectedMarkerSprite(marker);
    }

    private static void UpdateTargetInfo(HUDUnitMarker marker, Component hudCamera)
    {
        var targetInfo = (Text)TargetInfoField.GetValue(SceneSingleton<CombatHUD>.i);
        if (targetInfo == null)
            return;

        targetInfo.transform.position = GetTransform(marker).position;
        targetInfo.transform.rotation = hudCamera.transform.rotation;
        ApplyAngularScale(targetInfo.transform, targetInfo.transform.position);
    }

    private static void UpdateSelectedMarkerSprite(HUDUnitMarker marker)
    {
        if (!marker.unit.HasRadarEmission())
            return;

        if ((marker.unit.radar as Radar).IsJammed())
        {
            if (marker.image.sprite != GameAssets.i.targetUnitSpriteJammed)
                marker.image.sprite = GameAssets.i.targetUnitSpriteJammed;
            return;
        }

        if (marker.image.sprite == GameAssets.i.targetUnitSpriteJammed)
            marker.image.sprite = DynamicMap.GetFactionMode(marker.unit.NetworkHQ) == FactionMode.Friendly
                ? GameAssets.i.targetUnitSpriteFriendly
                : GetIcon(marker);
    }

    private static bool IsBehindMainCamera(GlobalPosition knownPosition)
    {
        var mainCamera = APIBus.MainCamera;
        var realCameraPosition = mainCamera.transform.GlobalPosition();
        var realCameraForward = mainCamera.transform.forward;
        return Vector3.Dot(knownPosition - realCameraPosition, realCameraForward) < 0.0f;
    }

    private static void UpdateUnselectedMarker(HUDUnitMarker marker, GlobalPosition knownPosition)
    {
        if (!marker.image.enabled)
            marker.image.enabled = true;

        var localPos = knownPosition.ToLocalPosition();
        var hudPos = ToCockpitHudLocalPosition(VrHudProjectionHelper.WorldToHud(localPos));
        var worldHudPos = ToCockpitHudWorldPosition(hudPos, localPos);
        
        GetTransform(marker).position = worldHudPos;
        ApplyAngularScale(marker, worldHudPos);

        if (marker.fresh)
        {
            var markerColor = GetColor(marker);
            var elapsed = Time.timeSinceLevelLoad - GetTimeCreated(marker);
            marker.image.color = Color.Lerp(markerColor + Color.yellow, markerColor, elapsed);
            if (elapsed > 1.0f)
                marker.fresh = false;
        }

        if (!GetFlashing(marker))
            return;

        var flashingColor = GetColor(marker);
        marker.image.color = Color.Lerp(flashingColor + Color.yellow, flashingColor, Mathf.Sin(Time.timeSinceLevelLoad * 20f) + 0.5f);
    }

    private static void ApplyAngularScale(HUDUnitMarker marker, Vector3 worldPosition)
    {
        var hudCamera = APIBus.CockpitHudCamera;
        if (hudCamera == null)
            return;

        var baseScale = GetMarkerBaseScale(marker);
        var depth = Vector3.Dot(worldPosition - hudCamera.transform.position, hudCamera.transform.forward);
        if (depth <= Mathf.Epsilon)
            depth = VrHudProjectionHelper.HudDistance;

        var angularScale = baseScale * depth / VrHudProjectionHelper.HudDistance;
        var transform = GetTransform(marker);
        transform.localScale = Vector3.one * angularScale;
    }

    private static void NormalizeAngularScale(HUDUnitMarker marker)
    {
        ApplyAngularScale(marker, GetTransform(marker).position);
    }

    private static Vector3 ToCockpitHudWorldPosition(Vector3 cockpitHudPosition, Vector3 targetWorldPosition)
    {
        var hudCamera = APIBus.CockpitHudCamera;
        if (hudCamera == null)
            return cockpitHudPosition;

        var scaledPosition = cockpitHudPosition * CockpitHudPositionScale;
        var distanceToTarget = Vector3.Distance(APIBus.MainCamera.transform.position, targetWorldPosition);
        var unscaledBlend = Mathf.InverseLerp(NearMarkerBlendStartMeters, NearMarkerFullScaleMeters, distanceToTarget);
        var blendedPosition = Vector3.Lerp(scaledPosition, cockpitHudPosition, unscaledBlend);
        return hudCamera.transform.position + blendedPosition;
    }

    private static Vector3 ToCockpitHudLocalPosition(Vector3 cockpitHudWorldPosition)
    {
        var hudCamera = APIBus.CockpitHudCamera;
        return hudCamera == null ? cockpitHudWorldPosition : cockpitHudWorldPosition - hudCamera.transform.position;
    }

    private static float GetMarkerBaseScale(HUDUnitMarker marker)
    {
        if (marker.selected)
            return 20.0f;

        if (GetAlwaysMaximized(marker) || GetMaximized(marker))
            return GetCustomScale(marker) * GetDistanceScale(marker);

        return DynamicMap.GetFactionMode(marker.unit.NetworkHQ) == FactionMode.Enemy ? 6.0f : 3.0f;
    }

    private static void SetTargetArrow(CombatHUD instance, bool enabled, Vector3 position, Vector3 targetPosition, Vector3 up, Component screenSpaceCamera)
    {
        var targetArrow = (Image)TargetArrowField.GetValue(instance);
        var targetArrowTail = (Transform)TargetArrowTailField.GetValue(instance);
        var targetText = (Text)TargetTextField.GetValue(instance);

        targetArrow.enabled = enabled;
        targetText.enabled = enabled;
        targetText.transform.position = targetArrowTail.position;
        targetText.transform.rotation = screenSpaceCamera.transform.rotation;
        if (!enabled)
            return;

        targetArrow.transform.position = position;
        ApplyAngularScale(targetArrow.transform, position);
        ApplyAngularScale(targetText.transform, targetText.transform.position);
        ApplyAngularScale(targetArrowTail, targetArrowTail.position);

        var desiredUp = targetPosition - position;
        if (desiredUp.sqrMagnitude <= Mathf.Epsilon)
            desiredUp = targetArrow.transform.up;
        desiredUp.Normalize();

        var desiredForward = -up;
        if (desiredForward.sqrMagnitude <= Mathf.Epsilon)
            desiredForward = APIBus.CockpitHudCamera.transform.forward;

        desiredForward = Vector3.ProjectOnPlane(desiredForward, desiredUp);
        if (desiredForward.sqrMagnitude <= Mathf.Epsilon)
            desiredForward = Vector3.ProjectOnPlane(APIBus.CockpitHudCamera.transform.forward, desiredUp);
        if (desiredForward.sqrMagnitude <= Mathf.Epsilon)
            desiredForward = Vector3.Cross(desiredUp, APIBus.CockpitHudCamera.transform.right);

        targetArrow.transform.rotation = Quaternion.LookRotation(desiredForward.normalized, desiredUp);
    }

    private static void ApplyAngularScale(Transform transform, Vector3 worldPosition)
    {
        var hudCamera = APIBus.CockpitHudCamera;
        if (hudCamera == null || transform == null)
            return;

        var depth = Vector3.Dot(worldPosition - hudCamera.transform.position, hudCamera.transform.forward);
        if (depth <= Mathf.Epsilon)
            depth = VrHudProjectionHelper.HudDistance;

        var scaleFactor = depth / VrHudProjectionHelper.HudDistance;
        transform.localScale = GetBaseLocalScale(transform) * scaleFactor;
    }

    private static Vector3 GetBaseLocalScale(Component component)
    {
        var instanceId = component.GetInstanceID();
        if (BaseLocalScales.TryGetValue(instanceId, out var baseScale))
            return baseScale;

        baseScale = component.transform.localScale;
        BaseLocalScales[instanceId] = baseScale;
        return baseScale;
    }
}
