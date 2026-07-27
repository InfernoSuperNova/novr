using HarmonyLib;
using System.Reflection;
using NuclearOption.UIStyleSystem;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;


// Ensures our hud markers are in our VR UI camera's space
internal static class HUDUnitMarkerViewPositionPatch
{ 
    private static readonly FieldInfo HiddenField = AccessTools.Field(typeof(global::HUDUnitMarker), "hidden");
    private static readonly FieldInfo TransformField = AccessTools.Field(typeof(global::HUDUnitMarker), "_transform");
    private static readonly FieldInfo IconField = AccessTools.Field(typeof(global::HUDUnitMarker), "icon");
    private static readonly FieldInfo TimeCreatedField = AccessTools.Field(typeof(global::HUDUnitMarker), "timeCreated");
    private static readonly FieldInfo ColorField = AccessTools.Field(typeof(global::HUDUnitMarker), "color");
    private static readonly FieldInfo FlashingField = AccessTools.Field(typeof(global::HUDUnitMarker), "flashing");
    private static readonly FieldInfo TargetArrowField = AccessTools.Field(typeof(global::CombatHUD), "targetArrow");
    private static readonly FieldInfo TargetArrowTailField = AccessTools.Field(typeof(global::CombatHUD), "targetArrowTail");
    private static readonly FieldInfo TargetTextField = AccessTools.Field(typeof(global::CombatHUD), "targetText");
    private static readonly FieldInfo TargetInfoField = AccessTools.Field(typeof(global::CombatHUD), "targetInfo");
    private static bool GetHidden(global::HUDUnitMarker marker) => (bool)HiddenField.GetValue(marker);
    private static Transform GetTransform(global::HUDUnitMarker marker) => (Transform)TransformField.GetValue(marker);
    private static Sprite GetIcon(global::HUDUnitMarker marker) => (Sprite)IconField.GetValue(marker);
    private static float GetTimeCreated(global::HUDUnitMarker marker) => (float)TimeCreatedField.GetValue(marker);
    private static Color GetColor(global::HUDUnitMarker marker) => (Color)ColorField.GetValue(marker);
    private static bool GetFlashing(global::HUDUnitMarker marker) => (bool)FlashingField.GetValue(marker);

    [HarmonyPatch(typeof(global::HUDUnitMarker), nameof(global::HUDUnitMarker.UpdatePosition))]
    private static class UpdatePositionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::HUDUnitMarker __instance, FactionHQ hq, global::GlobalPosition viewPosition, Vector3 cameraForward)
        {
            var mainCamera = APIBus.MainCamera;
            var cockpitHudCamera = APIBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null)
                return true;

            var markerTransform = GetTransform(__instance);
            markerTransform.rotation = cockpitHudCamera.transform.rotation;

            var targetInfo = TargetInfoField.GetValue(SceneSingleton<CombatHUD>.i) as Component;
            if (targetInfo != null)
                targetInfo.transform.rotation = cockpitHudCamera.transform.rotation;

            if (GetHidden(__instance))
                return false;

            GlobalPosition knownPosition = __instance.unit.GlobalPosition();
            if (__instance.outdated && !hq.TryGetKnownPosition(__instance.unit, out knownPosition))
                return false;

            var knownWorldPosition = knownPosition.ToLocalPosition();
            if (__instance.selected)
            {
                if (VrHudProjection.PinToScreenEdge(knownWorldPosition, out var rayToScreen, out _))
                {
                    __instance.image.enabled = false;
                    if (VrHudProjection.TryProjectDirectionToCockpitHud(knownWorldPosition, out var targetHudPosition))
                        SetTargetArrow(SceneSingleton<CombatHUD>.i, true, rayToScreen, targetHudPosition,
                            -cockpitHudCamera.transform.forward, cockpitHudCamera);
                }
                else
                {
                    __instance.image.enabled = true;
                    if (VrHudProjection.TryProjectToCockpitHud(knownWorldPosition, out var targetHudPosition))
                        markerTransform.position = targetHudPosition;
                    SetTargetArrow(SceneSingleton<CombatHUD>.i, false, Vector3.zero, Vector3.zero, Vector3.zero,
                        cockpitHudCamera);
                }

                if (!__instance.unit.HasRadarEmission())
                  return false;

                if (__instance.unit.radar is Radar radar && radar.IsJammed())
                {
                    if (__instance.image.sprite == GameAssets.i.targetUnitSpriteJammed)
                        return false;
                    __instance.image.sprite = GameAssets.i.targetUnitSpriteJammed;
                }
                else
                {
                    if (__instance.image.sprite != GameAssets.i.targetUnitSpriteJammed)
                        return false;
                    __instance.image.sprite = DynamicMap.GetFactionMode(__instance.unit.NetworkHQ) == FactionMode.Friendly
                        ? GameAssets.i.targetUnitSpriteFriendly
                        : GetIcon(__instance);
                }
            }
            else if (Vector3.Dot(knownWorldPosition - mainCamera.transform.position, mainCamera.transform.forward) < 0.0f)
            {
                if (!__instance.image.enabled)
                    return false;
                __instance.image.enabled = false;
            }
            else
            {
                if (!__instance.image.enabled)
                    __instance.image.enabled = true;
                if (VrHudProjection.TryProjectToCockpitHud(knownWorldPosition, out var targetHudPosition))
                    markerTransform.position = targetHudPosition;

                if (__instance.fresh)
                {
                    var markerColor = GetColor(__instance);
                    var warningColor = ThemeManager.Active.ColorTheme.Warning;
                    var t = Time.timeSinceLevelLoad - GetTimeCreated(__instance);
                    __instance.image.color = Color.Lerp(markerColor + warningColor, markerColor, t);
                    if (t > 1.0f)
                        __instance.fresh = false;
                }

                if (!GetFlashing(__instance))
                    return false;
                var flashingColor = GetColor(__instance);
                var flashingWarningColor = ThemeManager.Active.ColorTheme.Warning;
                __instance.image.color = Color.Lerp(
                    flashingColor + flashingWarningColor,
                    flashingColor,
                    Mathf.Sin(Time.timeSinceLevelLoad * 20f) + 0.5f);
            }

            return false;
        }


        
        private static void SetTargetArrow(global::CombatHUD instance, bool enabled, Vector3 position, Vector3 targetPosition, Vector3 up, Component screenSpaceCamera)
        {
            var targetArrow = (Image)TargetArrowField.GetValue(instance);
            var targetArrowTail = (Transform)TargetArrowTailField.GetValue(instance);
            var targetText = (Text)TargetTextField.GetValue(instance);

            targetArrow.enabled = enabled;
            targetText.enabled = enabled;
            targetText.transform.rotation = screenSpaceCamera.transform.rotation;
            if (!enabled)
                return;

            targetArrow.transform.position = position;
            targetText.transform.position = targetArrowTail.position;
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
    }
    
}
