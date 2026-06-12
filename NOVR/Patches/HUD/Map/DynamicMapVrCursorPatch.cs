using System;
using System.Collections.Generic;
using HarmonyLib;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NOVR.Patches.HUD.Map;

// Harmony patches adapting tactical map interactions, cursor raycasting, selection,
// and waypoint line/marker formatting to work with the 3D VR laser pointer in cockpit space.
internal static class DynamicMapVrCursorPatch
{
    // Checks if a map icon is valid, active, and eligible for selection.
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

    // Patches map item selection to use VR cursor positions and camera space raycasting.
    [HarmonyPatch(typeof(global::DynamicMap), "SelectFromMap")]
    private static class SelectFromMapPatch
    {
        // Attempts an EventSystem raycast at the VR cursor's screen point,
        // falling back to clicking the closest icon within a screen distance threshold.
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

    // Patches map boundary checking to project VR cursor onto the map background RectTransform.
    [HarmonyPatch(typeof(global::DynamicMap), "IsCursorInMapRectangle")]
    private static class IsCursorInMapRectanglePatch
    {
        // Overrides boundary checks using the cockpit HUD camera.
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

    // Patches coordinates calculations to convert VR pointer position into global 2D simulation coordinates.
    [HarmonyPatch(typeof(global::DynamicMap), "GetCursorCoordinates")]
    private static class GetCursorCoordinatesPatch
    {
        // Projects the VR screen cursor position onto the map RawImage rectangle to get coordinates.
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

    // Patches the MapWaypoint constructor to override the destination position for new waypoints in VR.
    [HarmonyPatch(typeof(global::MapWaypoint), MethodType.Constructor, new Type[] { typeof(Vector3), typeof(Vector3), typeof(GameObject), typeof(GameObject) })]
    private static class MapWaypointConstructorPatch
    {
        // Projects the VR laser screen coordinates flat onto the map RawImage plane instead of floating in 3D.
        [HarmonyPrefix]
        private static void Prefix(ref Vector3 __0)
        {
            var cursor = VrUiCursor.I;
            var dynamicMap = SceneSingleton<global::DynamicMap>.i;
            var camera = APIBus.CockpitHudCamera;

            if (cursor != null && dynamicMap != null && camera != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dynamicMap.mapImage.GetComponent<RectTransform>(),
                    cursor.GetScreenPoint(),
                    camera,
                    out var localPoint
                );
                __0 = dynamicMap.mapImage.transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0f));
            }
        }
    }

    // Aligns, scales, and rotates the waypoint marker and connecting line flat on the map canvas.
    // Handles the scale axis correction (stretching along Y-axis instead of X) and flattens local Z.
    private static void AlignWaypoint(
        GameObject marker,
        GameObject vector,
        ref Vector3 previousWaypoint,
        float scale,
        bool updateRotation)
    {
        marker.transform.localScale = Vector3.one * scale;
        
        // Flatten the previous waypoint local coordinate relative to the map canvas plane
        previousWaypoint = new Vector3(previousWaypoint.x, previousWaypoint.y, 0f);

        Vector3 localMarkerPos = marker.transform.localPosition;
        Vector3 delta = localMarkerPos - previousWaypoint;
        delta.z = 0f;

        if (updateRotation)
        {
            // Calculate the 2D direction angle flat on the rotated canvas plane
            float angle = -Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg + 180f;
            vector.transform.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        // Scale the line vector. Y-axis is length/magnitude, X and Z are width (4f * scale)
        vector.transform.localScale = new Vector3(4f * scale, delta.magnitude, 4f * scale);
    }

    // Patches initial marker placement to position waypoints flat on the canvas.
    [HarmonyPatch(typeof(global::MapWaypoint), "PlaceMarker")]
    private static class MapWaypointPlaceMarkerPatch
    {
        // Intercepts placement to flatten coordinates to the local Z plane and apply VR scale/alignment.
        [HarmonyPrefix]
        private static bool Prefix(
            global::MapWaypoint __instance,
            ref Vector3 ___waypointPosition,
            ref Vector3 ___previousWaypoint,
            ref GameObject ___marker,
            ref GameObject ___vector)
        {
            if (VrUiCursor.I != null)
            {
                var dynamicMap = SceneSingleton<global::DynamicMap>.i;
                if (dynamicMap == null) return true;

                // Flatten waypoint position on the map's iconLayer local plane (Z = 0)
                var iconLayer = dynamicMap.iconLayer.transform;
                Vector3 localWaypoint = iconLayer.InverseTransformPoint(___waypointPosition);
                localWaypoint.z = 0f;
                Vector3 flatWaypointPosition = iconLayer.TransformPoint(localWaypoint);

                ___waypointPosition = flatWaypointPosition;
                ___marker.transform.position = flatWaypointPosition;
                ___vector.transform.position = flatWaypointPosition;

                float scale = 1f / dynamicMap.mapImage.transform.localScale.x;
                AlignWaypoint(___marker, ___vector, ref ___previousWaypoint, scale, updateRotation: true);
                return false;
            }
            return true;
        }
    }

    // Patches marker updates during map zooming or panning in VR.
    [HarmonyPatch(typeof(global::MapWaypoint), "UpdateMarker")]
    private static class MapWaypointUpdateMarkerPatch
    {
        // Intercepts update to keep the marker and vector scales from warping on map pan/zoom.
        [HarmonyPrefix]
        private static bool Prefix(
            global::MapWaypoint __instance,
            ref Vector3 ___waypointPosition,
            ref Vector3 ___previousWaypoint,
            ref GameObject ___marker,
            ref GameObject ___vector)
        {
            if (VrUiCursor.I != null)
            {
                var dynamicMap = SceneSingleton<global::DynamicMap>.i;
                if (dynamicMap == null || ___marker == null || ___vector == null) return true;

                float scale = 1f / dynamicMap.mapImage.transform.localScale.x;
                AlignWaypoint(___marker, ___vector, ref ___previousWaypoint, scale, updateRotation: false);
                return false;
            }
            return true;
        }
    }
}
