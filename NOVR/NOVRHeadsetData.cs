using NOVR.UnityTypesHelper;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR;

[DefaultExecutionOrder(-100)]
public class NOVRHeadsetData : NOVRBehaviour
{
    public static Vector3 TranslationAnchor { get; private set; }
    public static Vector3 Translation { get; private set; }
    public static Vector3 TranslationCalibrationOffset { get; private set; }
    public static Vector3 TranslationError => Translation - TranslationCalibrationOffset - TranslationAnchor;

    public static Quaternion Rotation { get; private set; }
    public static Quaternion RotationCalibrationOffset { get; private set; } = Quaternion.identity;
    public static Quaternion RotationError => Quaternion.Inverse(RotationCalibrationOffset) * Rotation;

    public static void SetAnchor(Vector3 anchor)
    {
        TranslationAnchor = anchor;
    }

    public static void CalibrateTranslation(CalibrationAxes calibrationAxes = CalibrationAxes.All, bool overrideExistingInNonCalibratedAxes = false)
    {
        Vector3 currentError = -GetHeadPosition();
        bool ov = overrideExistingInNonCalibratedAxes;
        TranslationCalibrationOffset = new Vector3(
            (calibrationAxes & CalibrationAxes.X) != 0 ? currentError.x : ov ? TranslationCalibrationOffset.x : 0,
            (calibrationAxes & CalibrationAxes.Y) != 0 ? currentError.y : ov ? TranslationCalibrationOffset.y : 0,
            (calibrationAxes & CalibrationAxes.Z) != 0 ? currentError.z : ov ? TranslationCalibrationOffset.z : 0
        ) + Vector3.forward * ModConfiguration.Instance.CockpitHeadForwardOffset.Value
          + Vector3.right * ModConfiguration.Instance.CockpitHeadRightOffset.Value;
    }

    public static void CalibrateRotation(CalibrationAxes calibrationAxes = CalibrationAxes.Yaw, bool overrideExistingInNonCalibratedAxes = false)
    {
        var currentRotation = GetHeadRotation();
        var currentEuler = currentRotation.eulerAngles;
        Vector3 currentError = new(
            -Mathf.DeltaAngle(0f, currentEuler.x),
            -Mathf.DeltaAngle(0f, currentEuler.y),
            -Mathf.DeltaAngle(0f, currentEuler.z)
        );

        bool ov = overrideExistingInNonCalibratedAxes;
        RotationCalibrationOffset = Quaternion.Euler(
            (calibrationAxes & CalibrationAxes.X) != 0 ? currentError.x : ov ? RotationCalibrationOffset.eulerAngles.x : 0,
            (calibrationAxes & CalibrationAxes.Y) != 0 ? currentError.y : ov ? RotationCalibrationOffset.eulerAngles.y : 0,
            (calibrationAxes & CalibrationAxes.Z) != 0 ? currentError.z : ov ? RotationCalibrationOffset.eulerAngles.z : 0
        );
    }

    protected override void Awake()
    {
        base.Awake();
        DisableCameraAutoTracking();
    }

    private void DisableCameraAutoTracking()
    {
        var camera = GetComponent<Camera>();
        if (!camera) return;

        var cameraTrackingDisablingMethod = UuvrXrDevice.XrDeviceType?.GetMethod("DisableAutoXRCameraTracking");

        if (cameraTrackingDisablingMethod != null)
        {
            cameraTrackingDisablingMethod.Invoke(null, new object[] { camera, true });
        }
        else
        {
            // TODO: use alternative method for disabling tracking.
            Debug.LogWarning("Failed to find DisableAutoXRCameraTracking method. Using SetStereoViewMatrix, which also prevents Unity from auto-tracking cameras, but can cause other issues.");
            // TODO: this crashes some games? Example Monster Girl Island. Although that game already comes with VR stuff, dunno if could affect.
            // camera.SetStereoViewMatrix(Camera.StereoscopicEye.Left, camera.worldToCameraMatrix);
            // camera.SetStereoViewMatrix(Camera.StereoscopicEye.Right, camera.worldToCameraMatrix);
        }
    }

    protected override void OnBeforeRender()
    {
        base.OnBeforeRender();
        UpdateTransform();
    }

    private void Update()
    {
        UpdateTransform();
    }

    private void LateUpdate()
    {
        UpdateTransform();
    }

    private void UpdateTransform()
    {
        Translation = TranslationAnchor + TranslationCalibrationOffset + GetHeadPosition();
        Rotation = RotationCalibrationOffset * GetHeadRotation();
    }

    private static Vector3 GetHeadPosition()
    {
        if (TryGetHeadDevice(out var device) &&
            device.TryGetFeatureValue(CommonUsages.devicePosition, out var position))
        {
            return position;
        }

#pragma warning disable CS0618
        return InputTracking.GetLocalPosition(XRNode.Head);
#pragma warning restore CS0618
    }

    private static Quaternion GetHeadRotation()
    {
        if (TryGetHeadDevice(out var device) &&
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
        {
            return rotation;
        }

#pragma warning disable CS0618
        return InputTracking.GetLocalRotation(XRNode.Head);
#pragma warning restore CS0618
    }

    private static bool TryGetHeadDevice(out InputDevice device)
    {
        device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (device.isValid)
        {
            return true;
        }

        device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        return device.isValid;
    }
}
