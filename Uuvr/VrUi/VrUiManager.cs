using UnityEngine;
using UnityEngine.Rendering.Universal;
using Uuvr.VrCamera;

namespace Uuvr.VrUi;

public class VrUiManager : UuvrBehaviour
{

    private Camera? _cockpitHudCamera;

    public static VrUiManager I { get; private set; }

    public Camera CockpitHudCamera => _cockpitHudCamera ??= CreateUiCamera("VrCockpitHudCamera", 100);  
    public Camera HelmetHudCamera => CockpitHudCamera;

    private Camera _cachedMainCamera;

    private void Start()
    {
        I = this;
        Create<PatchDispatcher>(transform);
        Create<VrUiCursor>(transform);
        ConfigureUiCameras();
        
    }

    protected override void OnSettingChanged()
    {
        base.OnSettingChanged();
        ConfigureUiCameras();
    }

    private void Update()
    {
        ConfigureUiCameras();
        if (_cachedMainCamera == null) UpdateMainCamera();
    }

    private Camera CreateUiCamera(string cameraName, float depth)
    {
        var poseDriver = Create<UuvrPoseDriver>(transform);
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
        camera.cullingMask = 1 << LayerHelper.GetVrUiLayer();
        additionalCameraData.renderType = CameraRenderType.Overlay;

        return camera;
    }

    private void UpdateMainCamera()
    {
        _cachedMainCamera = Camera.main;
        var camAdditionalData = _cachedMainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
        camAdditionalData.cameraStack.Add(_cockpitHudCamera);
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
}
