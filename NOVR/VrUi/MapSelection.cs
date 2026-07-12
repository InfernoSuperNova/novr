using UnityEngine;

namespace NOVR.VrUi;

internal static class MapSelection
{
    internal static bool IsValidatedClickInProgress { get; private set; }

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

        var icons = Object.FindObjectsOfType<global::MapIcon>();
        global::MapIcon? closest = null;
        var closestSquared = float.MaxValue;

        foreach (var icon in icons)
        {
            if (icon == null || !icon.gameObject.activeInHierarchy)
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
}
