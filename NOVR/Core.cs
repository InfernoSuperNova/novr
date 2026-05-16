using System;
using System.Reflection;
using NOVR.VrCamera;
using NOVR.VrTogglers;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace NOVR;

public class Core : MonoBehaviour
{
    
    private float _originalFixedDeltaTime;
    
    private NOUIManager? _vrUi;
    private ThingDisabler? _thingDisabler;
    private PropertyInfo? _refreshRateProperty;
    private VrTogglerManager? _vrTogglerManager;
    
    private Aircraft _aircraft;
    private Aircraft _oldAircraft;


    public Core()
    {
        var xrDeviceType = Type.GetType("UnityEngine.XR.XRDevice, UnityEngine.XRModule") ??
                           Type.GetType("UnityEngine.XR.XRDevice, UnityEngine.VRModule") ??
                           Type.GetType("UnityEngine.VR.VRDevice, UnityEngine.VRModule") ??
                           Type.GetType("UnityEngine.VR.VRDevice, UnityEngine");

        _refreshRateProperty = xrDeviceType?.GetProperty("refreshRate");
        
        _vrUi = NOVRBehaviour.Create<NOUIManager>(transform);
        _thingDisabler = NOVRBehaviour.Create<ThingDisabler>(transform);

        _vrTogglerManager = new VrTogglerManager();
    }

    public static void Create()
    {
        new GameObject("NOVR").AddComponent<Core>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<VrCameraManager>();
        gameObject.AddComponent<EventBus>();
        
    }

    private void OnDestroy()
    {
        Debug.Log("NOVR has been destroyed. This shouldn't have happened. Recreating...");
        
        Create();
    }
    

    private void Update()
    {
        UpdatePhysicsRate();
    }

    private void UpdatePhysicsRate()
    {
        if (_originalFixedDeltaTime == 0)
        {
            _originalFixedDeltaTime = Time.fixedDeltaTime;
        }

        if (_refreshRateProperty == null) return;

        var headsetRefreshRate = (float)_refreshRateProperty.GetValue(null, null);
        if (headsetRefreshRate <= 0) return;

        if (ModConfiguration.Instance.PhysicsMatchHeadsetRefreshRate.Value)
        {
            Time.fixedDeltaTime = 1f / (float) _refreshRateProperty.GetValue(null, null);
        }
        else
        {
            Time.fixedDeltaTime = _originalFixedDeltaTime;
        }
    }
    private void FixedUpdate()
    {
        _oldAircraft = _aircraft;
        GameManager.GetLocalAircraft(out _aircraft);
        if (_aircraft != _oldAircraft) NOVRPoseDriver.Calibrate();
        CameraStateManager.enableMouseLook = false;
    }

}
