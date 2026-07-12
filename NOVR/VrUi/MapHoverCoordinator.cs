namespace NOVR.VrUi;

internal enum MapHoverSource
{
    Cursor,
    Head
}

internal static class MapHoverCoordinator
{
    private static global::MapIcon? _cursorIcon;
    private static global::MapIcon? _headIcon;
    private static MapHoverSource? _owner;

    internal static void Update(
        MapHoverSource source,
        global::DynamicMap map,
        global::MapIcon? icon)
    {
        var previous = GetIcon(source);
        SetIcon(source, icon);

        if (icon != null && icon != previous)
        {
            _owner = source;
            map.DisplayTooltip(icon);
            return;
        }

        if (icon == null && _owner == source)
        {
            var fallbackSource = source == MapHoverSource.Cursor
                ? MapHoverSource.Head
                : MapHoverSource.Cursor;
            var fallback = GetIcon(fallbackSource);
            if (fallback != null)
            {
                _owner = fallbackSource;
                map.DisplayTooltip(fallback);
            }
            else
            {
                _owner = null;
                map.HideTooltip();
            }
            return;
        }

        if (_owner == source && icon != null)
            EnsureTooltipVisible(map, icon);
    }

    internal static void Clear(MapHoverSource source, global::DynamicMap? map)
    {
        if (map != null)
        {
            Update(source, map, null);
            return;
        }

        SetIcon(source, null);
        if (_owner == source)
            _owner = null;
    }

    private static global::MapIcon? GetIcon(MapHoverSource source)
    {
        return source == MapHoverSource.Cursor ? _cursorIcon : _headIcon;
    }

    private static void SetIcon(MapHoverSource source, global::MapIcon? icon)
    {
        if (source == MapHoverSource.Cursor)
            _cursorIcon = icon;
        else
            _headIcon = icon;
    }

    private static void EnsureTooltipVisible(global::DynamicMap map, global::MapIcon icon)
    {
        var tooltip = map.GetComponentInChildren<global::MapToolTip>(true);
        if (tooltip == null || !tooltip.gameObject.activeSelf)
            map.DisplayTooltip(icon);
    }
}
