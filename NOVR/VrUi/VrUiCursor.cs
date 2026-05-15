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
    private Mouse _virtualMouse;
    private Mouse _realMouse;
    
    
    private int ScreenWidth => Screen.width;
    private int ScreenHeight => Screen.height;
    public Camera UiCamera =>  EventBus.CockpitHudCamera;
    
    
    public Vector2 GetScreenPoint() => _cursor ? UiCamera.WorldToScreenPoint(_cursor.transform.position)  : Vector2.zero;
    
    
    private void Start()
    {
        _texture = CreateCursorTexture();
    }

    private void Update()
    {
        if (!IsRealCursorVisible()) // This means we don't have to manually show and hide it every game update
        {
            if (_cursor != null)
            {
                _cursor.SetActive(false);
            }
            return;
        }
        
        if (!_hasInitializedEventSystem)
        {
            _realMouse = Mouse.current;
            _virtualMouse = InputSystem.AddDevice<Mouse>("VirtualMouse");
            _hasInitializedEventSystem = true;
        }
        if (_texture == null) return;
        UpdateCursorAngles();
        
        InputState.Change(_virtualMouse, new MouseState
        {
            position = GetScreenPoint(),
            buttons = _realMouse.leftButton.isPressed ? (ushort)1 : (ushort)0 
        });
        
    }
    

    private void UpdateCursorAngles()
    {
        

        EnsureCursorCanvas(UiCamera);

        if (_cursor == null || _cursorRectTransform == null)
        {
            return;
        }

        if (!_cursor.activeSelf)
        {
            _cursor.SetActive(true);
        }
        
        
        var mouse = _realMouse; // Whatever our real mouse is

        var mousePos = mouse.position.ReadValue();
        float cursorPitch = ProjectPitchAngle(mousePos.y);
        float cursorYaw = ProjectYawAngle(mousePos.x);        
        
        
        
        Vector3 direction = Quaternion.Euler(-cursorPitch, cursorYaw, 0f) * Vector3.forward;
        var inScreenSpace = UiCamera.WorldToScreenPoint(UiCamera.transform.position + direction * DefaultProjectionDistance);
        var cursorDistance = GetDistanceUnderCursor(inScreenSpace);
        Vector3 pos = UiCamera.transform.position + direction * cursorDistance;
        _cursor.transform.position = pos;
        _cursor.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
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

        foreach (var result in results)
        {
            if (result.gameObject == _cursor || result.distance < 0f)
            {
                continue;
            }

            distance = result.worldPosition == Vector3.zero
                ? result.distance
                : Vector3.Distance(UiCamera.transform.position, result.worldPosition);

            return distance > 0f;
        }

        return false;
    }


    private float ProjectPitchAngle(float y) => Mathf.Lerp(-MaxPitchDegrees, MaxPitchDegrees, y / ScreenHeight);
    private float ProjectYawAngle(float x) => Mathf.Lerp(-MaxYawDegrees, MaxYawDegrees, x / ScreenWidth);
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
    
}
