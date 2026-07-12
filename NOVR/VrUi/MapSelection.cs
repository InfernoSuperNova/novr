using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi;

internal static class MapSelection
{
    private static readonly List<global::MapIcon> CachedIcons = new();
    private static global::DynamicMap? _cachedMap;
    private static bool _iconCacheInitialized;

    internal static bool IsValidatedClickInProgress { get; private set; }

    internal static void InvalidateIconCache(global::DynamicMap map)
    {
        if (_cachedMap != null && _cachedMap != map)
            return;

        CachedIcons.Clear();
        _cachedMap = map;
        _iconCacheInitialized = false;
    }

    internal static void RegisterIcon(global::MapIcon icon)
    {
        if (!_iconCacheInitialized || icon == null || CachedIcons.Contains(icon))
            return;

        CachedIcons.Add(icon);
    }

    internal static bool TryGetLocalPoint(
        global::DynamicMap map,
        Ray ray,
        out Vector2 localPoint,
        out Vector3 worldPoint)
    {
        localPoint = default;
        worldPoint = default;

        if (map == null || map.mapImage == null)
            return false;

        var mapRect = map.mapImage.GetComponent<RectTransform>();
        if (mapRect == null)
            return false;

        var denominator = Vector3.Dot(mapRect.forward, ray.direction);
        if (Mathf.Abs(denominator) < 0.0001f)
            return false;

        var distance = Vector3.Dot(mapRect.forward, mapRect.position - ray.origin) / denominator;
        if (distance < 0.0f)
            return false;

        worldPoint = ray.GetPoint(distance);
        var localPosition = mapRect.InverseTransformPoint(worldPoint);
        localPoint = new Vector2(localPosition.x, localPosition.y);
        return mapRect.rect.Contains(localPoint);
    }

    internal static bool TrySelectClosestIcon(
        global::DynamicMap map,
        Vector2 mapLocalPoint,
        global::MapIcon.ClickSource clickSource)
    {
        if (!TryFindClosestIcon(map, mapLocalPoint, out var closest) || closest == null)
            return false;

        IsValidatedClickInProgress = true;
        try
        {
            closest.ClickIcon(clickSource);
        }
        finally
        {
            IsValidatedClickInProgress = false;
        }

        return true;
    }

    internal static bool TryFindClosestIcon(
        global::DynamicMap map,
        Vector2 mapLocalPoint,
        out global::MapIcon? closest)
    {
        return TryFindClosestIcon(map, mapLocalPoint, requireSelectable: false, out closest);
    }

    internal static bool TryFindClosestSelectableIcon(
        global::DynamicMap map,
        Vector2 mapLocalPoint,
        out global::MapIcon? closest)
    {
        return TryFindClosestIcon(map, mapLocalPoint, requireSelectable: true, out closest);
    }

    private static bool TryFindClosestIcon(
        global::DynamicMap map,
        Vector2 mapLocalPoint,
        bool requireSelectable,
        out global::MapIcon? closest)
    {
        closest = null;
        if (map == null || map.mapImage == null)
            return false;

        var mapRect = map.mapImage.GetComponent<RectTransform>();
        if (mapRect == null)
            return false;

        var rectSize = mapRect.rect.size;
        if (rectSize.x < 1.0f || rectSize.y < 1.0f)
            return false;

        var pointNormalized = new Vector2(
            mapLocalPoint.x / rectSize.x,
            mapLocalPoint.y / rectSize.y);
        var maxRadius = ModConfiguration.Instance != null
            ? ModConfiguration.Instance.MapClickMaxRadius.Value
            : 0.05f;
        var maxRadiusSquared = maxRadius * maxRadius;

        EnsureIconCache(map);
        var closestSquared = float.MaxValue;

        for (var index = CachedIcons.Count - 1; index >= 0; index--)
        {
            var icon = CachedIcons[index];
            if (icon == null)
            {
                CachedIcons.RemoveAt(index);
                continue;
            }

            if (!icon.gameObject.activeInHierarchy)
                continue;
            if (requireSelectable && !IsSelectable(icon))
                continue;

            var iconLocalPosition = mapRect.InverseTransformPoint(icon.transform.position);
            var iconNormalized = new Vector2(
                iconLocalPosition.x / rectSize.x,
                iconLocalPosition.y / rectSize.y);
            var squaredDistance = (iconNormalized - pointNormalized).sqrMagnitude;
            if (squaredDistance >= closestSquared)
                continue;

            closestSquared = squaredDistance;
            closest = icon;
        }

        if (closest == null || closestSquared > maxRadiusSquared)
        {
            closest = null;
            return false;
        }

        return true;
    }

    private static void EnsureIconCache(global::DynamicMap map)
    {
        if (_cachedMap != map)
        {
            CachedIcons.Clear();
            _cachedMap = map;
            _iconCacheInitialized = false;
        }

        if (_iconCacheInitialized)
            return;

        CachedIcons.AddRange(Object.FindObjectsOfType<global::MapIcon>());
        _iconCacheInitialized = true;
    }

    private static bool IsSelectable(global::MapIcon icon)
    {
        if (icon is global::UnitMapIcon unitIcon)
        {
            var combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud != null && combatHud.aircraft == unitIcon.unit)
                return false;

            var selector = SceneSingleton<TargetListSelector>.i;
            return selector == null || !selector.CheckExclusions(unitIcon.unit);
        }

        if (icon is global::AirbaseMapIcon airbaseIcon)
        {
            var aircraft = SceneSingleton<CombatHUD>.i?.aircraft;
            return (aircraft == null || aircraft.disabled) &&
                   GameManager.gameResolution != GameResolution.Defeat &&
                   airbaseIcon.airbase != null &&
                   airbaseIcon.airbase.AnyHangarsAvailable();
        }

        return true;
    }
}
