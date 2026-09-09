using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi;

[DefaultExecutionOrder(-1000)]
public class VrUiCursor: NOVRBehaviour
{
    public static VrUiCursor? Instance { get; private set; }
    public static VrUiCursor? I => Instance;
    private static int _instanceCount;
    private int _instanceId;

    public bool IsActive => _cursor != null && _cursor.activeSelf;
    public Vector3 CursorPosition => _cursor != null ? _cursor.transform.position : Vector3.zero;

    protected override void Awake()
    {
        base.Awake();
        _instanceId = ++_instanceCount;
        if (Instance != null && Instance != this)
            if (NOVRPlugin.LogSource != null)
                NOVRPlugin.LogSource.LogMessage($"[VrUiCursor] WARNING: Instance already set (id={Instance._instanceId}), overwriting with new instance id={_instanceId}");
        Instance = this;
        if (NOVRPlugin.LogSource != null)
            NOVRPlugin.LogSource.LogMessage($"[VrUiCursor] Awake id={_instanceId} name={name} parent={(transform.parent != null ? transform.parent.name : "<none>")}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            if (NOVRPlugin.LogSource != null)
                NOVRPlugin.LogSource.LogMessage($"[VrUiCursor] OnDestroy id={_instanceId}");
        }
    }

    private Texture2D? _texture;
    private const float DefaultProjectionDistance = 5;
    private const float CursorCanvasScale = 0.001f;
    private const int CursorTextureSize = 64;
    private const float CursorRingRadius = 12f;
    private const float CursorRingThickness = 4f;
    private const float CursorIdlePulseScale = 0.035f;
    private const float CursorIdlePulseSpeed = 5.5f;
    private const float CursorHoverScale = 1.18f;
    private const float CursorPressedScale = 0.84f;
    private const float CursorClickPulseScale = 0.22f;
    private const float CursorClickPulseDuration = 0.18f;
    private const float CursorAnimationLerpSpeed = 24f;
    private static readonly Color CursorNormalColor = new Color32(100, 200, 100, 255);
    private static readonly Color CursorHoverColor = new Color32(155, 255, 175, 255);
    private static readonly Color CursorPressedColor = new Color32(255, 224, 92, 255);
    private static readonly Color CursorMouseNormalColor = new Color32(110, 180, 240, 255);
    private static readonly Color CursorMouseHoverColor = new Color32(170, 215, 255, 255);
    private static readonly Color CursorMousePressedColor = new Color32(255, 180, 220, 255);
    private GameObject? _cursor;
    private RectTransform? _cursorRectTransform;
    private Canvas? _cursorCanvas;
    private RawImage? _cursorImage;
    private bool _cursorOverInteractive;
    private float _lastCursorClickTime = -100f;
    private bool _hasProjectionReferenceOverride;
    private Quaternion _projectionReferenceRotation = Quaternion.identity;

    private Mouse? _realMouse;
    private bool _isOffscreen;
    private Canvas? _activeCanvas;
    private bool _hasActiveCanvas;
    private Ray _lastProbeRay;
    private Vector3 _lastCursorTargetPos;
    private string _lastCanvasName = "";
    
    // Paired-diagnostic snapshots for VirtualMouse feed-vs-consume debugging
    private int _feedFrame;
    private Vector2 _feedScreenPoint;
    private Vector3 _feedCameraPos;
    private Quaternion _feedCameraRot;
    private float _feedProjM00, _feedProjM11, _feedProjM02, _feedProjM12;
    private Vector3 _feedCursorWorldPos;

    // Controller input mode
    private bool _controllerModeActive;
    private bool _triggerIsPressed;
    private bool _triggerWasPressed;
    private Vector3 _controllerOrigin;
    private Quaternion _controllerRotation;

    // Throttled diagnostic logging
    private float _lastDiagLogTime = -100f;
    private const float DiagLogInterval = 1f;
    private static int _diagFrameCounter;

    // Runtime input mode override — set by CheckModeToggleRequests() in response
    // to a mouse left-click (→ Mouse) or controller trigger press (→ Controller).
    // When _runtimeMode == Auto the config-driven default is used.
    public enum RuntimeInputMode { Auto, Mouse, Controller }
    private RuntimeInputMode _runtimeMode = RuntimeInputMode.Auto;

    // Angular dead-zone for controller ray — suppresses cursor movement when the
    // ray direction changes by less than this threshold, killing idle shimmer.
    private const float ControllerDeadZoneDegrees = 0.3f;
    private Vector3 _lastControllerRayLocalDir;
    private bool _hasLastControllerRay;

    // Direct pointer event state
    private PointerEventData? _pointerEventData;
    private GameObject? _hovered;
    private GameObject? _pointerPress;
    private bool _wasLeftDown;

    // Standard UI input module references — disabled normally, re-enabled when the
    // original game's control mapper (non-VR screen) is open so mouse clicks work.
    private StandaloneInputModule? _standaloneInputModule;
    private UnityEngine.InputSystem.UI.InputSystemUIInputModule? _inputSystemUIInputModule;

    public Camera? UiCamera
    {
        get
        {
            return APIBus.CockpitHudCamera;
        }
    }
    
    
    public Vector2 GetScreenPoint()
    {
        var camera = UiCamera;
        if (_cursor == null || camera == null)
        {
            _isOffscreen = true;
            return Vector2.zero;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(
            _cursor.transform.position);

        if (screenPoint.z <= 0f)
        {
            _isOffscreen = true;
            return Vector2.zero;
        }

        _isOffscreen = false;
        return new Vector2(
            Mathf.Clamp(screenPoint.x, 0f, camera.pixelWidth),
            Mathf.Clamp(screenPoint.y, 0f, camera.pixelHeight));
    }

    public bool IsControllerModeActive => _controllerModeActive;

    public RuntimeInputMode RuntimeMode => _runtimeMode;

    public void SetRuntimeMode(RuntimeInputMode mode)
    {
        _runtimeMode = mode;
    }

    public void SetProjectionReferenceRotation(Quaternion referenceRotation)
    {
        _projectionReferenceRotation = referenceRotation;
        _hasProjectionReferenceOverride = true;
    }

    public void ClearProjectionReferenceRotation()
    {
        _hasProjectionReferenceOverride = false;
    }

    // Routes a synthetic click to whatever the cursor is over, by reusing the
    // proven left-click selection path (closest-icon lookup that ignores
    // iconImage.raycastTarget). The EventSystem pointer pipeline approach
    // doesn't work here because the game's MapIcon raycastTarget state doesn't
    // match its visibility for hover-based icons in the map view.
    public void SimulateLeftClick()
    {
        ForwardMapClickIfNeeded();
    }


    private void Start()
    {
        _texture = CreateCursorTexture();
        if (NOVRPlugin.LogSource != null)
            NOVRPlugin.LogSource.LogMessage($"[VrUiCursor] Start id={_instanceId}");
    }

    private void Update()
    {
        if (!Application.isFocused)
        {
            if (_cursor != null && _cursor.activeSelf)
                _cursor.SetActive(false);
            return;
        }

        if (_realMouse == null)
        {
            _realMouse = Mouse.current ?? throw new System.InvalidOperationException(
                $"[{nameof(VrUiCursor)}] Unity InputSystem could not find an active hardware Mouse device during initialization.");
        }

        if (Time.frameCount < 120 || Time.frameCount % 120 == 0)
            if (_standaloneInputModule == null || _inputSystemUIInputModule == null)
                DisableStandardUIModule();

        UpdateStandardUIModuleState();
        if (_texture == null) return;

        CheckModeToggleRequests();

        // Determine input mode
        string modeSetting = ModConfiguration.Instance.CursorInputMode.Value;
        bool controllerAvailable = VrControllerInput.TryGetDominantHand(
            out _controllerOrigin, out _controllerRotation, out _triggerIsPressed);

        bool useController;
        if (_runtimeMode == RuntimeInputMode.Controller)
        {
            useController = true;
        }
        else if (_runtimeMode == RuntimeInputMode.Mouse)
        {
            useController = false;
        }
        else
        {
            useController = modeSetting == "Controller" ||
                            (modeSetting == "Auto" && controllerAvailable);
        }

        // Throttled diagnostic — show current mode + pose state once per second.
        // Gated behind VerboseDiagnostics to avoid string allocations during normal play.
        if (ModConfiguration.Instance != null && ModConfiguration.Instance.VerboseDiagnostics.Value)
        {
            float diagNow = Time.unscaledTime;
            if (diagNow - _lastDiagLogTime > DiagLogInterval)
            {
                _lastDiagLogTime = diagNow;
                string branch = (useController && controllerAvailable) ? "CONTROLLER" : "MOUSE";
                string cursorPosStr = (_cursor != null) ? _cursor.transform.position.ToString() : "<null>";
                string cursorActiveStr = (_cursor != null) ? _cursor.activeSelf.ToString() : "<null>";
                string msg = $"[VrUiCursor] mode='{modeSetting}' runtime={_runtimeMode} ctrlAvail={controllerAvailable} branch={branch} ctrlPos={_controllerOrigin} cursorPos={cursorPosStr} cursorActive={cursorActiveStr} trigger={_triggerIsPressed} _hasActiveCanvas={_hasActiveCanvas}";
                if (NOVRPlugin.LogSource != null) NOVRPlugin.LogSource.LogMessage(msg);
                else Debug.Log(msg);
            }
        }

        if (useController && controllerAvailable)
        {
            _controllerModeActive = true;
            _triggerWasPressed = _triggerIsPressed && !_triggerWasPressed;

            // Use trigger was-pressed tracking for animation
            bool triggerDownThisFrame = VrControllerInput.GetTriggerWasPressedThisFrame(
                XRNode.RightHand) || VrControllerInput.GetTriggerWasPressedThisFrame(
                XRNode.LeftHand);

            UpdateCursorAnglesFromController();

            Vector2 screenPoint = GetScreenPoint();
            if (!_isOffscreen)
            {
                _feedFrame = Time.frameCount;
                _feedScreenPoint = screenPoint;
                _feedCursorWorldPos = _cursor != null ? _cursor.transform.position : Vector3.zero;
                var snapCam = UiCamera;
                if (snapCam != null)
                {
                    _feedCameraPos = snapCam.transform.position;
                    _feedCameraRot = snapCam.transform.rotation;
                    var p = snapCam.projectionMatrix;
                    _feedProjM00 = p.m00; _feedProjM11 = p.m11;
                    _feedProjM02 = p.m02; _feedProjM12 = p.m12;
                }

                FirePointerEvents(screenPoint, _triggerIsPressed);
            }

            UpdateCursorAnimation(triggerDownThisFrame, _triggerIsPressed);

            if (triggerDownThisFrame)
            {
                ForwardMapClickIfNeeded();
            }

            _triggerWasPressed = _triggerIsPressed;
        }
        else
        {
            _controllerModeActive = false;
            if (!IsRealCursorVisible())
            {
                if (_cursor != null)
                    _cursor.SetActive(false);
                return;
            }

            UpdateCursorAngles();
            var realMouse = _realMouse;
            if (realMouse == null) return;

            Vector2 screenPoint = GetScreenPoint();
            if (!_isOffscreen)
            {
                _feedFrame = Time.frameCount;
                _feedScreenPoint = screenPoint;
                _feedCursorWorldPos = _cursor != null ? _cursor.transform.position : Vector3.zero;
                var snapCam = UiCamera;
                if (snapCam != null)
                {
                    _feedCameraPos = snapCam.transform.position;
                    _feedCameraRot = snapCam.transform.rotation;
                    var p = snapCam.projectionMatrix;
                    _feedProjM00 = p.m00; _feedProjM11 = p.m11;
                    _feedProjM02 = p.m02; _feedProjM12 = p.m12;
                }
                FirePointerEvents(screenPoint, realMouse.leftButton.isPressed);
            }

            UpdateCursorAnimation(realMouse.leftButton.wasPressedThisFrame, realMouse.leftButton.isPressed);

            if (realMouse.leftButton.wasPressedThisFrame)
            {
                ForwardMapClickIfNeeded();
            }
        }
    }

    private void CheckModeToggleRequests()
    {
        if (_realMouse != null && _realMouse.leftButton.wasPressedThisFrame
            && _runtimeMode != RuntimeInputMode.Mouse)
        {
            _runtimeMode = RuntimeInputMode.Mouse;
            return;
        }

        bool triggerPressedThisFrame =
            VrControllerInput.GetTriggerWasPressedThisFrame(XRNode.RightHand) ||
            VrControllerInput.GetTriggerWasPressedThisFrame(XRNode.LeftHand);
        if (triggerPressedThisFrame && _runtimeMode != RuntimeInputMode.Controller)
        {
            _runtimeMode = RuntimeInputMode.Controller;
        }
    }

    private void FirePointerEvents(Vector2 screenPoint, bool isLeftDown)
    {
        var es = EventSystem.current;
        if (es == null) return;

        if (_activeCanvas == null || !_hasActiveCanvas) return;

        var raycaster = _activeCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null) return;

        var ped = _pointerEventData;
        if (ped == null)
        {
            ped = new PointerEventData(es);
            _pointerEventData = ped;
        }

        ped.position = screenPoint;
        ped.delta = Vector2.zero;
        ped.button = PointerEventData.InputButton.Left;

        var results = new List<RaycastResult>();
        raycaster.Raycast(ped, results);

        // Get the event root (the ancestor that has Selectable or IPointerClickHandler)
        GameObject? current = null;
        if (results.Count > 0)
        {
            current = GetEventRoot(results[0].gameObject);
            ped.pointerCurrentRaycast = results[0];
        }

        // Hover enter / exit — use hierarchy-walking version
        if (current != _hovered)
        {
            // Exit old hierarchy
            if (_hovered != null)
            {
                ExecuteEvents.ExecuteHierarchy(_hovered, ped, ExecuteEvents.pointerExitHandler);
            }
            _hovered = current;
            // Enter new hierarchy
            if (current != null)
            {
                ExecuteEvents.ExecuteHierarchy(current, ped, ExecuteEvents.pointerEnterHandler);
            }
        }

        _cursorOverInteractive = false;
        if (current != null)
        {
            var selectable = current.GetComponent<Selectable>();
            if (selectable != null)
                _cursorOverInteractive = true;
        }

        // Click handling
        if (isLeftDown)
        {
            if (!_wasLeftDown)
            {
                _pointerPress = current;
                ped.pressPosition = screenPoint;
                ped.pointerPress = current;
                ped.clickTime = Time.unscaledTime;
                ped.clickCount = 1;
                if (current != null)
                {
                    ExecuteEvents.ExecuteHierarchy(current, ped, ExecuteEvents.pointerDownHandler);
                }
            }
            else
            {
                if (_pointerPress != null && _pointerPress == current)
                {
                    ExecuteEvents.ExecuteHierarchy(_pointerPress, ped, ExecuteEvents.dragHandler);
                }
            }
        }
        else if (_wasLeftDown)
        {
            if (_pointerPress != null)
            {
                ExecuteEvents.ExecuteHierarchy(_pointerPress, ped, ExecuteEvents.pointerUpHandler);
                if (_pointerPress == current)
                {
                    ExecuteEvents.ExecuteHierarchy(_pointerPress, ped, ExecuteEvents.pointerClickHandler);
                    ped.clickCount++;
                }
                else
                {
                    ExecuteEvents.ExecuteHierarchy(_pointerPress, ped, ExecuteEvents.initializePotentialDrag);
                }
            }
            _pointerPress = null;
        }

        _wasLeftDown = isLeftDown;
    }

    private static GameObject? GetEventRoot(GameObject? obj)
    {
        if (obj == null) return null;
        // Walk up to find the first ancestor with IPointerClickHandler (a button root)
        Transform t = obj.transform;
        while (t != null)
        {
            if (t.GetComponent<IPointerClickHandler>() != null)
                return t.gameObject;
            t = t.parent;
        }
        return obj;
    }

    private bool DisableStandardUIModule()
    {
        bool foundAny = false;
        bool verbose = ModConfiguration.Instance != null && ModConfiguration.Instance.VerboseDiagnostics.Value;

        if (_standaloneInputModule == null)
        {
            _standaloneInputModule = FindObjectOfType<StandaloneInputModule>();
            if (_standaloneInputModule != null)
            {
                if (verbose && NOVRPlugin.LogSource != null)
                    NOVRPlugin.LogSource.LogMessage($"[VrUiCursor] Disabling StandaloneInputModule (enabled={_standaloneInputModule.enabled}) on {_standaloneInputModule.gameObject.name}");
                _standaloneInputModule.enabled = false;
                foundAny = true;
            }
        }
        else if (_standaloneInputModule.enabled)
        {
            _standaloneInputModule.enabled = false;
            foundAny = true;
        }

        if (_inputSystemUIInputModule == null)
        {
            _inputSystemUIInputModule = FindObjectOfType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (_inputSystemUIInputModule != null)
            {
                if (verbose && NOVRPlugin.LogSource != null)
                    NOVRPlugin.LogSource.LogMessage($"[VrUiCursor] Disabling InputSystemUIInputModule (enabled={_inputSystemUIInputModule.enabled}) on {_inputSystemUIInputModule.gameObject.name}");
                _inputSystemUIInputModule.enabled = false;
                foundAny = true;
            }
        }
        else if (_inputSystemUIInputModule.enabled)
        {
            _inputSystemUIInputModule.enabled = false;
            foundAny = true;
        }

        return foundAny;
    }

    private void UpdateStandardUIModuleState()
    {
        if (_standaloneInputModule == null && _inputSystemUIInputModule == null)
            return;

        var controlMapperOpen = GameManager.controlMapper != null && GameManager.controlMapper.isOpen;

        if (_standaloneInputModule != null)
            _standaloneInputModule.enabled = controlMapperOpen;
        if (_inputSystemUIInputModule != null)
            _inputSystemUIInputModule.enabled = controlMapperOpen;
    }

    private void UpdateCursorAngles()
    {
        var camera = UiCamera;
        if (camera == null) return;

        EnsureCursorCanvas(camera);

        if (_cursor == null || _cursorRectTransform == null)
            return;

        if (!_cursor.activeSelf)
            _cursor.SetActive(true);

        var mouse = _realMouse;
        if (mouse == null) return;

        var mousePos = mouse.position.ReadValue();

        Transform anchor = GetAnchorTransform();
        Vector3 probeOrigin = anchor != null ? anchor.position : camera.transform.position;
        Quaternion referenceRotation = GetProjectionReferenceRotation();

        // Compute mouse-driven world direction
        float cursorPitch = Mathf.Lerp(-45f, 45f, Mathf.Clamp01(mousePos.y / Screen.height));
        float cursorYaw = Mathf.Lerp(-65f, 65f, Mathf.Clamp01(mousePos.x / Screen.width));
        Vector3 localDir = Quaternion.Euler(-cursorPitch, cursorYaw, 0f) * Vector3.forward;
        Vector3 worldDir = referenceRotation * localDir;

        Ray probeRay = new Ray(probeOrigin, worldDir);
        _lastProbeRay = probeRay;

        // Find the first canvas plane the ray intersects (no rect clamping)
        if (VrCanvasHitTester.RaycastCanvasPlanes(probeRay, out var hit))
        {
            _activeCanvas = hit.Canvas;
            _hasActiveCanvas = true;
            _lastCanvasName = hit.Canvas.name;
            _lastCursorTargetPos = hit.WorldPoint;
            VrCanvasHitTester.LastActiveCanvas = hit.Canvas;

            _cursor.transform.position = hit.WorldPoint;
            var rt = hit.Canvas.GetComponent<RectTransform>();
            _cursor.transform.rotation = Quaternion.LookRotation(rt.forward, rt.up);
        }
        else
        {
            _hasActiveCanvas = false;
            _activeCanvas = null;
            VrCanvasHitTester.LastActiveCanvas = null;
            _lastCanvasName = "(none)";
            _cursor.SetActive(false);
        }
    }

    private void UpdateCursorAnglesFromController()
    {
        var camera = UiCamera;
        if (camera == null) return;

        EnsureCursorCanvas(camera);
        if (_cursor == null || _cursorRectTransform == null)
            return;

        if (!_cursor.activeSelf)
        {
            _cursor.SetActive(true);
            _hasLastControllerRay = false;
        }

        Vector3 localDir = _controllerRotation * Vector3.forward;
        Ray probeRay = new Ray(_controllerOrigin, localDir);
        _lastProbeRay = probeRay;

        if (VrCanvasHitTester.RaycastCanvasPlanes(probeRay, out var hit))
        {
            // Angular dead-zone: suppress cursor update when the ray direction
            // hasn't moved enough, preventing idle jitter from shifting the cursor.
            if (_hasLastControllerRay)
            {
                float angleDeg = Vector3.Angle(_lastControllerRayLocalDir, localDir);
                if (angleDeg < ControllerDeadZoneDegrees)
                {
                    // Keep previous cursor position and canvas, but still update
                    // the probe ray for the laser visual.
                    _lastControllerRayLocalDir = localDir;
                    // return; // DISABLED: dead-zone + OneEuro filter kept deltas < 0.3 deg/frame, pinning cursor
                }
            }

            _hasLastControllerRay = true;
            _lastControllerRayLocalDir = localDir;

            _activeCanvas = hit.Canvas;
            _hasActiveCanvas = true;
            _lastCanvasName = hit.Canvas.name;
            _lastCursorTargetPos = hit.WorldPoint;
            VrCanvasHitTester.LastActiveCanvas = hit.Canvas;

            _cursor.transform.position = hit.WorldPoint;
            var rt = hit.Canvas.GetComponent<RectTransform>();
            _cursor.transform.rotation = Quaternion.LookRotation(rt.forward, rt.up);
        }
        else
        {
            _hasActiveCanvas = false;
            _activeCanvas = null;
            VrCanvasHitTester.LastActiveCanvas = null;
            _lastCanvasName = "(none)";
            _cursor.SetActive(false);
            _hasLastControllerRay = false;
        }
    }

    private Quaternion GetProjectionReferenceRotation()
    {
        if (_hasProjectionReferenceOverride)
        {
            return _projectionReferenceRotation;
        }

        var camera = UiCamera;
        return camera != null ? camera.transform.rotation : Quaternion.identity;
    }

    private Transform GetAnchorTransform()
    {
        if (_hasProjectionReferenceOverride)
        {
            return APIBus.CockpitHudReference?.transform;
        }
        return null;
    }
    
    private void EnsureCursorCanvas(Camera uiCaptureCamera)
    {
        if (_cursor != null)
        {
            if (_cursorImage != null)
            {
                _cursorImage.texture = _texture;
            }
            return;
        }

        _cursor = new GameObject("VrUiCursorCanvas");
        _cursor.transform.localScale = Vector3.one * CursorCanvasScale;
        _cursorCanvas = _cursor.AddComponent<Canvas>();
        _cursorCanvas.renderMode = RenderMode.WorldSpace;
        _cursorCanvas.planeDistance = Mathf.Max(uiCaptureCamera.nearClipPlane + 0.01f, 0.11f);
        _cursorCanvas.overrideSorting = true;
        _cursorCanvas.sortingOrder = short.MaxValue;
        _cursorCanvas.pixelPerfect = true;

        _cursorRectTransform = _cursor.GetComponent<RectTransform>();
        _cursorRectTransform.sizeDelta = new Vector2(CursorTextureSize, CursorTextureSize);
        _cursorImage = _cursor.AddComponent<RawImage>();
        _cursorImage.raycastTarget = false;
        _cursorImage.texture = _texture;
        _cursorImage.color = CursorNormalColor;
        LayerHelper.SetLayerRecursive(_cursor.transform, LayerHelper.GetVrUiLayer());
    }

    
    private void UpdateCursorAnimation(bool wasPressed, bool isPressed)
    {
        if (_cursor == null || _cursorImage == null) return;

        if (wasPressed)
        {
            _lastCursorClickTime = Time.unscaledTime;
        }

        var idlePulse = Mathf.Sin(Time.unscaledTime * CursorIdlePulseSpeed) * CursorIdlePulseScale;
        var clickProgress = Mathf.Clamp01((Time.unscaledTime - _lastCursorClickTime) / CursorClickPulseDuration);
        var clickPulse = clickProgress < 1f
            ? Mathf.Sin((1f - clickProgress) * Mathf.PI) * CursorClickPulseScale
            : 0f;

        var targetVisualScale = 1f + idlePulse + clickPulse;
        if (_cursorOverInteractive)
        {
            targetVisualScale *= CursorHoverScale;
        }
        if (isPressed)
        {
            targetVisualScale *= CursorPressedScale;
        }

        var targetScale = Vector3.one * (CursorCanvasScale * targetVisualScale);
        _cursor.transform.localScale = Vector3.Lerp(_cursor.transform.localScale, targetScale, Time.unscaledDeltaTime * CursorAnimationLerpSpeed);

        var targetColor = CursorNormalColor;
        if (_cursorOverInteractive)
        {
            targetColor = CursorHoverColor;
        }
        if (isPressed)
        {
            targetColor = CursorPressedColor;
        }
        if (_runtimeMode == RuntimeInputMode.Mouse)
        {
            if (isPressed)
                targetColor = CursorMousePressedColor;
            else if (_cursorOverInteractive)
                targetColor = CursorMouseHoverColor;
            else
                targetColor = CursorMouseNormalColor;
        }

        _cursorImage.color = Color.Lerp(_cursorImage.color, targetColor, Time.unscaledDeltaTime * CursorAnimationLerpSpeed);
    }


    private static bool IsRealCursorVisible() => Cursor.visible && Cursor.lockState != CursorLockMode.Locked;

    private static Texture2D CreateCursorTexture()
    {
        var texture = new Texture2D(CursorTextureSize, CursorTextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var colors = new Color32[CursorTextureSize * CursorTextureSize];
        var center = new Vector2((CursorTextureSize - 1) * 0.5f, (CursorTextureSize - 1) * 0.5f);
        var innerRadius = CursorRingRadius - CursorRingThickness * 0.5f;
        var outerRadius = CursorRingRadius + CursorRingThickness * 0.5f;
        var transparent = new Color32(0, 0, 0, 0);

        for (var y = 0; y < CursorTextureSize; y++)
        {
            for (var x = 0; x < CursorTextureSize; x++)
            {
                var distanceFromCenter = Vector2.Distance(new Vector2(x, y), center);
                var isRing = distanceFromCenter >= innerRadius && distanceFromCenter <= outerRadius;
                colors[y * CursorTextureSize + x] = isRing ? Color.white : transparent;
            }
        }

        texture.SetPixels32(colors);
        texture.Apply();
        return texture;
    }

    public void ForwardMapClickIfNeeded()
    {
        if (_activeCanvas == null || !_hasActiveCanvas) return;
        if (_activeCanvas.name != "MapCanvas") return;

        var dynamicMap = Object.FindObjectOfType<global::DynamicMap>();
        if (dynamicMap == null) return;

        var mapImage = dynamicMap.mapImage;
        if (mapImage == null) return;
        var mapImageRect = mapImage.GetComponent<RectTransform>();
        if (mapImageRect == null) return;

        var rectSize = mapImageRect.rect.size;
        if (rectSize.x < 1f || rectSize.y < 1f) return;

        var camera = APIBus.CockpitHudCamera;
        if (camera == null) return;

        var cursorScreenPoint = GetScreenPoint();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mapImageRect, cursorScreenPoint, camera, out var cursorLocal))
        {
            return;
        }

        var cursorNorm = new Vector2(cursorLocal.x / rectSize.x, cursorLocal.y / rectSize.y);
        float maxRadius = ModConfiguration.Instance != null
            ? ModConfiguration.Instance.MapClickMaxRadius.Value
            : 0.05f;
        float maxRadiusSqr = maxRadius * maxRadius;

        var icons = UnityEngine.Object.FindObjectsOfType<global::MapIcon>();
        global::MapIcon? closest = null;
        float closestSqr = float.MaxValue;

        foreach (var icon in icons)
        {
            if (icon == null || !icon.gameObject.activeInHierarchy) continue;

            Vector2 iconScreenPoint = camera.WorldToScreenPoint(icon.transform.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapImageRect, iconScreenPoint, camera, out var iconLocal))
            {
                continue;
            }

            var iconNorm = new Vector2(iconLocal.x / rectSize.x, iconLocal.y / rectSize.y);
            float sqr = (iconNorm - cursorNorm).sqrMagnitude;

            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                closest = icon;
            }
        }

        if (closest != null && closestSqr <= maxRadiusSqr)
            closest.ClickIcon(global::MapIcon.ClickSource.Mouse);
    }
}
