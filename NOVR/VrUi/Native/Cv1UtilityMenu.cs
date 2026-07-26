using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.Native;

/// <summary>
/// VR utility panel toggled directly by the left Touch menu button.
/// It does not depend on Nuclear Option's own pause or main-menu state.
/// </summary>
public sealed class Cv1UtilityMenu : NOVRBehaviour
{
    private const float CanvasScale = 0.0016f;
    private const float DistanceMeters = 1.35f;
    private const float ResetDelaySeconds = 3.0f;
    private const float CalibrationDurationSeconds = 15.0f;
    private const float MinRenderScale = 0.7f;
    private const float MaxRenderScale = 2.0f;
    private const float RenderScaleStep = 0.1f;

    private static readonly Vector2 CanvasSize = new(760f, 650f);
    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.96f);
    private static readonly Color PanelColor = new(0.07f, 0.085f, 0.095f, 0.98f);
    private static readonly Color ButtonColor = new(0.16f, 0.24f, 0.29f, 0.98f);
    private static readonly Color ActionColor = new(0.10f, 0.34f, 0.20f, 0.98f);

    private GameObject? _root;
    private Canvas? _canvas;
    private Text? _resetText;
    private Text? _renderScaleText;
    private Text? _mirrorModeText;
    private Text? _calibrationText;
    private Text? _statusText;
    private bool _resetPending;
    private float _resetAt;
    private bool _calibrationPending;
    private float _calibrationStartedAt;
    private float _nextStatusReadAt;
    private float _nextMirrorApplyAt;
    private bool _mirrorModeApplied;

    private string ControlDirectory
    {
        get
        {
            string? configured = Environment.GetEnvironmentVariable("NOVR_CV1_CONTROL_DIR");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            const string protonHostPath = @"Z:\home\george\.local\state\vr";
            if (Directory.Exists(protonHostPath))
                return protonHostPath;

            return "/home/george/.local/state/vr";
        }
    }

    private string RequestPath => Path.Combine(ControlDirectory, "cv1-camera-calibration.request");
    private string StatusPath => Path.Combine(ControlDirectory, "cv1-camera-calibration.status");

    private void Start()
    {
        CreatePanel();
        ApplyRenderScale(GetConfiguredRenderScale(), save: false);
        ApplyMirrorMode(GetConfiguredMirrorMode(), save: false);
    }

    private void OnDestroy()
    {
        if (_canvas != null)
            VrCanvasHitTester.Unregister(_canvas);
    }

    private void Update()
    {
        if (VrControllerInput.GetLeftMenuWasPressedThisFrame())
            Toggle();

        UpdateResetCountdown();
        UpdateCalibrationStatus();
        if (!_mirrorModeApplied && Time.unscaledTime >= _nextMirrorApplyAt)
        {
            _nextMirrorApplyAt = Time.unscaledTime + 1f;
            ApplyMirrorMode(GetConfiguredMirrorMode(), save: false);
        }

        if (_canvas != null)
            _canvas.worldCamera = APIBus.CockpitHudCamera;
    }

    private void Toggle()
    {
        if (_root == null)
            return;

        bool show = !_root.activeSelf;
        _root.SetActive(show);
        if (!show)
            return;

        PlaceInFrontOfHeadset();
        RefreshRenderScaleText();
        SetStatus("Point with either controller and press the trigger.");
    }

    private void PlaceInFrontOfHeadset()
    {
        if (_root == null)
            return;

        Transform reference = APIBus.CockpitHudReference.transform;
        Vector3 forward = reference.forward;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (horizontalForward.sqrMagnitude < 0.001f)
            horizontalForward = forward;
        horizontalForward.Normalize();

        _root.transform.SetParent(null, true);
        _root.transform.position = reference.position + horizontalForward * DistanceMeters;
        _root.transform.rotation = Quaternion.LookRotation(horizontalForward, Vector3.up);
        _root.transform.localScale = Vector3.one * CanvasScale;
    }

    private void QueueReset()
    {
        _resetPending = true;
        _resetAt = Time.unscaledTime + ResetDelaySeconds;
        SetStatus("Look straight ahead. View reset in 3 seconds.");
        RefreshResetText();
    }

    private void UpdateResetCountdown()
    {
        if (!_resetPending)
            return;

        if (Time.unscaledTime >= _resetAt)
        {
            NOVRHeadsetData.CalibrateTranslation();
            NOVRHeadsetData.CalibrateRotation();
            _resetPending = false;
            PlaceInFrontOfHeadset();
            SetStatus("View reset complete.");
        }

        RefreshResetText();
    }

    private void RefreshResetText()
    {
        if (_resetText == null)
            return;

        if (!_resetPending)
        {
            _resetText.text = "RESET VIEW (3s)";
            return;
        }

        float remaining = Mathf.Max(0f, _resetAt - Time.unscaledTime);
        _resetText.text = $"RESETTING {remaining:0.0}";
    }

    private void StartCameraCalibration()
    {
        try
        {
            Directory.CreateDirectory(ControlDirectory);
            if (File.Exists(StatusPath))
                File.Delete(StatusPath);
            File.WriteAllText(
                RequestPath,
                DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            _calibrationPending = true;
            _calibrationStartedAt = Time.unscaledTime;
            _nextStatusReadAt = 0f;
            SetCalibrationInstructions(
                "Move the HMD slowly where BOTH cameras can see it. " +
                "Turn it left/right and up/down while keeping its LEDs facing the cameras.");
            SetStatus("Camera calibration started.");
        }
        catch (Exception exception)
        {
            _calibrationPending = false;
            SetStatus($"Could not start calibration: {exception.Message}");
            Debug.LogError($"[NOVR] CV1 camera calibration request failed: {exception}");
        }
    }

    private void UpdateCalibrationStatus()
    {
        if (!_calibrationPending || Time.unscaledTime < _nextStatusReadAt)
            return;

        _nextStatusReadAt = Time.unscaledTime + 0.25f;

        try
        {
            if (File.Exists(StatusPath))
            {
                string status = File.ReadAllText(StatusPath).Trim();
                if (!string.IsNullOrEmpty(status))
                {
                    SetCalibrationInstructions(status);
                    if (status.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase) ||
                        status.StartsWith("FAILED", StringComparison.OrdinalIgnoreCase))
                    {
                        _calibrationPending = false;
                        SetStatus(status.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase)
                            ? "Camera calibration applied and saved."
                            : "Camera calibration failed; follow the message above.");
                    }
                }
            }
            else
            {
                float remaining = Mathf.Max(0f, CalibrationDurationSeconds -
                                                (Time.unscaledTime - _calibrationStartedAt));
                SetStatus($"Collecting camera observations: {remaining:0} seconds.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Could not read CV1 calibration status: {exception.Message}");
        }
    }

    private void ChangeRenderScale(float delta)
    {
        float value = RoundToStep(
            Mathf.Clamp(GetConfiguredRenderScale() + delta, MinRenderScale, MaxRenderScale),
            RenderScaleStep);
        ApplyRenderScale(value, save: true);
        SetStatus($"Render resolution set to {value:0.0}x.");
    }

    private static float GetConfiguredRenderScale()
    {
        return Mathf.Clamp(
            ModConfiguration.Instance?.RenderResolutionScale.Value ?? 1.3f,
            MinRenderScale,
            MaxRenderScale);
    }

    private void ApplyRenderScale(float value, bool save)
    {
        value = RoundToStep(Mathf.Clamp(value, MinRenderScale, MaxRenderScale), RenderScaleStep);
        XRSettings.eyeTextureResolutionScale = value;
        if (ModConfiguration.Instance != null)
        {
            ModConfiguration.Instance.RenderResolutionScale.Value = value;
            if (save)
                ModConfiguration.Instance.Config.Save();
        }

        RefreshRenderScaleText();
        Debug.Log($"[NOVR] Eye texture resolution scale set to {value:0.0}x.");
    }

    private void RefreshRenderScaleText()
    {
        if (_renderScaleText != null)
            _renderScaleText.text = $"{GetConfiguredRenderScale():0.0}x";
    }

    private static string GetConfiguredMirrorMode()
    {
        string configured = ModConfiguration.Instance?.DesktopMirrorMode.Value ?? "BothEyes";
        return configured.Equals("SideBySide", StringComparison.OrdinalIgnoreCase)
            ? "SideBySide"
            : "BothEyes";
    }

    private void ToggleMirrorMode()
    {
        string next = GetConfiguredMirrorMode() == "BothEyes" ? "SideBySide" : "BothEyes";
        ApplyMirrorMode(next, save: true);
    }

    private void ApplyMirrorMode(string modeName, bool save)
    {
        modeName = modeName.Equals("SideBySide", StringComparison.OrdinalIgnoreCase)
            ? "SideBySide"
            : "BothEyes";

        if (ModConfiguration.Instance != null)
        {
            ModConfiguration.Instance.DesktopMirrorMode.Value = modeName;
            if (save)
                ModConfiguration.Instance.Config.Save();
        }

        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetInstances(displays);
        _mirrorModeApplied = false;
        foreach (XRDisplaySubsystem display in displays)
        {
            if (!display.running)
                continue;

            int blitMode = ResolveMirrorBlitMode(display, modeName);
            display.SetPreferredMirrorBlitMode(blitMode);
            _mirrorModeApplied = true;
        }

        RefreshMirrorModeText();
        if (save)
        {
            SetStatus(_mirrorModeApplied
                ? $"Desktop mirror set to {FormatMirrorMode(modeName)}."
                : "Mirror mode saved; it will apply when the XR display starts.");
        }
        Debug.Log($"[NOVR] Desktop mirror mode set to {modeName}; applied={_mirrorModeApplied}.");
    }

    private static int ResolveMirrorBlitMode(XRDisplaySubsystem display, string modeName)
    {
        string keyword = modeName == "SideBySide" ? "side" : "both";
        XRDisplaySubsystemDescriptor descriptor = display.subsystemDescriptor;
        if (descriptor != null)
        {
            int count = descriptor.GetAvailableMirrorBlitModeCount();
            for (int index = 0; index < count; index++)
            {
                descriptor.GetMirrorBlitModeByIndex(index, out XRMirrorViewBlitModeDesc candidate);
                string description = candidate.blitModeDesc ?? string.Empty;
                if (description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate.blitMode;
            }
        }

        return modeName == "SideBySide"
            ? XRMirrorViewBlitMode.SideBySide
            : XRMirrorViewBlitMode.Default;
    }

    private void RefreshMirrorModeText()
    {
        if (_mirrorModeText != null)
            _mirrorModeText.text = FormatMirrorMode(GetConfiguredMirrorMode());
    }

    private static string FormatMirrorMode(string modeName)
    {
        return modeName == "SideBySide" ? "SIDE BY SIDE" : "BOTH EYES";
    }

    private void CreatePanel()
    {
        _root = new GameObject("NOVR CV1 Utility Menu");
        DontDestroyOnLoad(_root);

        var rootRect = _root.AddComponent<RectTransform>();
        rootRect.sizeDelta = CanvasSize;
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 7000;
        _canvas.worldCamera = APIBus.CockpitHudCamera;
        _root.AddComponent<GraphicRaycaster>();
        LayerHelper.SetLayerRecursive(_root.transform, LayerHelper.GetVrUiLayer());
        VrCanvasHitTester.Register(_canvas);

        CreateImage("Background", rootRect, BackgroundColor, Vector2.zero, CanvasSize);
        CreateText("Title", rootRect, "CV1 VR CONTROLS", new Vector2(0f, 224f),
            new Vector2(680f, 46f), 26, TextAnchor.MiddleCenter, Color.white);

        Button resetButton = CreateButton("Reset View", rootRect, "RESET VIEW (3s)",
            new Vector2(0f, 142f), new Vector2(590f, 60f), ActionColor, QueueReset, 20);
        _resetText = resetButton.GetComponentInChildren<Text>();

        RectTransform calibrationPanel = CreateImage("Calibration Panel", rootRect, PanelColor,
            new Vector2(0f, 20f), new Vector2(650f, 160f));
        CreateButton("Calibrate Cameras", calibrationPanel, "RECALCULATE CAMERA POSITIONS",
            new Vector2(0f, 42f), new Vector2(570f, 52f), ActionColor, StartCameraCalibration, 17);
        _calibrationText = CreateText("Calibration Instructions", calibrationPanel,
            "Uses the HMD LEDs to align both Rift cameras.",
            new Vector2(0f, -38f), new Vector2(590f, 70f), 14,
            TextAnchor.MiddleCenter, new Color(0.82f, 0.88f, 0.90f, 1f));

        RectTransform resolutionPanel = CreateImage("Resolution Panel", rootRect, PanelColor,
            new Vector2(0f, -116f), new Vector2(650f, 86f));
        CreateText("Resolution Label", resolutionPanel, "RENDER RESOLUTION",
            new Vector2(-170f, 0f), new Vector2(250f, 42f), 17,
            TextAnchor.MiddleLeft, Color.white);
        CreateButton("Resolution Down", resolutionPanel, "-", new Vector2(100f, 0f),
            new Vector2(58f, 48f), ButtonColor, () => ChangeRenderScale(-RenderScaleStep), 24);
        _renderScaleText = CreateText("Resolution Value", resolutionPanel, "1.3x",
            new Vector2(200f, 0f), new Vector2(120f, 46f), 20,
            TextAnchor.MiddleCenter, Color.white);
        CreateButton("Resolution Up", resolutionPanel, "+", new Vector2(300f, 0f),
            new Vector2(58f, 48f), ButtonColor, () => ChangeRenderScale(RenderScaleStep), 24);

        RectTransform mirrorPanel = CreateImage("Mirror Panel", rootRect, PanelColor,
            new Vector2(0f, -215f), new Vector2(650f, 70f));
        CreateText("Mirror Label", mirrorPanel, "DESKTOP OUTPUT",
            new Vector2(-170f, 0f), new Vector2(250f, 42f), 17,
            TextAnchor.MiddleLeft, Color.white);
        Button mirrorButton = CreateButton("Mirror Mode", mirrorPanel, "BOTH EYES",
            new Vector2(210f, 0f), new Vector2(280f, 48f), ButtonColor, ToggleMirrorMode, 17);
        _mirrorModeText = mirrorButton.GetComponentInChildren<Text>();
        RefreshMirrorModeText();

        _statusText = CreateText("Status", rootRect,
            "Press the left controller menu button to close.",
            new Vector2(0f, -292f), new Vector2(680f, 50f), 14,
            TextAnchor.MiddleCenter, new Color(0.78f, 0.86f, 0.89f, 1f));

        _root.SetActive(false);
    }

    private void SetCalibrationInstructions(string message)
    {
        if (_calibrationText != null)
            _calibrationText.text = message;
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message;
    }

    private static Button CreateButton(
        string name,
        RectTransform parent,
        string label,
        Vector2 position,
        Vector2 size,
        Color color,
        UnityEngine.Events.UnityAction action,
        int fontSize)
    {
        RectTransform rect = CreateImage(name, parent, color, position, size);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);
        NativeButtonFeedback.Configure(button, color);
        CreateText($"{name} Text", rect, label, Vector2.zero, size, fontSize,
            TextAnchor.MiddleCenter, Color.white);
        return button;
    }

    private static RectTransform CreateImage(
        string name,
        RectTransform parent,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());
        var rect = gameObject.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private static Text CreateText(
        string name,
        RectTransform parent,
        string value,
        Vector2 position,
        Vector2 size,
        int fontSize,
        TextAnchor alignment,
        Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());
        var rect = gameObject.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var text = gameObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static float RoundToStep(float value, float step)
    {
        return Mathf.Round(value / step) * step;
    }
}
