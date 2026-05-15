using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class DynamicMapRotationSafePatch
{
    private const float MinimumMapImageAlpha = 1.0f;
    private const float MapBrightnessMultiplier = 2.0f;
    private static readonly FieldInfo MapBackgroundField = AccessTools.Field(typeof(global::DynamicMap), "mapBackground");
    private static readonly FieldInfo MapTargetField = AccessTools.Field(typeof(global::DynamicMap), "mapTarget");
    private static readonly FieldInfo IsJumpingField = AccessTools.Field(typeof(global::DynamicMap), "isJumping");

    [HarmonyPatch(typeof(global::DynamicMap), "CenterMap")]
    private static class CenterMapPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::DynamicMap __instance)
        {
            var cameraPosition = SceneSingleton<CameraStateManager>.i.transform.GlobalPosition().AsVector3() * __instance.mapDisplayFactor;
            __instance.mapImage.transform.localPosition = MapOffsetToLocalPosition(__instance, cameraPosition);
            return false;
        }
    }

    [HarmonyPatch(typeof(global::DynamicMap), "CenterMinimizedMap")]
    private static class CenterMinimizedMapPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::DynamicMap __instance)
        {
            var combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null || combatHud.aircraft == null)
                return false;

            var cameraPosition = SceneSingleton<CameraStateManager>.i.transform.position.ToGlobalPosition().AsVector3() * __instance.mapDisplayFactor;
            var aircraftForward = combatHud.aircraft.transform.forward with { y = 0.0f };
            var viewForward = EventBus.MainCamera != null
                ? EventBus.MainCamera.transform.forward
                : SceneSingleton<CameraStateManager>.i.transform.forward;
            viewForward.y = 0.0f;
            if (viewForward.sqrMagnitude <= Mathf.Epsilon)
                viewForward = aircraftForward;
            var mapFocusPosition = cameraPosition + aircraftForward.normalized * __instance.mapDisplayFactor * 4000.0f;

            var viewYaw = Quaternion.LookRotation(viewForward.normalized, Vector3.up).eulerAngles.y;
            __instance.mapImage.transform.localEulerAngles = new Vector3(0.0f, 0.0f, combatHud.aircraft.transform.eulerAngles.y);
            __instance.mapImage.transform.localPosition = MapOffsetToLocalPosition(__instance, mapFocusPosition);
            __instance.viewIndicator.transform.localPosition = new Vector3(cameraPosition.x, cameraPosition.z, 0.0f);
            __instance.viewIndicator.transform.eulerAngles = new Vector3(
                0.0f,
                0.0f,
                __instance.mapImage.transform.eulerAngles.z - viewYaw);
            return false;
        }
    }

    [HarmonyPatch(typeof(global::DynamicMap), "JumptoTarget")]
    private static class JumpToTargetPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::DynamicMap __instance)
        {
            var currentPosition = __instance.mapImage.transform.localPosition;
            var targetPosition = MapOffsetToLocalPosition(__instance, (Vector3)MapTargetField.GetValue(__instance));
            __instance.mapImage.transform.localPosition = Vector3.Lerp(currentPosition, targetPosition, 0.05f);
            if (Vector3.Distance(currentPosition, targetPosition) < 0.1f)
            {
                MapTargetField.SetValue(__instance, Vector3.zero);
                IsJumpingField.SetValue(__instance, false);
            }

            return false;
        }
    }
    
    [HarmonyPatch(typeof(global::DynamicMap), "Update")]
    private static class UpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::DynamicMap __instance)
        {
            var mapImage = __instance.mapImage.GetComponent<Image>();
            if (mapImage == null)
                return;
            
            var color = mapImage.color;
            color.r = Mathf.Clamp01(color.r * MapBrightnessMultiplier);
            color.g = Mathf.Clamp01(color.g * MapBrightnessMultiplier);
            color.b = Mathf.Clamp01(color.b * MapBrightnessMultiplier);
            color.a = Mathf.Max(color.a, MinimumMapImageAlpha);
            mapImage.color = color;
        }
    }

    internal static Vector3 MapOffsetToLocalPosition(global::DynamicMap map, Vector3 mapPosition)
    {
        var scale = map.mapImage.transform.localScale.x * GetMapBackgroundTransform(map).localScale.x;
        return Quaternion.Euler(0.0f, 0.0f, map.mapImage.transform.localEulerAngles.z) *
               new Vector3(-mapPosition.x, -mapPosition.z, 0.0f) *
               scale;
    }

    internal static float LocalSignedAngle(Vector3 fromLocalPosition, Vector3 toLocalPosition)
    {
        var delta = toLocalPosition - fromLocalPosition;
        return -Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
    }

    internal static Transform GetMapBackgroundTransform(global::DynamicMap map)
    {
        return ((UnityEngine.UI.Image)MapBackgroundField.GetValue(map)).transform;
    }

}
