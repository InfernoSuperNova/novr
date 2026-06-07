using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NOVR.VrUi.HarmonyPatches;

internal static class DynamicMapVrCursorPatch
{
    private const float MaxIconSelectDistanceSqr = 10000f;
    private static readonly FieldInfo IconLookupField = AccessTools.Field(typeof(global::DynamicMap), "iconLookup");
    private static int _lastRectangleMismatchLogFrame = -1000;

    private static bool FixEnabled => ModConfiguration.Instance?.DynamicMapVrCursorFixEnabled.Value ?? true;
    private static bool DiagnosticsEnabled => ModConfiguration.Instance?.DynamicMapVrCursorDiagnosticsEnabled.Value ?? true;

    private static bool TryGetVrPointer(out Vector2 screenPoint)
    {
        screenPoint = default;
        return FixEnabled && VrUiPointerState.TryGetScreenPoint(out screenPoint);
    }

    private static bool IsCursorInMapRectangle(global::DynamicMap map, Vector2 screenPoint)
    {
        if (map == null || map.mapBackground == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            map.mapBackground.rectTransform,
            screenPoint,
            VrUiPointerState.GetEventCamera());
    }

    private static bool TryGetMapLocalPoint(global::DynamicMap map, Vector2 screenPoint, out Vector2 localPoint)
    {
        localPoint = default;
        if (map == null || map.mapImage == null)
        {
            return false;
        }

        var mapImageRect = map.mapImage.transform as RectTransform ?? map.mapImage.GetComponent<RectTransform>();
        return mapImageRect != null &&
               RectTransformUtility.ScreenPointToLocalPointInRectangle(
                   mapImageRect,
                   screenPoint,
                   VrUiPointerState.GetEventCamera(),
                   out localPoint);
    }

    private static bool TryFindRaycastMapIcon(Vector2 screenPoint, out MapIcon mapIcon)
    {
        mapIcon = null;
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        var pointerEventData = new PointerEventData(eventSystem)
        {
            position = screenPoint
        };

        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerEventData, results);
        foreach (var result in results)
        {
            var candidate = result.gameObject.GetComponentInParent<MapIcon>();
            if (IsSelectableMapIcon(candidate))
            {
                mapIcon = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindNearestUnitIcon(global::DynamicMap map, Vector2 screenPoint, out MapIcon mapIcon)
    {
        mapIcon = null;
        if (IconLookupField.GetValue(map) is not Dictionary<Unit, UnitMapIcon> iconLookup)
        {
            return false;
        }

        var eventCamera = VrUiPointerState.GetEventCamera();
        var bestDistance = MaxIconSelectDistanceSqr;
        foreach (var icon in iconLookup.Values)
        {
            if (!IsSelectableMapIcon(icon))
            {
                continue;
            }

            var iconScreenPoint = eventCamera != null
                ? RectTransformUtility.WorldToScreenPoint(eventCamera, icon.transform.position)
                : (Vector2)icon.transform.position;
            var distance = (screenPoint - iconScreenPoint).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                mapIcon = icon;
            }
        }

        return mapIcon != null;
    }

    private static bool IsSelectableMapIcon(MapIcon mapIcon)
    {
        if (mapIcon == null ||
            !mapIcon.gameObject.activeSelf ||
            mapIcon.iconImage == null ||
            !mapIcon.iconImage.raycastTarget)
        {
            return false;
        }

        if (mapIcon is UnitMapIcon unitMapIcon)
        {
            try
            {
                return unitMapIcon.unit != null &&
                       !SceneSingleton<TargetListSelector>.i.CheckExclusions(unitMapIcon.unit);
            }
            catch (Exception)
            {
                return true;
            }
        }

        return true;
    }

    private static void LogMapState(global::DynamicMap map, string reason, Vector2 vrPoint, MapIcon mapIcon = null)
    {
        if (!DiagnosticsEnabled)
        {
            return;
        }

        var realMouse = Input.mousePosition;
        var vrContained = IsCursorInMapRectangle(map, vrPoint);
        var realContained = map != null &&
                            map.mapBackground != null &&
                            RectTransformUtility.RectangleContainsScreenPoint(
                                map.mapBackground.rectTransform,
                                realMouse,
                                null);
        var hitName = mapIcon != null ? mapIcon.name : "<none>";
        Debug.Log(
            $"NOVR DynamicMap cursor: {reason}; vrPoint={vrPoint}; realMouse={realMouse}; " +
            $"vrInMap={vrContained}; realInMap={realContained}; hit={hitName}; " +
            $"screen={Screen.width}x{Screen.height}; pointerFrame={VrUiPointerState.LastUpdatedFrame}; frame={Time.frameCount}");
    }

    [HarmonyPatch(typeof(global::DynamicMap), nameof(global::DynamicMap.IsCursorInMapRectangle))]
    private static class IsCursorInMapRectanglePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::DynamicMap __instance, ref bool __result)
        {
            if (!TryGetVrPointer(out var screenPoint))
            {
                return true;
            }

            __result = IsCursorInMapRectangle(__instance, screenPoint);
            if (DiagnosticsEnabled)
            {
                var realResult = __instance != null &&
                                 __instance.mapBackground != null &&
                                 RectTransformUtility.RectangleContainsScreenPoint(
                                     __instance.mapBackground.rectTransform,
                                     Input.mousePosition,
                                     null);
                if (realResult != __result && Time.frameCount - _lastRectangleMismatchLogFrame > 30)
                {
                    _lastRectangleMismatchLogFrame = Time.frameCount;
                    LogMapState(__instance, "rectangle mismatch", screenPoint);
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(global::DynamicMap), nameof(global::DynamicMap.GetCursorCoordinates))]
    private static class GetCursorCoordinatesPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::DynamicMap __instance, ref GlobalPosition __result)
        {
            if (!TryGetVrPointer(out var screenPoint) ||
                !TryGetMapLocalPoint(__instance, screenPoint, out var localPoint))
            {
                return true;
            }

            var mapMetersPerReferencePixel = __instance.mapDimension / 900f;
            __result = new GlobalPosition(
                localPoint.x * mapMetersPerReferencePixel,
                0f,
                localPoint.y * mapMetersPerReferencePixel);
            return false;
        }
    }

    [HarmonyPatch(typeof(global::DynamicMap), "SelectFromMap")]
    private static class SelectFromMapPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::DynamicMap __instance)
        {
            if (!TryGetVrPointer(out var screenPoint))
            {
                return true;
            }

            if (!IsCursorInMapRectangle(__instance, screenPoint))
            {
                LogMapState(__instance, "select ignored outside map", screenPoint);
                return false;
            }

            if (TryFindRaycastMapIcon(screenPoint, out var raycastIcon) ||
                TryFindNearestUnitIcon(__instance, screenPoint, out raycastIcon))
            {
                LogMapState(__instance, "select clicked icon", screenPoint, raycastIcon);
                raycastIcon.ClickIcon(MapIcon.ClickSource.Controller);
                return false;
            }

            LogMapState(__instance, "select found no icon", screenPoint);
            return false;
        }
    }

    [HarmonyPatch(typeof(global::UnitMapIcon), nameof(global::UnitMapIcon.ClickIcon))]
    private static class UnitMapIconClickDiagnosticsPatch
    {
        [HarmonyPrefix]
        private static void Prefix(global::UnitMapIcon __instance, MapIcon.ClickSource clickSource)
        {
            if (!DiagnosticsEnabled || !VrUiPointerState.TryGetScreenPoint(out var screenPoint))
            {
                return;
            }

            LogMapState(SceneSingleton<DynamicMap>.i, $"unit click source={clickSource}", screenPoint, __instance);
        }
    }
}
