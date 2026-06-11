using System;
using System.Reflection;
using NOVR.UnityTypesHelper;
using UnityEngine;

namespace NOVR;

[DefaultExecutionOrder(-100)]
public class NOVRHeadsetData : NOVRBehaviour
{
    
    
    
    private static MethodInfo? _trackingRotationMethod;
    private static MethodInfo? _trackingPositionMethod;
    private static readonly object[] TrackingMethodArgs = {
        1
    };
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
        Vector3 currentError = -(Vector3)_trackingPositionMethod.Invoke(null, TrackingMethodArgs);
        bool ov  = overrideExistingInNonCalibratedAxes;
        TranslationCalibrationOffset = new Vector3(
            (calibrationAxes & CalibrationAxes.X) != 0 ? currentError.x : ov ? TranslationCalibrationOffset.x : 0,
            (calibrationAxes & CalibrationAxes.Y) != 0 ? currentError.y : ov ? TranslationCalibrationOffset.y : 0,
            (calibrationAxes & CalibrationAxes.Z) != 0 ? currentError.z : ov ? TranslationCalibrationOffset.z : 0
        ) + Vector3.forward * ModConfiguration.Instance.CockpitHeadForwardOffset.Value;
    }


    public static void CalibrateRotation(CalibrationAxes calibrationAxes = CalibrationAxes.Yaw, bool overrideExistingInNonCalibratedAxes = false)
    {
        var currentRotation = (Quaternion)_trackingRotationMethod.Invoke(null, TrackingMethodArgs);
        var currentEuler = currentRotation.eulerAngles;
        Vector3 currentError = new(
            -Mathf.DeltaAngle(0f, currentEuler.x),
            -Mathf.DeltaAngle(0f, currentEuler.y),
            -Mathf.DeltaAngle(0f, currentEuler.z)
        );

        bool ov  = overrideExistingInNonCalibratedAxes;
        RotationCalibrationOffset = Quaternion.Euler(
            (calibrationAxes & CalibrationAxes.X) != 0 ? currentError.x : ov ? RotationCalibrationOffset.eulerAngles.x : 0,
            (calibrationAxes & CalibrationAxes.Y) != 0 ? currentError.y : ov ? RotationCalibrationOffset.eulerAngles.y : 0,
            (calibrationAxes & CalibrationAxes.Z) != 0 ? currentError.z : ov ? RotationCalibrationOffset.eulerAngles.z : 0
        );
    }
    
    
    
    protected override void Awake()
    {
        base.Awake();
        var inputTrackingType = Type.GetType("UnityEngine.XR.InputTracking, UnityEngine.XRModule") ??
                                Type.GetType("UnityEngine.XR.InputTracking, UnityEngine.VRModule") ??
                                Type.GetType("UnityEngine.VR.InputTracking, UnityEngine.VRModule") ??
                                Type.GetType("UnityEngine.VR.InputTracking, UnityEngine");

        _trackingRotationMethod = inputTrackingType?.GetMethod("GetLocalRotation");
        _trackingPositionMethod = inputTrackingType?.GetMethod("GetLocalPosition");

        if (_trackingRotationMethod == null || _trackingPositionMethod == null)
        {
            Debug.LogError("Failed to find InputTracking.GetLocalRotation/GetLocalPosition. Destroying UUVR Pose Driver.");
            Destroy(this);
            return;
        }
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
        if (_trackingRotationMethod != null && _trackingPositionMethod != null)
        {
            Translation = TranslationAnchor + TranslationCalibrationOffset + (Vector3)_trackingPositionMethod.Invoke(null, TrackingMethodArgs);
            Rotation = RotationCalibrationOffset * (Quaternion)_trackingRotationMethod.Invoke(null, TrackingMethodArgs);
        }
    }
}
