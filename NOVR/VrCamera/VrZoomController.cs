using UnityEngine;

namespace NOVR.VrCamera;

/// <summary>
/// Applies binocular-style magnification to the headset's native per-eye projections.
/// </summary>
internal sealed class VrZoomController : NOVRBehaviour
{
    private const float MinimumMagnification = 1f;
    private const float MaximumSupportedMagnification = 10f;
    private const float AxisDeadzone = 0.001f;

    private static float _magnification = MinimumMagnification;

    private Camera? _camera;

    protected override void Awake()
    {
        base.Awake();
        _camera = GetComponent<Camera>();

        Debug.Log(
            $"[NOVR.Zoom] Stereo zoom ready (speed {ModConfiguration.Instance.ZoomSpeed.Value:0.##}x/s, " +
            $"maximum {ModConfiguration.Instance.MaximumZoom.Value:0.##}x, " +
            $"instant zoom out {ModConfiguration.Instance.InstantZoomOut.Value}).");
    }

    protected override void OnBeforeRender()
    {
        base.OnBeforeRender();

        if (_camera == null || !_camera.enabled)
        {
            return;
        }

        // Always start from the OpenXR-provided asymmetric eye projections so zoom does not
        // accumulate from one frame to the next and returning to 1x restores the native view.
        _camera.ResetStereoProjectionMatrices();

        var magnification = Mathf.Clamp(
            _magnification,
            MinimumMagnification,
            MaximumSupportedMagnification);

        if (magnification <= MinimumMagnification)
        {
            return;
        }

        ApplyMagnification(Camera.StereoscopicEye.Left, magnification);
        ApplyMagnification(Camera.StereoscopicEye.Right, magnification);
    }

    protected override void OnDisable()
    {
        if (_camera != null)
        {
            _camera.ResetStereoProjectionMatrices();
        }

        ResetZoom();
        base.OnDisable();
    }

    internal static void UpdateZoomInput(float zoomAxis)
    {
        var configuration = ModConfiguration.Instance;
        var maximumZoom = Mathf.Clamp(
            configuration.MaximumZoom.Value,
            MinimumMagnification,
            MaximumSupportedMagnification);
        var zoomSpeed = Mathf.Clamp(configuration.ZoomSpeed.Value, 0.1f, 20f);
        var axisMagnitude = Mathf.Clamp01(Mathf.Abs(zoomAxis));

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

    private void ApplyMagnification(Camera.StereoscopicEye eye, float magnification)
    {
        if (_camera == null)
        {
            return;
        }

        var projection = _camera.GetStereoProjectionMatrix(eye);

        // Scaling these terms narrows the frustum around each eye's existing optical centre.
        // Leave m02/m12 untouched to preserve the headset's asymmetric lens projection.
        projection.m00 *= magnification;
        projection.m11 *= magnification;

        _camera.SetStereoProjectionMatrix(eye, projection);
    }
}
