#if CPP
using Il2CppSystem.Collections.Generic;
#else
using System.Collections.Generic;
#endif
using UnityEngine;

namespace NOVR.VrCamera;

public class VrCamera : StereoCamera
{
    public static VrCamera? HighestDepthVrCamera { get; private set; }
    
    
    
    private LineRenderer _forwardLine;

    //private Camera _childCamera;
    
    // private int _originalCullingMask = -2;
    
#if MODERN
        private Quaternion _rotationBeforeRender;
#endif
    

    private void Start()
    {
        gameObject.AddComponent<NOVRPoseDriver>();
    }
    
    private void Update()
    {
        if (HighestDepthVrCamera == null || ParentCamera.depth > HighestDepthVrCamera.CameraInUse.depth)
        {
            HighestDepthVrCamera = this;
        }
    }
    
 


}
