#if CPP
using Il2CppSystem.Collections.Generic;
#else
using System.Collections.Generic;
#endif
using UnityEngine;

namespace Uuvr.VrCamera;

public class VrCamera : StereoCamera
{
    public static VrCamera? HighestDepthVrCamera { get; private set; }
    
    
    private UuvrPoseDriver _childCameraPoseDriver;
    
    private LineRenderer _forwardLine;

    private Camera _childCamera;
    
    // private int _originalCullingMask = -2;
    
#if MODERN
        private Quaternion _rotationBeforeRender;
#endif

    



    protected override void OnBeforeRender()
    {
        UpdateRelativeMatrix();
    }

    private void OnPreCull()
    {
        UpdateRelativeMatrix();
    }

    private void OnPreRender()
    {
        UpdateRelativeMatrix();
    }

    private void LateUpdate()
    {
        UpdateRelativeMatrix();
    }
    
#if MODERN
    protected override void OnBeginFrameRendering()
    {
        base.OnBeginFrameRendering();

        if (ModConfiguration.Instance.CameraTracking.Value != ModConfiguration.CameraTrackingMode.RelativeTransform) return;
        
        _rotationBeforeRender = transform.rotation;
        transform.rotation = _childCamera.transform.rotation;
    }

    protected override void OnEndFrameRendering()
    {
        if (ModConfiguration.Instance.CameraTracking.Value != ModConfiguration.CameraTrackingMode.RelativeTransform) return;

        transform.rotation = _rotationBeforeRender;
    }
#endif

    private void Start()
    {
        var rotationNullifier = Create<VrCameraOffset>(transform);
        _childCameraPoseDriver = Create<UuvrPoseDriver>(rotationNullifier.transform);
        _childCameraPoseDriver.name = "VrChildCamera";
        _childCamera = _childCameraPoseDriver.gameObject.AddComponent<Camera>();
        VrCameraManager.IgnoredCameras.Add(_childCamera);
        _childCamera.CopyFrom(ParentCamera);
    }
    
    private void Update()
    {
        
        var cameraTrackingMode = ModConfiguration.Instance.CameraTracking.Value;
        _childCameraPoseDriver.gameObject.SetActive(cameraTrackingMode != ModConfiguration.CameraTrackingMode.Absolute);

        
        _childCamera.cullingMask = 0;
        _childCamera.clearFlags = CameraClearFlags.Nothing;
        _childCamera.depth = -100;

        if (HighestDepthVrCamera == null || ParentCamera.depth > HighestDepthVrCamera.CameraInUse.depth)
        {
            HighestDepthVrCamera = this;
        }

        UpdateRelativeMatrix();
    }

    private void UpdateRelativeMatrix()
    {
        if (ModConfiguration.Instance.CameraTracking.Value != ModConfiguration.CameraTrackingMode.RelativeMatrix) return;
        
        var eye = ParentCamera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left ? Camera.StereoscopicEye.Left : Camera.StereoscopicEye.Right;
       
        // A bit confused by this.
        // worldToCameraMatrix by itself almost works perfectly, but it breaks culling.
        // I expected SetStereoViewMatrix by itself to be enough, but it was even more broken (although culling did work).
        // So I'm just doing both I guess.
        
        ParentCamera.worldToCameraMatrix = _childCamera.GetStereoViewMatrix(eye);

        
        
        // 29/04/2026 - I want to move away from config options towards standardized hard coded options specifically for NO. Leaving these here in case I need to test them, though.
        if (ModConfiguration.Instance.RelativeCameraSetStereoView.Value)
        {
            // Some times setting worldToCameraMatrix is enough, some times not. I'm not sure why, need to learn more.
            // Some times it's actually better not to call SetStereoViewMatrix, since it messes up the shadows. Like in Aragami.
            ParentCamera.SetStereoViewMatrix(eye, ParentCamera.worldToCameraMatrix);
        }
    }
    
    
 


}
