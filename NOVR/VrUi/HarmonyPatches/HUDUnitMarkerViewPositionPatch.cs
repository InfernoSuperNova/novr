using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;


// Ensures our hud markers are in our VR UI camera's space
internal static class HUDUnitMarkerViewPositionPatch
{ 
    private static readonly global::GlobalPosition ZeroViewPosition = new(0f, 0f, 0f);
    private const BindingFlags InstanceFieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private const float HudDistance = 1000.0f;
    private const float VrViewportHorizontalDegrees = 50.0f;
    private const float VrViewportVerticalDegrees = 50.0f;
    private const float VrViewportHalfHorizontalDegrees = VrViewportHorizontalDegrees * 0.5f;
    private const float VrViewportHalfVerticalDegrees = VrViewportVerticalDegrees * 0.5f;
    
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
        private static bool Prefix(HUDUnitMarker __instance, FactionHQ hq, ref global::GlobalPosition viewPosition, ref Vector3 cameraForward)
        {
            var mainCamera = EventBus.MainCamera;
            var screenSpaceCamera = EventBus.CockpitHudCamera;

            var realCameraPosition = mainCamera.transform.GlobalPosition();
            var realCameraForward = mainCamera.transform.forward;
            var screenSpacePosition = screenSpaceCamera.transform.GlobalPosition();
            var screenSpaceForward = screenSpaceCamera.transform.forward;

            GetTransform(__instance).rotation = screenSpaceCamera.transform.rotation;
            var targetInfo = (Text)TargetInfoField.GetValue(SceneSingleton<CombatHUD>.i);
            if (targetInfo != null)
            {
                targetInfo.transform.rotation =screenSpaceCamera.transform.rotation;
            }
            
            if (GetHidden(__instance))
                return false;
            GlobalPosition knownPosition = __instance.unit.GlobalPosition();
            if (__instance.outdated && !hq.TryGetKnownPosition(__instance.unit, out knownPosition))
              return false;
            if (__instance.selected)
            {
              if (PinToScreenEdge(knownPosition.ToLocalPosition(), out Vector3 rayToScreen, out float arrowAngle, mainCamera, screenSpaceCamera))
              {
                __instance.image.enabled = false;
                var mainCameraLocal = mainCamera.transform.InverseTransformPoint(knownPosition.ToLocalPosition());
                var hudCameraWorld = screenSpaceCamera.transform.TransformPoint(mainCameraLocal);
                SetTargetArrow(SceneSingleton<CombatHUD>.i, true, rayToScreen, hudCameraWorld, -screenSpaceCamera.transform.forward, screenSpaceCamera);
              }
              else
              {
                __instance.image.enabled = true;
                
                var mainCameraLocal = mainCamera.transform.InverseTransformPoint(knownPosition.ToLocalPosition());
                var hudCameraWorld = screenSpaceCamera.transform.TransformPoint(mainCameraLocal);
                GetTransform(__instance).position = hudCameraWorld.normalized * HudDistance;
                SetTargetArrow(SceneSingleton<CombatHUD>.i, false, Vector3.zero, Vector3.zero, Vector3.zero, screenSpaceCamera);
              }
              if (!__instance.unit.HasRadarEmission())
                return false;
              if ((__instance.unit.radar as Radar).IsJammed())
              {
                if (!((UnityEngine.Object) __instance.image.sprite != (UnityEngine.Object) GameAssets.i.targetUnitSpriteJammed))
                  return false;
                __instance.image.sprite = GameAssets.i.targetUnitSpriteJammed;
              }
              else
              {
                if (!((UnityEngine.Object) __instance.image.sprite == (UnityEngine.Object) GameAssets.i.targetUnitSpriteJammed))
                  return false;
                __instance.image.sprite = DynamicMap.GetFactionMode(__instance.unit.NetworkHQ) == FactionMode.Friendly ? GameAssets.i.targetUnitSpriteFriendly : GetIcon(__instance);
              }
            }
            else if ((double) Vector3.Dot(knownPosition - realCameraPosition, realCameraForward) < 0.0)
            {
              if (!__instance.image.enabled)
                return false;
              __instance.image.enabled = false;
            }
            else
            {
              if (!__instance.image.enabled)
                __instance.image.enabled = true;
              var mainCameraLocal = mainCamera.transform.InverseTransformPoint(knownPosition.ToLocalPosition());
              var hudCameraWorld = screenSpaceCamera.transform.TransformPoint(mainCameraLocal);
              GetTransform(__instance).position = hudCameraWorld.normalized * HudDistance;
              if (__instance.fresh)
              {
                Color markerColor = GetColor(__instance);
                float t = Time.timeSinceLevelLoad - GetTimeCreated(__instance);
                __instance.image.color = Color.Lerp(markerColor + Color.yellow, markerColor, t);
                if ((double) t > 1.0)
                  __instance.fresh = false;
              }
              if (!GetFlashing(__instance))
                return false;
              Color flashingColor = GetColor(__instance);
              __instance.image.color = Color.Lerp(flashingColor + Color.yellow, flashingColor, Mathf.Sin(Time.timeSinceLevelLoad * 20f) + 0.5f);
            }

            return false;
        }


        
        private static bool PinToScreenEdge(Vector3 worldCoords, out Vector3 rayToScreen, out float arrowAngle, Component mainCamera, Component cockpitHudCamera)
        {
            var directionToTarget = worldCoords - mainCamera.transform.position;
            if (directionToTarget.sqrMagnitude <= Mathf.Epsilon)
            {
                rayToScreen = cockpitHudCamera.transform.forward * HudDistance;
                arrowAngle = 0.0f;
                return false;
            }

            var mainCameraLocalDirection = mainCamera.transform.InverseTransformDirection(directionToTarget.normalized);
            var targetYawDegrees = Mathf.Atan2(mainCameraLocalDirection.x, mainCameraLocalDirection.z) * Mathf.Rad2Deg;
            var targetPitchDegrees = Mathf.Atan2(
                mainCameraLocalDirection.y,
                new Vector2(mainCameraLocalDirection.x, mainCameraLocalDirection.z).magnitude) * Mathf.Rad2Deg;

            var horizontalRatio = targetYawDegrees / VrViewportHalfHorizontalDegrees;
            var verticalRatio = targetPitchDegrees / VrViewportHalfVerticalDegrees;
            var ellipseDistance = Mathf.Sqrt(horizontalRatio * horizontalRatio + verticalRatio * verticalRatio);
            var screenEdge = mainCameraLocalDirection.z <= 0.0f || ellipseDistance > 1.0f;

            var pinnedYawDegrees = targetYawDegrees;
            var pinnedPitchDegrees = targetPitchDegrees;
            if (screenEdge && ellipseDistance > Mathf.Epsilon)
            {
                pinnedYawDegrees /= ellipseDistance;
                pinnedPitchDegrees /= ellipseDistance;
            }

            var pinnedLocalDirection = DirectionFromYawPitch(pinnedYawDegrees, pinnedPitchDegrees);
            rayToScreen = cockpitHudCamera.transform.TransformDirection(pinnedLocalDirection).normalized * HudDistance;
            arrowAngle = Mathf.Atan2(targetPitchDegrees, targetYawDegrees);

            return screenEdge;
        }

        private static Vector3 DirectionFromYawPitch(float yawDegrees, float pitchDegrees)
        {
            var yawRadians = yawDegrees * Mathf.Deg2Rad;
            var pitchRadians = pitchDegrees * Mathf.Deg2Rad;
            var pitchCosine = Mathf.Cos(pitchRadians);

            return new Vector3(
                Mathf.Sin(yawRadians) * pitchCosine,
                Mathf.Sin(pitchRadians),
                Mathf.Cos(yawRadians) * pitchCosine);
        }
        
        
        private static void SetTargetArrow(global::CombatHUD instance, bool enabled, Vector3 position, Vector3 targetPosition, Vector3 up, Component screenSpaceCamera)
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
                desiredForward = EventBus.CockpitHudCamera.transform.forward;

            desiredForward = Vector3.ProjectOnPlane(desiredForward, desiredUp);
            if (desiredForward.sqrMagnitude <= Mathf.Epsilon)
                desiredForward = Vector3.ProjectOnPlane(EventBus.CockpitHudCamera.transform.forward, desiredUp);
            if (desiredForward.sqrMagnitude <= Mathf.Epsilon)
                desiredForward = Vector3.Cross(desiredUp, EventBus.CockpitHudCamera.transform.right);

            targetArrow.transform.rotation = Quaternion.LookRotation(desiredForward.normalized, desiredUp);
        }
    }
    
}
