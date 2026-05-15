using UnityEngine;

namespace NOVR.VrCamera;

public class StereoCamera : NOVRBehaviour
{
    
    public Camera? ParentCamera { get; private set; }
    public Camera? CameraInUse {
        get {
            return ParentCamera;
        }
    }
    
    
    protected override void Awake()
    {
        base.Awake();
        ParentCamera = GetComponent<Camera>();
    }
    
    private void Start()
    {
        // TODO: setting for disabling post processing, antialiasing, etc.
        // TODO: add option for this.
        // SetUpForwardLine();
    }
    // TODO: add option for rendering original camera forward line.
    // private void SetUpForwardLine()
    // {
    //     _forwardLine = new GameObject("VrCameraForwardLine").AddComponent<LineRenderer>();
    //     _forwardLine.transform.SetParent(transform, false);
    //     _forwardLine.useWorldSpace = false;
    //     _forwardLine.SetPositions(new []{ Vector3.forward * 2f, Vector3.forward * 10f });
    //     _forwardLine.startWidth = 0.1f;
    //     _forwardLine.endWidth = 0f;
    // }
    
    
}