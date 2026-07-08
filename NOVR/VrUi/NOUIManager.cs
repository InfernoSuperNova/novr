using System;
using NOVR.VrCamera;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NOVR.VrUi;

public class NOUIManager : NOVRBehaviour
{
    private const float SmoothingFactor = 10f;
    private Camera? _cockpitHudCamera;
    private GameObject? _smoothedForwardReference;
    private Camera? _lastMainCamera;

    public static NOUIManager I { get; private set; }

    public Camera CockpitHudCamera => _cockpitHudCamera ??= CreateUiCamera("VrCockpitHudCamera", 100);
    public GameObject CockpitHudReference => _smoothedForwardReference ??= CreateSmoothedForwardReference();

    private GameObject CreateSmoothedForwardReference()
    {
        var go = new GameObject("SmoothedForwardReference");
        go.transform.SetParent(transform);
        
        var cam = CockpitHudCamera;
        go.transform.position = cam.transform.position;
        go.transform.localRotation = cam.transform.localRotation;
        return go;
    }

    private new void Awake()
    {
        base.Awake();
        APIBus.OnMainCameraChanged += OnMainCameraChanged;
    }

    private void OnDestroy()
    {
        APIBus.OnMainCameraChanged -= OnMainCameraChanged;
    }

    private void Start()
    {
        I = this;
        Create<UIBehaviorPatcher>(transform);
        UIBehaviorPatcher.DoPatching();
        Create<VrUiCursor>(transform);
        Create<NativeVrUiRoot>(transform);
        ConfigureUiCameras();
    }

    protected override void OnSettingChanged()
    {
        base.OnSettingChanged();
        ConfigureUiCameras();
    }

    private void Update()
    {
        UpdateSmoothedPosition();
    }
    
    private void UpdateSmoothedPosition()
    {
        var smoothedForwardReference = CockpitHudReference;
        var cam = CockpitHudCamera;
        smoothedForwardReference.transform.position = cam.transform.position;
        smoothedForwardReference.transform.localRotation = Quaternion.Lerp(smoothedForwardReference.transform.localRotation, cam.transform.localRotation, Mathf.Clamp(Time.deltaTime * SmoothingFactor, 0, 1));
    }

    

    private Camera CreateUiCamera(string cameraName, float depth)
    {
        var poseDriver = Create<NOVRPoseDriver>(transform);
        poseDriver.name = cameraName;

        var camera = poseDriver.gameObject.AddComponent<Camera>();
        var additionalCameraData = poseDriver.gameObject.AddComponent<UniversalAdditionalCameraData>();
        VrCameraManager.IgnoredCameras.Add(camera);
        

        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        camera.targetTexture = null;
        camera.clearFlags = CameraClearFlags.Depth;
        camera.backgroundColor = Color.clear;
        camera.depth = depth;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.cullingMask = 1 << (int)LayerHelper.GetVrUiLayer();
        additionalCameraData.renderType = CameraRenderType.Overlay;

        return camera;
    }

    private void OnMainCameraChanged(Camera? previous, Camera? newCam)
    {
        var cockpitHudCamera = CockpitHudCamera;
        RemoveCockpitHudCameraFromStack(previous, cockpitHudCamera);
        RemoveCockpitHudCameraFromStack(_lastMainCamera, cockpitHudCamera);

        if (newCam == null)
        {
            _lastMainCamera = null;
            return;
        }

        var cameraStack = newCam.gameObject.GetComponent<UniversalAdditionalCameraData>()?.cameraStack;
        if (cameraStack != null)
        {
            RemoveDuplicateCockpitHudEntries(cameraStack, cockpitHudCamera);
            if (!cameraStack.Contains(cockpitHudCamera))
            {
                cameraStack.Add(cockpitHudCamera);
            }
        }

        _lastMainCamera = newCam;
    }

    private void ConfigureUiCameras()
    {
        ConfigureUiCamera(CockpitHudCamera);
    }

    private static void ConfigureUiCamera(Camera camera)
    {
        
        camera.clearFlags = CameraClearFlags.Depth;
        camera.backgroundColor = Color.clear;
        camera.targetTexture = null;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 10000f;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    private static void RemoveCockpitHudCameraFromStack(Camera? mainCamera, Camera cockpitHudCamera)
    {
        if (mainCamera == null)
        {
            return;
        }

        var cameraStack = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>()?.cameraStack;
        if (cameraStack == null)
        {
            return;
        }

        for (var i = cameraStack.Count - 1; i >= 0; i--)
        {
            if (cameraStack[i] == cockpitHudCamera)
            {
                cameraStack.RemoveAt(i);
            }
        }
    }

    private static void RemoveDuplicateCockpitHudEntries(
        System.Collections.Generic.List<Camera> cameraStack,
        Camera cockpitHudCamera)
    {
        var found = false;
        for (var i = cameraStack.Count - 1; i >= 0; i--)
        {
            if (cameraStack[i] != cockpitHudCamera)
            {
                continue;
            }

            if (found)
            {
                cameraStack.RemoveAt(i);
            }

            found = true;
        }
    }
}
