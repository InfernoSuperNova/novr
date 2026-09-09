using System;
using System.Reflection;
using HarmonyLib;
using NOVR.VrCamera;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NOVR.VrUi;

public class NOUIManager : NOVRBehaviour
{
    private const float SmoothingFactor = 10f;
    // The game's overlay camera that renders cockpit interior geometry (layers Cockpit and
    // CockpitAndExternal). The clipped HUD camera must render right after it, because the
    // next camera in the stack (postProcessingRenderer) clears the depth buffer.
    private const string CockpitRendererCameraName = "cockpitRenderer";
    private static readonly FieldInfo? ClearDepthField = AccessTools.Field(typeof(UniversalAdditionalCameraData), "m_ClearDepth");
    private Camera? _cockpitHudCamera;
    private Camera? _clippedHudCamera;
    private GameObject? _smoothedForwardReference;

    public static NOUIManager I { get; private set; }

    public Camera CockpitHudCamera => _cockpitHudCamera ??= CreateUiCamera("VrCockpitHudCamera", 100, LayerHelper.GetVrUiLayer());
    public Camera ClippedHudCamera => _clippedHudCamera ??= CreateClippedHudCamera();
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
        if (NOVRPlugin.LogSource != null)
            NOVRPlugin.LogSource.LogMessage($"[NOUIManager] Start called, creating children. parent={(transform.parent != null ? transform.parent.name : "<none>")}");
        Create<UIBehaviorPatcher>(transform);
        UIBehaviorPatcher.DoPatching();
        Create<VrUiCursor>(transform);
        Create<VrControllerLaser>(transform);
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
        ConfigureUiCameras();
        UpdateSmoothedPosition();
    }

    private void UpdateSmoothedPosition()
    {
        var smoothedForwardReference = CockpitHudReference;
        var cam = CockpitHudCamera;
        smoothedForwardReference.transform.position = cam.transform.position;
        smoothedForwardReference.transform.localRotation = Quaternion.Lerp(smoothedForwardReference.transform.localRotation, cam.transform.localRotation, Mathf.Clamp(Time.deltaTime * SmoothingFactor, 0, 1));
    }

    

    private Camera CreateUiCamera(string cameraName, float depth, LayerHelper.Layers layer)
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
        camera.cullingMask = 1 << (int)layer;
        additionalCameraData.renderType = CameraRenderType.Overlay;

        return camera;
    }

    private Camera CreateClippedHudCamera()
    {
        var camera = CreateUiCamera("VrClippedHudCamera", 99, LayerHelper.GetVrUiClippedHudLayer());

        // Keep the depth buffer written by cockpitRenderer so HUD elements on the clipped
        // layer are occluded by opaque cockpit geometry (instrument panel, canopy frame).
        // URP exposes clearDepth as get-only, so the serialized field is set via reflection.
        if (ClearDepthField != null)
        {
            ClearDepthField.SetValue(camera.GetComponent<UniversalAdditionalCameraData>(), false);
        }
        else
        {
            Debug.LogWarning($"{nameof(NOUIManager)}: UniversalAdditionalCameraData.m_ClearDepth not found; cockpit geometry will not occlude the clipped HUD layer");
        }

        camera.clearFlags = CameraClearFlags.Nothing;
        return camera;
    }

    private void OnMainCameraChanged(Camera? previous, Camera? newCam)
    {
        if (newCam == null)
        {
            return;
        }

        var cameraStack = newCam.gameObject.GetComponent<UniversalAdditionalCameraData>()?.cameraStack;
        if (cameraStack == null)
        {
            return;
        }

        // Append only; EnforceClippedCameraStackPosition orders the stack every frame.
        if (!cameraStack.Contains(ClippedHudCamera))
        {
            cameraStack.Add(ClippedHudCamera);
        }

        if (!cameraStack.Contains(CockpitHudCamera))
        {
            cameraStack.Add(CockpitHudCamera);
        }
    }

    private void ConfigureUiCameras()
    {
        ConfigureUiCamera(CockpitHudCamera);
        ConfigureClippedHudCamera(ClippedHudCamera);
        EnforceClippedCameraStackPosition();
    }

    // The clipped HUD camera must render directly after the game's cockpitRenderer to see
    // its depth buffer, but the game adds its overlay cameras to the stack at its own pace,
    // so the position is re-checked every frame.
    private void EnforceClippedCameraStackPosition()
    {
        var mainCamera = APIBus.MainCamera;
        if (mainCamera == null) return;

        var cameraStack = mainCamera.GetComponent<UniversalAdditionalCameraData>()?.cameraStack;
        if (cameraStack == null) return;

        var clippedHudCamera = ClippedHudCamera;
        var currentIndex = cameraStack.IndexOf(clippedHudCamera);
        if (currentIndex < 0) return;

        var desiredIndex = -1;
        for (var i = 0; i < cameraStack.Count; i++)
        {
            var stackCamera = cameraStack[i];
            if (stackCamera != null && stackCamera.name == CockpitRendererCameraName)
            {
                desiredIndex = i + 1;
                break;
            }
        }

        if (desiredIndex < 0)
        {
            // No cockpit renderer (e.g. menus): keep it just before the main HUD camera.
            var hudIndex = cameraStack.IndexOf(CockpitHudCamera);
            desiredIndex = hudIndex >= 0 ? hudIndex : cameraStack.Count;
        }

        if (desiredIndex > currentIndex) desiredIndex--;
        if (desiredIndex == currentIndex) return;

        cameraStack.RemoveAt(currentIndex);
        cameraStack.Insert(desiredIndex, clippedHudCamera);
        Debug.Log($"{nameof(NOUIManager)}: moved clipped HUD camera in stack from {currentIndex} to {desiredIndex}");
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

    private static void ConfigureClippedHudCamera(Camera camera)
    {
        camera.clearFlags = CameraClearFlags.Nothing;
        camera.backgroundColor = Color.clear;
        camera.targetTexture = null;
        camera.rect = new Rect(0f, 0f, 1f, 1f);

        // This camera depth-tests against cockpitRenderer's buffer (near 0.01, far 5).
        // The projections don't need to match: the pitch ladder sits at ~1000m, and with
        // near=0.01 its depth value lands below any cockpit geometry inside 5m while still
        // beating the cleared far-plane value, so cockpit occludes it and sky doesn't.
        // The far plane just has to contain the 1000m slices.
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 10000f;
    }
}
