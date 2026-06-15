using System;
using System.Reflection;
using NOVR.VrCamera;
using NOVR.VrTogglers;
using NOVR.VrUi;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace NOVR;

public class Core : MonoBehaviour
{
    private static bool _isApplicationQuitting;
    
    private float _originalFixedDeltaTime;

    private NOVRHeadsetData? _headsetData;
    private NOUIManager? _vrUi;
    private PropertyInfo? _refreshRateProperty;
    private VrTogglerManager? _vrTogglerManager;
    
    private Aircraft _aircraft;
    private Aircraft _oldAircraft;

    public static void Create()
    {
        new GameObject("NOVR").AddComponent<Core>();
    }

    private void Awake()
    {
        Application.quitting -= HandleApplicationQuitting;
        Application.quitting += HandleApplicationQuitting;
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<VrCameraManager>();
        gameObject.AddComponent<APIBus>();
        EnsureNativeMenuEnvironmentAssetCache();
    }

    private static void HandleApplicationQuitting()
    {
        _isApplicationQuitting = true;
    }

    private void OnApplicationQuit()
    {
        _isApplicationQuitting = true;
    }

    private void OnDestroy()
    {
        if (_isApplicationQuitting) return;

        Debug.Log("NOVR has been destroyed. This shouldn't have happened. Recreating...");
        
        Create();
    }

    private void Start()
    {
        EnsureNativeMenuEnvironmentAssetCache();
        
        var xrDeviceType = Type.GetType("UnityEngine.XR.XRDevice, UnityEngine.XRModule") ??
                           Type.GetType("UnityEngine.XR.XRDevice, UnityEngine.VRModule") ??
                           Type.GetType("UnityEngine.VR.VRDevice, UnityEngine.VRModule") ??
                           Type.GetType("UnityEngine.VR.VRDevice, UnityEngine");

        _refreshRateProperty = xrDeviceType?.GetProperty("refreshRate");
        
        _headsetData = NOVRBehaviour.Create<NOVRHeadsetData>(transform);
        _vrUi = NOVRBehaviour.Create<NOUIManager>(transform);

        if (ModConfiguration.Instance.LogXrStartupDiagnostics.Value &&
            gameObject.GetComponent<XrStartupDiagnosticsBehaviour>() == null)
        {
            gameObject.AddComponent<XrStartupDiagnosticsBehaviour>();
        }
        
        _vrTogglerManager = new VrTogglerManager();
        
    }



    private void Update()
    {
        EnsureNativeMenuEnvironmentAssetCache();
        UpdatePhysicsRate();
    }

    private void EnsureNativeMenuEnvironmentAssetCache()
    {
        if (NativeMenuEnvironmentAssetCache.Instance != null ||
            gameObject.GetComponent<NativeMenuEnvironmentAssetCache>() != null)
        {
            return;
        }

        gameObject.AddComponent<NativeMenuEnvironmentAssetCache>();
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


        Time.fixedDeltaTime = _originalFixedDeltaTime;
        
    }
    private void FixedUpdate()
    {
        _oldAircraft = _aircraft;
        GameManager.GetLocalAircraft(out _aircraft);
        if (_aircraft != _oldAircraft) NOVRHeadsetData.CalibrateTranslation();
        CameraStateManager.enableMouseLook = false;
    }

}
