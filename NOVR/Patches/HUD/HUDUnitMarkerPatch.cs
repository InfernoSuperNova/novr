using System.Reflection;
using HarmonyLib;
using NOVR.PatchHelper;
using NOVR.VrUi.HarmonyPatches;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.HUD;


// Ensures our hud markers are in our VR UI camera's space
internal static class HUDUnitMarkerPatch
{ 
    private static readonly FieldInfo HiddenField = AccessTools.Field(typeof(HUDUnitMarker), "hidden");
    private static readonly FieldInfo TransformField = AccessTools.Field(typeof(HUDUnitMarker), "_transform");
    private static readonly FieldInfo IconField = AccessTools.Field(typeof(HUDUnitMarker), "icon");
    private static readonly FieldInfo TimeCreatedField = AccessTools.Field(typeof(HUDUnitMarker), "timeCreated");
    private static readonly FieldInfo ColorField = AccessTools.Field(typeof(HUDUnitMarker), "color");
    private static readonly FieldInfo FlashingField = AccessTools.Field(typeof(HUDUnitMarker), "flashing");
    private static readonly FieldInfo TargetArrowField = AccessTools.Field(typeof(CombatHUD), "targetArrow");
    private static readonly FieldInfo TargetArrowTailField = AccessTools.Field(typeof(CombatHUD), "targetArrowTail");
    private static readonly FieldInfo TargetTextField = AccessTools.Field(typeof(CombatHUD), "targetText");
    private static readonly FieldInfo TargetInfoField = AccessTools.Field(typeof(CombatHUD), "targetInfo");
    private static bool GetHidden(HUDUnitMarker marker) => (bool)HiddenField.GetValue(marker);
    private static Transform GetTransform(HUDUnitMarker marker) => (Transform)TransformField.GetValue(marker);
    private static Sprite GetIcon(HUDUnitMarker marker) => (Sprite)IconField.GetValue(marker);
    private static float GetTimeCreated(HUDUnitMarker marker) => (float)TimeCreatedField.GetValue(marker);
    private static Color GetColor(HUDUnitMarker marker) => (Color)ColorField.GetValue(marker);
    private static bool GetFlashing(HUDUnitMarker marker) => (bool)FlashingField.GetValue(marker);

    [HarmonyPatch(typeof(HUDUnitMarker), nameof(HUDUnitMarker.UpdatePosition))]
    
    
    [PatchPrefix(typeof(HUDUnitMarker), nameof(HUDUnitMarker.UpdatePosition))]
    private static bool UpdatePosition(HUDUnitMarker __instance, FactionHQ hq, ref GlobalPosition viewPosition, ref Vector3 cameraForward)
    {
        var mainCamera = APIBus.MainCamera;
        var screenSpaceCamera = APIBus.MainCamera;

        var realCameraPosition = mainCamera.transform.GlobalPosition();
        var realCameraForward = mainCamera.transform.forward;
        GetTransform(__instance).rotation = screenSpaceCamera.transform.rotation;
        
        
        
        
        var targetInfo = (Text)TargetInfoField.GetValue(SceneSingleton<CombatHUD>.i);
        if (targetInfo != null)
        {
            targetInfo.transform.rotation = screenSpaceCamera.transform.rotation;
        }
        
        if (GetHidden(__instance))
            return false;
        GlobalPosition knownPosition = __instance.unit.GlobalPosition();
        if (__instance.outdated && !hq.TryGetKnownPosition(__instance.unit, out knownPosition))
          return false;
        if (__instance.selected)
        {
          if (VrHudProjectionHelper.PinToScreenEdge(knownPosition.ToLocalPosition(), out Vector3 rayToScreen, out _))
          {
            __instance.image.enabled = false;
            if (VrHudProjectionHelper.TryProjectDirectionToCockpitHud(knownPosition.ToLocalPosition(), out var targetHudPosition))
              SetTargetArrow(SceneSingleton<CombatHUD>.i, true, rayToScreen, targetHudPosition, -screenSpaceCamera.transform.forward, screenSpaceCamera);
          }
          else
          {
            __instance.image.enabled = true;
            
            if (VrHudProjectionHelper.TryProjectToCockpitHud(knownPosition.ToLocalPosition(), out var targetHudPosition))
              GetTransform(__instance).position = targetHudPosition;
            SetTargetArrow(SceneSingleton<CombatHUD>.i, false, Vector3.zero, Vector3.zero, Vector3.zero, screenSpaceCamera);
          }
          if (!__instance.unit.HasRadarEmission())
            return false;
          if ((__instance.unit.radar as Radar).IsJammed())
          {
            if (!(__instance.image.sprite != GameAssets.i.targetUnitSpriteJammed))
              return false;
            __instance.image.sprite = GameAssets.i.targetUnitSpriteJammed;
          }
          else
          {
            if (!(__instance.image.sprite == GameAssets.i.targetUnitSpriteJammed))
              return false;
            __instance.image.sprite = DynamicMap.GetFactionMode(__instance.unit.NetworkHQ) == FactionMode.Friendly ? GameAssets.i.targetUnitSpriteFriendly : GetIcon(__instance);
          }
        }
        else if (Vector3.Dot(knownPosition - realCameraPosition, realCameraForward) < 0.0)
        {
          if (!__instance.image.enabled)
            return false;
          __instance.image.enabled = false;
        }
        else
        {
          if (!__instance.image.enabled)
            __instance.image.enabled = true;
          if (VrHudProjectionHelper.TryProjectToCockpitHud(knownPosition.ToLocalPosition(), out var targetHudPosition))
            GetTransform(__instance).position = targetHudPosition;
          if (__instance.fresh)
          {
            Color markerColor = GetColor(__instance);
            float t = Time.timeSinceLevelLoad - GetTimeCreated(__instance);
            __instance.image.color = Color.Lerp(markerColor + Color.yellow, markerColor, t);
            if (t > 1.0)
              __instance.fresh = false;
          }
          if (!GetFlashing(__instance))
            return false;
          Color flashingColor = GetColor(__instance);
          __instance.image.color = Color.Lerp(flashingColor + Color.yellow, flashingColor, Mathf.Sin(Time.timeSinceLevelLoad * 20f) + 0.5f);
        }

        return false;
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
      var desiredUp = targetPosition - position;
      if (desiredUp.sqrMagnitude <= Mathf.Epsilon)
        desiredUp = targetArrow.transform.up;
      desiredUp.Normalize();

      var desiredForward = -up;
      if (desiredForward.sqrMagnitude <= Mathf.Epsilon)
        desiredForward = APIBus.MainCamera.transform.forward;

      desiredForward = Vector3.ProjectOnPlane(desiredForward, desiredUp);
      if (desiredForward.sqrMagnitude <= Mathf.Epsilon)
        desiredForward = Vector3.ProjectOnPlane(APIBus.MainCamera.transform.forward, desiredUp);
      if (desiredForward.sqrMagnitude <= Mathf.Epsilon)
        desiredForward = Vector3.Cross(desiredUp, APIBus.MainCamera.transform.right);

      targetArrow.transform.rotation = Quaternion.LookRotation(desiredForward.normalized, desiredUp);
    }
}
