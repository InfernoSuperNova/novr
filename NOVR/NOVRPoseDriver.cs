using System;
using System.Reflection;
using NOVR.UnityTypesHelper;
using UnityEngine;

namespace NOVR;

public class NOVRPoseDriver: NOVRBehaviour
{
    private Vector3 _anchor;
    private static Vector3 _calibrate;
    private static MethodInfo? _trackingRotationMethod;
    private static MethodInfo? _trackingPositionMethod;
    private static readonly object[] TrackingMethodArgs = {
        2 // Enum value for XRNode.CenterEye
    };

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

        if (ModConfiguration.Instance.DisableUnityXrCameraAutoTracking.Value)
        {
            DisableCameraAutoTracking();
        }
        else
        {
            Debug.Log("[NOVR] Leaving Unity XR camera auto tracking enabled.");
        }
    }

    public void SetAnchor(Vector3 anchor)
    {
        _anchor = anchor;
    }

    public static void Calibrate()
    {
        _calibrate = -(Vector3)_trackingPositionMethod.Invoke(null, TrackingMethodArgs);
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
            transform.localRotation = (Quaternion)_trackingRotationMethod.Invoke(null, TrackingMethodArgs);
            transform.localPosition = _anchor + (Vector3)_trackingPositionMethod.Invoke(null, TrackingMethodArgs) + _calibrate;
        }
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
}
