using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

namespace NOVR.VrUi.HarmonyPatches;

internal static class DynamicMapVrCursorPatch
{
    private static bool IsSelectableMapIcon(global::MapIcon? mapIcon)
    {
        if (mapIcon == null ||
            !mapIcon.gameObject.activeInHierarchy ||
            mapIcon.iconImage == null ||
            !mapIcon.iconImage.raycastTarget)
        {
            return false;
        }

        if (mapIcon is global::UnitMapIcon unitMapIcon)
        {
            if (unitMapIcon.unit == null) return false;
            
            if (global::SceneSingleton<global::TargetListSelector>.i != null &&
                global::SceneSingleton<global::TargetListSelector>.i.CheckExclusions(unitMapIcon.unit))
            {
                return false;
            }
        }

        return true;
    }

    [HarmonyPatch(typeof(global::DynamicMap), "SelectFromMap")]
    private static class SelectFromMapPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::DynamicMap __instance)
        {
            var cursor = VrUiCursor.I;
            if (cursor != null && cursor.IsActive)
            {
                var camera = APIBus.CockpitHudCamera;
                if (camera == null) return true;

                // Calculate screen point exactly in the VR camera's screen/viewport space
                // to match the coordinate system of iconWorldPositions projected via the same camera.
                var cursorScreenPoint = (Vector2)camera.WorldToScreenPoint(cursor.CursorPosition);

                // 1. Try EventSystem raycast first (exact hit test)
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    var pointerEventData = new PointerEventData(eventSystem)
                    {
                        position = cursorScreenPoint
                    };
                    var results = new List<RaycastResult>();
                    eventSystem.RaycastAll(pointerEventData, results);
                    foreach (var result in results)
                    {
                        var icon = result.gameObject.GetComponentInParent<global::MapIcon>();
                        if (IsSelectableMapIcon(icon))
                        {
                            icon.ClickIcon(global::MapIcon.ClickSource.Mouse);
                            return false;
                        }
                    }
                }

                // 2. Fallback: Find closest selectable map icon in screen space
                var icons = UnityEngine.Object.FindObjectsOfType<global::MapIcon>();
                global::MapIcon? closestIcon = null;
                float closestSqrDistance = float.MaxValue;
                
                foreach (var icon in icons)
                {
                    if (!IsSelectableMapIcon(icon)) continue;
                    
                    Vector3 iconWorldPosition = icon.transform.position;
                    Vector2 iconScreenPoint = camera.WorldToScreenPoint(iconWorldPosition);
                    
                    float sqrDistance = (iconScreenPoint - cursorScreenPoint).sqrMagnitude;
                    if (sqrDistance < closestSqrDistance)
                    {
                        closestSqrDistance = sqrDistance;
                        closestIcon = icon;
                    }
                }
                
                if (closestIcon != null && closestSqrDistance <= 10000f)
                {
                    closestIcon.ClickIcon(global::MapIcon.ClickSource.Mouse);
                }
                
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(global::DynamicMap), "IsCursorInMapRectangle")]
    private static class IsCursorInMapRectanglePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::DynamicMap __instance, ref bool __result)
        {
            var cursor = VrUiCursor.I;
            if (cursor != null && cursor.IsActive)
            {
                var camera = APIBus.CockpitHudCamera;
                if (camera == null) return true;

                var screenPoint = cursor.GetScreenPoint();
                __result = RectTransformUtility.RectangleContainsScreenPoint(
                    __instance.mapBackground.rectTransform,
                    screenPoint,
                    camera
                );
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(global::DynamicMap), "GetCursorCoordinates")]
    private static class GetCursorCoordinatesPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::DynamicMap __instance, ref global::GlobalPosition __result)
        {
            var cursor = VrUiCursor.I;
            if (cursor != null && cursor.IsActive)
            {
                var camera = APIBus.CockpitHudCamera;
                if (camera == null) return true;

                var screenPoint = cursor.GetScreenPoint();
                var mapImageRect = __instance.mapImage.GetComponent<RectTransform>();
                
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapImageRect,
                    screenPoint,
                    camera,
                    out var localPoint
                );
                
                float scaleFactor = __instance.mapDimension / 900.0f;
                Vector2 worldCoords = localPoint * scaleFactor;
                
                __result = new global::GlobalPosition(worldCoords.x, 0f, worldCoords.y);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(global::MapWaypoint), MethodType.Constructor, new Type[] { typeof(Vector3), typeof(Vector3), typeof(GameObject), typeof(GameObject) })]
    private static class MapWaypointConstructorPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref Vector3 __0)
        {
            var cursor = VrUiCursor.I;
            if (cursor != null && cursor.IsActive)
            {
                __0 = cursor.CursorPosition;
            }
        }
    }
}
