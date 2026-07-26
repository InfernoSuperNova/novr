using System;
using System.Reflection;
using HarmonyLib;
using NOVR.VrCamera;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NOVR.VrUi;

public class NOUIManager : NOVRBehaviour
{
    private const float SmoothingFactor = 10f;
    // The game's overlay camera that renders cockpit interior geometry (layers Cockpit and
    // CockpitAndExternal). The clipped HUD camera must render right after it, because the
    // next camera in the stack (postProcessingRenderer) clears the depth buffer.
    private const string CockpitRendererCameraName = "cockpitRenderer";
    private const float CameraDiagnosticIntervalSeconds = 10f;
    private static readonly FieldInfo? ClearDepthField = AccessTools.Field(typeof(UniversalAdditionalCameraData), "m_ClearDepth");
    private Camera? _cockpitHudCamera;
    private Camera? _clippedHudCamera;
    private GameObject? _smoothedForwardReference;
    private Camera? _lastMainCamera;
    private bool _hudCamerasCombined;
    private float _nextCameraDiagnosticTime;
    private int _dedicatedHudCameraCalls;
    private int _clippedHudCameraCalls;

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
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        _nextCameraDiagnosticTime = Time.unscaledTime + CameraDiagnosticIntervalSeconds;
    }

    private void OnDestroy()
    {
        APIBus.OnMainCameraChanged -= OnMainCameraChanged;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
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
        Create<Cv1UtilityMenu>(transform);
        ConfigureUiCameras();
    }

    protected override void OnSettingChanged()
    {
        base.OnSettingChanged();
        ConfigureUiCameras();
    }

    private void Update()
    {
        MaintainHudRenderingPath();
        EnforceClippedCameraStackPosition();
        UpdateSmoothedPosition();
        MaybeLogCameraDiagnostics();
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
        var cockpitHudCamera = CockpitHudCamera;
        var clippedHudCamera = ClippedHudCamera;
        RemoveUiCamerasFromStack(previous, cockpitHudCamera, clippedHudCamera);
        RemoveUiCamerasFromStack(_lastMainCamera, cockpitHudCamera, clippedHudCamera);

        if (newCam == null)
        {
            _lastMainCamera = null;
            return;
        }

        var cameraStack = newCam.gameObject.GetComponent<UniversalAdditionalCameraData>()?.cameraStack;
        if (cameraStack != null)
        {
            RemoveDuplicateUiCameraEntries(cameraStack, cockpitHudCamera);
            RemoveDuplicateUiCameraEntries(cameraStack, clippedHudCamera);
            if (!cameraStack.Contains(clippedHudCamera))
            {
                cameraStack.Add(clippedHudCamera);
            }

            ConfigureHudRenderingPath(cameraStack, cockpitHudCamera, clippedHudCamera);
        }

        _lastMainCamera = newCam;
        EnforceClippedCameraStackPosition();
    }

    private void ConfigureUiCameras()
    {
        ConfigureUiCamera(CockpitHudCamera);
        ConfigureClippedHudCamera(ClippedHudCamera);
        var mainCamera = APIBus.MainCamera;
        var cameraStack = mainCamera != null
            ? mainCamera.GetComponent<UniversalAdditionalCameraData>()?.cameraStack
            : null;
        if (mainCamera != null && cameraStack != null)
        {
            ConfigureHudRenderingPath(cameraStack, CockpitHudCamera, ClippedHudCamera);
        }
        EnforceClippedCameraStackPosition();
    }

    private void MaintainHudRenderingPath()
    {
        var mainCamera = APIBus.MainCamera;
        if (mainCamera == null)
        {
            return;
        }

        var cameraStack = mainCamera.GetComponent<UniversalAdditionalCameraData>()?.cameraStack;
        if (cameraStack == null)
        {
            return;
        }

        ConfigureHudRenderingPath(cameraStack, CockpitHudCamera, ClippedHudCamera);
    }

    private void ConfigureHudRenderingPath(
        System.Collections.Generic.List<Camera> cameraStack,
        Camera cockpitHudCamera,
        Camera clippedHudCamera)
    {
        var combine = ModConfiguration.Instance.CombineNovrHudCameras.Value;
        var clippedLayerMask = 1 << (int)LayerHelper.GetVrUiClippedHudLayer();
        var normalLayerMask = 1 << (int)LayerHelper.GetVrUiLayer();
        var desiredClippedMask = combine ? clippedLayerMask | normalLayerMask : clippedLayerMask;
        if (clippedHudCamera.cullingMask != desiredClippedMask)
        {
            clippedHudCamera.cullingMask = desiredClippedMask;
        }

        if (!cameraStack.Contains(clippedHudCamera))
        {
            cameraStack.Add(clippedHudCamera);
        }

        if (combine)
        {
            RemoveAllCameraEntries(cameraStack, cockpitHudCamera);
        }
        else if (!cameraStack.Contains(cockpitHudCamera))
        {
            cameraStack.Add(cockpitHudCamera);
        }

        if (_hudCamerasCombined != combine)
        {
            _hudCamerasCombined = combine;
            NOVRPlugin.LogSource?.LogMessage(
                $"[HudCameraConsolidation] mode={(combine ? "combined-novr" : "dedicated")}");
        }
    }

    private static void RemoveAllCameraEntries(
        System.Collections.Generic.List<Camera> cameraStack,
        Camera camera)
    {
        for (var i = cameraStack.Count - 1; i >= 0; i--)
        {
            if (cameraStack[i] == camera)
            {
                cameraStack.RemoveAt(i);
            }
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _cockpitHudCamera)
        {
            _dedicatedHudCameraCalls++;
        }
        else if (camera == _clippedHudCamera)
        {
            _clippedHudCameraCalls++;
        }
    }

    private void MaybeLogCameraDiagnostics()
    {
        if (!ModConfiguration.Instance.LogHudCameraConsolidation.Value ||
            Time.unscaledTime < _nextCameraDiagnosticTime)
        {
            return;
        }

        NOVRPlugin.LogSource?.LogMessage(
            $"[HudCameraConsolidation] mode={(_hudCamerasCombined ? "combined-novr" : "dedicated")} " +
            $"dedicatedHudCalls={_dedicatedHudCameraCalls} clippedHudCalls={_clippedHudCameraCalls}");

        _dedicatedHudCameraCalls = 0;
        _clippedHudCameraCalls = 0;
        _nextCameraDiagnosticTime = Time.unscaledTime + CameraDiagnosticIntervalSeconds;
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

    private static void RemoveUiCamerasFromStack(
        Camera? mainCamera,
        Camera cockpitHudCamera,
        Camera clippedHudCamera)
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
            if (cameraStack[i] == cockpitHudCamera || cameraStack[i] == clippedHudCamera)
            {
                cameraStack.RemoveAt(i);
            }
        }
    }

    private static void RemoveDuplicateUiCameraEntries(
        System.Collections.Generic.List<Camera> cameraStack,
        Camera uiCamera)
    {
        var found = false;
        for (var i = cameraStack.Count - 1; i >= 0; i--)
        {
            if (cameraStack[i] != uiCamera)
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
