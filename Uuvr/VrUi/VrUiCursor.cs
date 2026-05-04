using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Uuvr.VrUi;

// The mouse cursor isn't visible in the VR UI plane, unless it's being rendered in software mode.
// So we use a custom mouse cursor graphic and render that.
public class VrUiCursor: UuvrBehaviour
{
#if CPP
    public VrUiCursor(System.IntPtr pointer) : base(pointer)
    {
    }
#endif

    private Texture2D _texture;
    private Vector2 _offset = new(22, 2);
    private VrUiManager? _vrUiManager;
    private GameObject? _cursor;
    private RectTransform? _cursorRectTransform;
    private Canvas? _cursorCanvas;
    private RawImage? _cursorImage;
    private void Start()
    {
        var bytes = File.ReadAllBytes(Path.Combine(UuvrPlugin.ModFolderPath, @"Assets\cursor.bmp"));

        // Read dimensions from BMP header
        var width = bytes[18] + (bytes[19] << 8);
        var height = bytes[22] + (bytes[23] << 8);

        var colors = new Color32[width * height];
        _texture = new Texture2D(width, height, TextureFormat.BGRA32, false);
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = new Color32(bytes[i * 4 + 54], bytes[i * 4 + 55], bytes[i * 4 + 56], bytes[i * 4 + 57]);
        }
        _texture.SetPixels32(colors);

        _texture.Apply();
        _vrUiManager = GetComponentInParent<VrUiManager>();
    }

    private void LateUpdate()
    {
        if (_texture == null) return;
        UpdateCursorCanvas();
    }

    private void Update()
    {
        if (_texture == null) return;

        // Perhaps it's unnecessary to set the cursor every frame,
        // but some games override it. I should probably leave it alone for games that already set it,
        // but I'm not sure how to check.
        //Cursor.SetCursor(_texture, _offset, CursorMode.ForceSoftware);
    }

    private void UpdateCursorCanvas()
    {
        if (_vrUiManager == null)
        {
            return;
        }

        var uiCamera = VrUiManager.I.CockpitHudCamera;

        EnsureCursorCanvas(uiCamera);

        if (_cursor == null || _cursorRectTransform == null)
        {
            return;
        }

        var screenPos = GetCursorUiPoint();
        Vector3 pos;
        if (TryGetUiPointUnderScreenPoint(screenPos, out var hit))
        {
            pos = hit.worldPosition;
        }
        else
        {
            var cam = _vrUiManager.CockpitHudCamera;
            var ray = cam.ScreenPointToRay(screenPos);
            pos = ray.origin + ray.direction * 2.5f;
        }

        _cursor.transform.position = Vector3.Lerp(_cursor.transform.position, pos, 0.8f);
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
        _cursorCanvas = _cursor.AddComponent<Canvas>();
        _cursor.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
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

    private static Vector2 GetCursorUiPoint()
    {
        return Mouse.current.position.ReadValue();
    }
    
    private static readonly List<RaycastResult> RaycastResults = new();
    private static bool TryGetUiPointUnderScreenPoint(Vector2 point, out RaycastResult result)
    {
        result = default;

        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        var pointerData = new PointerEventData(eventSystem)
        {
            position = point
        };

        RaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, RaycastResults);

        if (RaycastResults.Count == 0)
        {
            return false;
        }

        result = RaycastResults[0];
        return true;
    }
}
