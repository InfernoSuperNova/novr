using UnityEngine;

namespace NOVR.VrCamera;

/// <summary>
/// Tracks cockpit zoom input for XRPassZoomPatch.
/// </summary>
internal static class VrZoomController
{
    private const float MinimumMagnification = 1f;
    private const float MaximumSupportedMagnification = 10f;
    private const float AxisDeadzone = 0.05f;

    private static float _magnification = MinimumMagnification;

    internal static float Magnification => _magnification;

    internal static void UpdateZoomInput(float zoomAxis)
    {
        var axisMagnitude = Mathf.Clamp01(Mathf.Abs(zoomAxis));
        if (axisMagnitude <= AxisDeadzone)
        {
            return;
        }

        var configuration = ModConfiguration.Instance;
        var maximumZoom = Mathf.Clamp(
            configuration.MaximumZoom.Value,
            MinimumMagnification,
            MaximumSupportedMagnification);
        var zoomSpeed = Mathf.Clamp(configuration.ZoomSpeed.Value, 0.1f, 20f);

        _magnification = Mathf.Clamp(
            _magnification,
            MinimumMagnification,
            maximumZoom);

        if (zoomAxis > AxisDeadzone)
        {
            _magnification = Mathf.MoveTowards(
                _magnification,
                maximumZoom,
                zoomSpeed * axisMagnitude * Time.unscaledDeltaTime);
        }
        else if (zoomAxis < -AxisDeadzone)
        {
            _magnification = configuration.InstantZoomOut.Value
                ? MinimumMagnification
                : Mathf.MoveTowards(
                    _magnification,
                    MinimumMagnification,
                    zoomSpeed * axisMagnitude * Time.unscaledDeltaTime);
        }
    }

    internal static void ResetZoom()
    {
        _magnification = MinimumMagnification;
    }
}
