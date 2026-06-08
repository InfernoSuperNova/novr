using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace NOVR.VrUi;

[DefaultExecutionOrder(-1000)]
public class VrUiCursor: NOVRBehaviour
{
    public static VrUiCursor? Instance { get; private set; }
    public static VrUiCursor? I => Instance;

    public bool IsActive => _cursor != null && _cursor.activeSelf;
    public Vector3 CursorPosition => _cursor != null ? _cursor.transform.position : Vector3.zero;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        if (_virtualMouse != null)
        {
            try
            {
                InputSystem.RemoveDevice(_virtualMouse);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[{nameof(VrUiCursor)}] Failed to remove VirtualMouse during OnDestroy: {ex}");
            }
            _virtualMouse = null;
        }
    }

    private Texture2D? _texture;
    private const float MaxYawDegrees = 65f;
    private const float MaxPitchDegrees = 45f;
    private const float DefaultProjectionDistance = 5;
    private const int CursorTextureSize = 64;
    private const float CursorRingRadius = 12f;
    private const float CursorRingThickness = 4f;
    private GameObject? _cursor;
    private RectTransform? _cursorRectTransform;
    private Canvas? _cursorCanvas;
    private RawImage? _cursorImage;

    private bool _hasInitializedEventSystem = false;
    private Mouse? _virtualMouse;
    private Mouse? _realMouse;
    
    
    private int ScreenWidth => Screen.width;
    private int ScreenHeight => Screen.height;
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
        if (_cursor != null && camera != null)
        {
            Vector3 viewportPoint = camera.WorldToViewportPoint(_cursor.transform.position, Camera.MonoOrStereoscopicEye.Mono);
            float screenX = Mathf.Clamp(viewportPoint.x * Screen.width, 0f, Screen.width);
            float screenY = Mathf.Clamp(viewportPoint.y * Screen.height, 0f, Screen.height);
            return new Vector2(screenX, screenY);
        }
        return Vector2.zero;
    }
    
    
    private void Start()
    {
        _texture = CreateCursorTexture();
    }

    private void Update()
    {
        if (!Application.isFocused)
        {
            if (_cursor != null && _cursor.activeSelf)
            {
                _cursor.SetActive(false);
            }
            return;
        }

        if (!IsRealCursorVisible()) // This means we don't have to manually show and hide it every game update
        {
            if (_cursor != null)
            {
                _cursor.SetActive(false);
            }
            return;
        }
        
        if (_virtualMouse == null)
        {
            _realMouse = Mouse.current ?? throw new System.InvalidOperationException(
                $"[{nameof(VrUiCursor)}] Unity InputSystem could not find an active hardware Mouse device during initialization.");
            _virtualMouse = InputSystem.AddDevice<Mouse>("VirtualMouse");
            Debug.Log($"[NOVR] Added VirtualMouse device: name='{_virtualMouse.name}', path='{_virtualMouse.path}', displayName='{_virtualMouse.displayName}'");
        }

        if (!_hasInitializedEventSystem)
        {
            if (RestrictUIModuleToVirtualMouse())
            {
                _hasInitializedEventSystem = true;
            }
        }
        if (_texture == null) return;
        UpdateCursorAngles();
        
        var realMouse = _realMouse;
        if (realMouse == null || _virtualMouse == null) return;

        var screenPoint = GetScreenPoint();

        ushort buttons = 0;
        if (realMouse.leftButton.isPressed) buttons |= 1;
        if (realMouse.rightButton.isPressed) buttons |= 2;
        if (realMouse.middleButton.isPressed) buttons |= 4;

        InputState.Change(_virtualMouse, new MouseState
        {
            position = screenPoint,
            delta = realMouse.delta.ReadValue(),
            scroll = realMouse.scroll.ReadValue(),
            buttons = buttons
        });

        if (realMouse.leftButton.wasPressedThisFrame)
        {
            LogRaycastAtCursor();
        }
    }
    

    private void UpdateCursorAngles()
    {
        var camera = UiCamera;
        if (camera == null) return;

        EnsureCursorCanvas(camera);

        if (_cursor == null || _cursorRectTransform == null)
        {
            return;
        }

        if (!_cursor.activeSelf)
        {
            _cursor.SetActive(true);
        }
        
        var mouse = _realMouse;
        if (mouse == null) return;

        var mousePos = mouse.position.ReadValue();
        float cursorPitch = ProjectPitchAngle(mousePos.y);
        float cursorYaw = ProjectYawAngle(mousePos.x);        
        
        Vector3 localDirection = Quaternion.Euler(-cursorPitch, cursorYaw, 0f) * Vector3.forward;
        Quaternion referenceRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        Vector3 worldDirection = referenceRotation * localDirection;
        Vector3 viewportSpace = camera.WorldToViewportPoint(camera.transform.position + worldDirection * DefaultProjectionDistance, Camera.MonoOrStereoscopicEye.Mono);
        Vector2 inScreenSpace = new Vector2(viewportSpace.x * Screen.width, viewportSpace.y * Screen.height);
        float cursorDistance = GetDistanceUnderCursor(inScreenSpace);
        Vector3 pos = camera.transform.position + worldDirection * cursorDistance;
        _cursor.transform.position = pos;
        _cursor.transform.rotation = Quaternion.LookRotation(worldDirection, camera.transform.up);
    }
    
    private void EnsureCursorCanvas(Camera uiCaptureCamera)
    {
        if (_cursor != null)
        {
            if (_cursorImage != null)
            {
                _cursorImage.texture = _texture;
                _cursorImage.color = Color.white;
            }
            return;
        }

        _cursor = new GameObject("VrUiCursorCanvas");
        _cursor.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        _cursorCanvas = _cursor.AddComponent<Canvas>();
        _cursorCanvas.renderMode = RenderMode.WorldSpace;
        _cursorCanvas.planeDistance = Mathf.Max(uiCaptureCamera.nearClipPlane + 0.01f, 0.11f);
        _cursorCanvas.overrideSorting = true;
        _cursorCanvas.sortingOrder = short.MaxValue;
        _cursorCanvas.pixelPerfect = true;

        _cursorRectTransform = _cursor.GetComponent<RectTransform>();
        _cursorImage = _cursor.AddComponent<RawImage>();
        _cursorImage.raycastTarget = false;
        _cursorImage.texture = _texture;
        _cursorImage.color = Color.white;
        LayerHelper.SetLayerRecursive(_cursor.transform, LayerHelper.GetVrUiLayer());
        
        
        
        
    }


    private float GetDistanceUnderCursor(Vector2 screenPos)
    {
        if (TryGetUiDistanceUnderCursor(screenPos, out var uiDistance))
        {
            return uiDistance;
        }

        return DefaultProjectionDistance;
    }

    private bool TryGetUiDistanceUnderCursor(Vector2 screenPos, out float distance)
    {
        distance = default;

        if (EventSystem.current == null)
        {
            return false;
        }

        var pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        var camera = UiCamera;
        Vector3 cameraPos = camera != null ? camera.transform.position : Vector3.zero;

        foreach (var result in results)
        {
            if (result.gameObject == _cursor || 
                result.distance < 0f ||
                result.gameObject.GetComponentInParent<global::MapIcon>() != null)
            {
                continue;
            }

            distance = result.worldPosition == Vector3.zero
                ? result.distance
                : Vector3.Distance(cameraPos, result.worldPosition);

            return distance > 0f;
        }

        return false;
    }


    private float ProjectPitchAngle(float y)
    {
        int height = ScreenHeight;
        if (height <= 0) throw new System.InvalidOperationException($"[{nameof(VrUiCursor)}] Screen height is invalid ({height}).");
        return Mathf.Lerp(-MaxPitchDegrees, MaxPitchDegrees, y / height);
    }
    private float ProjectYawAngle(float x)
    {
        int width = ScreenWidth;
        if (width <= 0) throw new System.InvalidOperationException($"[{nameof(VrUiCursor)}] Screen width is invalid ({width}).");
        return Mathf.Lerp(-MaxYawDegrees, MaxYawDegrees, x / width);
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
        var green = new Color32(100, 200, 100, 255);
        var transparent = new Color32(0, 0, 0, 0);

        for (var y = 0; y < CursorTextureSize; y++)
        {
            for (var x = 0; x < CursorTextureSize; x++)
            {
                var distanceFromCenter = Vector2.Distance(new Vector2(x, y), center);
                var isRing = distanceFromCenter >= innerRadius && distanceFromCenter <= outerRadius;
                colors[y * CursorTextureSize + x] = isRing ? green : transparent;
            }
        }

        texture.SetPixels32(colors);
        texture.Apply();
        return texture;
    }
    
    private void LogRaycastAtCursor()
    {
        if (EventSystem.current == null) return;
        
        var screenPos = GetScreenPoint();
        var pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);
        
        Debug.Log($"[VrUiCursor] Click Raycast at screenPos={screenPos}: found {results.Count} results");
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            if (result.gameObject == _cursor) continue;
            
            var canvas = result.gameObject.GetComponentInParent<Canvas>();
            var cg = result.gameObject.GetComponentInParent<CanvasGroup>();
            string cgInfo = cg != null ? $", CanvasGroup(alpha={cg.alpha}, interactable={cg.interactable}, blocksRaycasts={cg.blocksRaycasts})" : "";
            string rectInfo = "";
            var rt = result.gameObject.GetComponent<RectTransform>();
            if (rt != null)
            {
                rectInfo = $", localPos={rt.localPosition}, size={rt.sizeDelta}";
            }
            Debug.Log($"[VrUiCursor]   Hit[{i}]: name='{result.gameObject.name}', path='{GetGameObjectPath(result.gameObject)}', canvas='{(canvas != null ? canvas.name : "None")}'{rectInfo}{cgInfo}");
        }
    }
    
    private static string GetGameObjectPath(GameObject go)
    {
        string path = go.name;
        Transform p = go.transform.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    private bool RestrictUIModuleToVirtualMouse()
    {
        try
        {
            if (_virtualMouse == null) return false;
            var uiModule = FindObjectOfType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (uiModule != null)
            {
                Debug.Log($"[NOVR] Restricting InputSystemUIInputModule actions to VirtualMouse (path: {_virtualMouse.path})");
                RestrictActionToVirtualMouse(uiModule.point?.action, _virtualMouse.path);
                RestrictActionToVirtualMouse(uiModule.leftClick?.action, _virtualMouse.path);
                RestrictActionToVirtualMouse(uiModule.middleClick?.action, _virtualMouse.path);
                RestrictActionToVirtualMouse(uiModule.rightClick?.action, _virtualMouse.path);
                RestrictActionToVirtualMouse(uiModule.scrollWheel?.action, _virtualMouse.path);
                return true;
            }
            else
            {
                Debug.LogWarning("[NOVR] InputSystemUIInputModule not found in scene yet, retrying next frame...");
                return false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NOVR] Exception while restricting UI actions to VirtualMouse: {ex}");
            return false;
        }
    }

    private static void RestrictActionToVirtualMouse(InputAction? action, string devicePath)
    {
        if (action == null) return;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.path.Contains("<Mouse>"))
            {
                var newPath = binding.path.Replace("<Mouse>", devicePath);
                action.ApplyBindingOverride(i, newPath);
                Debug.Log($"[NOVR] Overriding UI binding path: '{binding.path}' -> '{newPath}' for action '{action.name}'");
            }
        }
    }
}
