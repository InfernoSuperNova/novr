using System;
using UnityEngine;

namespace NOVR.VrUi;

internal static class VrUiPointerState
{
    private const int FreshFrameWindow = 2;

    public static bool Active { get; private set; }
    public static Vector2 ScreenPoint { get; private set; }
    public static Vector2 RealMousePoint { get; private set; }
    public static int LastUpdatedFrame { get; private set; } = -1000;

    public static bool HasFreshPointer => Active && Time.frameCount - LastUpdatedFrame <= FreshFrameWindow;

    public static void SetActive(Vector2 screenPoint, Vector2 realMousePoint)
    {
        Active = true;
        ScreenPoint = screenPoint;
        RealMousePoint = realMousePoint;
        LastUpdatedFrame = Time.frameCount;
    }

    public static void SetInactive()
    {
        Active = false;
    }

    public static bool TryGetScreenPoint(out Vector2 screenPoint)
    {
        screenPoint = ScreenPoint;
        return HasFreshPointer;
    }

    public static Camera? GetEventCamera()
    {
        try
        {
            return APIBus.CockpitHudCamera;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
