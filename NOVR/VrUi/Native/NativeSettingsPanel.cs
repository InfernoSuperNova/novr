using System;
using System.Collections.Generic;
using NuclearOption.Effects;
using Rewired;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeSettingsPanel : MonoBehaviour
{
    private const float RowStartY = 350f;
    private const float RowSpacing = 42f;
    private const float BindingScrollViewportHeight = 430f;
    private const float BindingScrollRowHeight = 40f;
    private const float BindingScrollIndicatorHeight = 430f;
    private const float BindingScrollIndicatorMinThumbHeight = 42f;
    private const float BindingActivePollSeconds = 0.08f;
    private const float BindingAxisValueRefreshSeconds = 0.08f;
    private const float BindingCaptureDelaySeconds = 0.25f;
    private const float BindingCaptureTimeoutSeconds = 8f;

    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.93f);
    private static readonly Color PanelColor = new(0.05f, 0.06f, 0.065f, 0.94f);
    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);
    private static readonly Color ButtonSelectedColor = new(0.44f, 0.49f, 0.50f, 1f);
    private static readonly Color ButtonHoverColor = new(0.34f, 0.40f, 0.42f, 1f);
    private static readonly Color ButtonPressedColor = new(0.16f, 0.20f, 0.22f, 1f);
    private static readonly Color BackButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);
    private static readonly Color ApplyButtonColor = new(0.12f, 0.34f, 0.20f, 0.96f);

    private readonly Dictionary<SettingsTab, Button> _tabButtons = new();
    private readonly Dictionary<BindingEntry, Text> _bindingAxisValueTexts = new();
    private readonly List<BindingEntry> _bindingEntries = new();
    private readonly List<BindingDeviceFilter> _bindingDeviceFilters = new();

    private NativeGameActionAdapter? _actions;
    private RectTransform? _container;
    private RectTransform? _contentRoot;
    private Font? _font;
    private SettingsTab _currentTab = SettingsTab.Audio;
    private InputMapper? _inputMapper;
    private BindingEntry? _queuedBinding;
    private BindingEntry? _activeBinding;
    private float _queuedBindingStartTime;
    private int _bindingDeviceFilterIndex;
    private BindingVisibilityFilter _bindingVisibilityFilter = BindingVisibilityFilter.All;
    private bool _bindingEntriesDirty = true;
    private float _bindingScrollOffset;
    private float _nextBindingActivePollTime;
    private float _nextBindingAxisValueUpdateTime;
    private string? _focusedBindingKey;
    private string? _lastActiveBindingKey;
    private string _bindingStatus = "Select REMAP, release the mouse, then press the new input.";
    private float _nextRowY;
    private bool _wasVisible;

    public void Initialize(NativeGameActionAdapter actions, RectTransform root)
    {
        _actions = actions;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildLayout(root);
    }

    public void SetVisible(bool visible)
    {
        if (_container == null) return;

        if (!visible && _wasVisible)
        {
            CancelBindingCapture("Binding capture canceled.");
        }

        if (visible && !_wasVisible)
        {
            ReloadPlayerSettings();
            _bindingEntriesDirty = true;
            RenderCurrentTab();
        }

        _wasVisible = visible;
        NativePanelTransition.SetVisible(_container, visible);
    }

    private void Update()
    {
        if (_queuedBinding != null && Time.unscaledTime >= _queuedBindingStartTime)
        {
            var binding = _queuedBinding;
            _queuedBinding = null;
            StartBindingCapture(binding);
        }

        if (_wasVisible && _currentTab == SettingsTab.Bindings)
        {
            UpdateVisibleBindingAxisValues();

            if (_queuedBinding == null && _activeBinding == null)
            {
                UpdateActiveBindingFocus();
            }
        }
    }

    private void BuildLayout(RectTransform root)
    {
        _container = CreateContainer("Native Settings", root, root.sizeDelta);
        CreateImage("Background", _container, BackgroundColor, Vector2.zero, _container.sizeDelta);
        CreateText("Header", _container, "SETTINGS", new Vector2(0f, NativeUiLayout.HeaderY), NativeUiLayout.HeaderSize, 22, TextAnchor.MiddleCenter, Color.white);

        var tabPanel = CreatePanel("Settings Tabs", _container, PanelColor, new Vector2(-770f, -15f), new Vector2(280f, 950f));
        CreateText("Tab Header", tabPanel, "CATEGORY", new Vector2(0f, 420f), new Vector2(240f, 30f), 17, TextAnchor.MiddleCenter, Color.white);

        AddTabButton(tabPanel, SettingsTab.Audio, "AUDIO", 345f);
        AddTabButton(tabPanel, SettingsTab.Graphics, "GRAPHICS", 285f);
        AddTabButton(tabPanel, SettingsTab.Gameplay, "GAMEPLAY", 225f);
        AddTabButton(tabPanel, SettingsTab.Controls, "CONTROLS", 165f);
        AddTabButton(tabPanel, SettingsTab.Bindings, "BINDINGS", 105f);
        AddTabButton(tabPanel, SettingsTab.Hud, "HUD", 45f);
        AddTabButton(tabPanel, SettingsTab.Chat, "CHAT", -15f);

        _contentRoot = CreatePanel("Settings Content", _container, PanelColor, new Vector2(170f, -15f), new Vector2(1440f, 950f));
        CreateMenuButton("BACK", _container, new Vector2(NativeUiLayout.FooterLeftX, NativeUiLayout.FooterY), NativeUiLayout.FooterButtonSize, BackButtonColor, BackToMainMenu, 15);
        CreateMenuButton("APPLY", _container, new Vector2(NativeUiLayout.FooterRightX, NativeUiLayout.FooterY), NativeUiLayout.FooterButtonSize, ApplyButtonColor, ApplyAndSave, 15);

        SetActiveTabButtonColors();
        RenderCurrentTab();
        NativePanelTransition.SetVisible(_container, false, instant: true);
    }

    private void AddTabButton(RectTransform parent, SettingsTab tab, string label, float y)
    {
        var button = CreateMenuButton(label, parent, new Vector2(0f, y), new Vector2(220f, 40f), ButtonColor, () => SelectTab(tab), 15);
        _tabButtons[tab] = button;
    }

    private void SelectTab(SettingsTab tab)
    {
        if (_currentTab == tab) return;

        if (_currentTab == SettingsTab.Bindings)
        {
            CancelBindingCapture(null);
        }

        _currentTab = tab;
        SetActiveTabButtonColors();
        RenderCurrentTab();
    }

    private void SetActiveTabButtonColors()
    {
        foreach (var pair in _tabButtons)
        {
            SetButtonColor(pair.Value, pair.Key == _currentTab ? ButtonSelectedColor : ButtonColor);
        }
    }

    private void RenderCurrentTab()
    {
        if (_contentRoot == null) return;

        ClearContent();
        _nextRowY = RowStartY;

        switch (_currentTab)
        {
            case SettingsTab.Audio:
                CreateContentTitle("AUDIO");
                RenderAudioTab();
                break;
            case SettingsTab.Graphics:
                CreateContentTitle("GRAPHICS");
                RenderGraphicsTab();
                break;
            case SettingsTab.Gameplay:
                CreateContentTitle("GAMEPLAY");
                RenderGameplayTab();
                break;
            case SettingsTab.Controls:
                CreateContentTitle("CONTROLS");
                RenderControlsTab();
                break;
            case SettingsTab.Bindings:
                CreateContentTitle("CONTROL BINDINGS");
                RenderBindingsTab();
                break;
            case SettingsTab.Hud:
                CreateContentTitle("HUD");
                RenderHudTab();
                break;
            case SettingsTab.Chat:
                CreateContentTitle("CHAT");
                RenderChatTab();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ClearContent()
    {
        if (_contentRoot == null) return;

        _bindingAxisValueTexts.Clear();

        for (var index = _contentRoot.childCount - 1; index >= 0; index--)
        {
            Destroy(_contentRoot.GetChild(index).gameObject);
        }
    }

    private void CreateContentTitle(string title)
    {
        if (_contentRoot == null) return;

        CreateText($"{title} Title", _contentRoot, title, new Vector2(0f, 420f), new Vector2(1320f, 32f), 20, TextAnchor.MiddleCenter, Color.white);
    }

    private void RenderAudioTab()
    {
        AddAudioRow("Master", AudioMixerVolume.Master);
        AddAudioRow("Music", AudioMixerVolume.Music);
        AddAudioRow("Interface", AudioMixerVolume.Interface);
        AddAudioRow("Effects", AudioMixerVolume.Effects);
        AddAudioRow("Menu", AudioMixerVolume.Menu);
        AddAudioRow("Radar Warning", AudioMixerVolume.RadarWarning);
        AddAudioRow("Missile Alert", AudioMixerVolume.MissileAlert);
        AddAudioRow("Jammed Noise", AudioMixerVolume.JammedNoise);
    }

    private void RenderGraphicsTab()
    {
        if (PlayerSettings.graphics == null || PlayerSettings.DetailSettings == null)
        {
            AddReadOnlyRow("Graphics settings are not initialized yet.");
            return;
        }

        AddToggleRow("VSync", () => PlayerSettings.graphics.Vsync, value => PlayerSettings.graphics.Vsync = value);
        AddToggleRow("Cinematic Mode", () => PlayerSettings.cinematicMode, SetCinematicMode);
        AddToggleRow("Debug Visuals", () => PlayerSettings.debugVis, SetDebugVisuals);
        AddOptionRow("Anti-Aliasing", GraphicsHelper.AAOptions, () => PlayerSettings.graphics.AntiAliasing, value => PlayerSettings.graphics.AntiAliasing = value);
        AddOptionRow("Texture Quality", GraphicsHelper.MipmapLimitOptions, () => PlayerSettings.graphics.MipmapLevel, value => PlayerSettings.graphics.MipmapLevel = value);
        AddOptionRow("Anisotropic Filtering", GraphicsHelper.AnisotropicOptions, () => PlayerSettings.graphics.AnisotropicFiltering, value => PlayerSettings.graphics.AnisotropicFiltering = value);
        AddOptionRow("Shadow Quality", GraphicsHelper.ShadowQualityOptions, () => PlayerSettings.graphics.ShadowQuality, value => PlayerSettings.graphics.ShadowQuality = value);
        AddFloatRow("Shadow Distance", () => PlayerSettings.graphics.ShadowDistance, value => PlayerSettings.graphics.ShadowDistance = Mathf.RoundToInt(value), 500f, 10000f, 500f, value => $"{value:0} m");
        AddToggleRow("Soft Shadows", () => PlayerSettings.graphics.SoftShadows, value => PlayerSettings.graphics.SoftShadows = value);
        AddFloatRow("LOD Bias", () => PlayerSettings.graphics.LodBias, value => PlayerSettings.graphics.LodBias = value, 1f, 4f, 0.1f, value => $"{value:0.0}");
        AddFloatRow("Cloud Detail", () => PlayerSettings.graphics.CloudDetail, value => PlayerSettings.graphics.CloudDetail = value, 0f, 1f, 0.05f, value => $"{value * 100f:0}%");
        AddToggleRow("Grass", () => PlayerSettings.DetailSettings.GrassEnabled, value => PlayerSettings.DetailSettings.GrassEnabled = value);
        AddFloatRow("Tree Distance", () => PlayerSettings.DetailSettings.TreeRangeMultiplier, value => PlayerSettings.DetailSettings.TreeRangeMultiplier = value, DetailSettings.TreeRangeMultiplierMin, DetailSettings.TreeRangeMultiplierMax, 0.1f, value => $"{value:0.0}");
        AddActionRow("Graphics Defaults", "RESET", ResetGraphicsSettings);
    }

    private void RenderGameplayTab()
    {
        AddOptionRow("Units", new[] { "Metric", "Imperial" }, () => (int)PlayerSettings.unitSystem, SetUnitSystem);
        AddFloatRow("Cockpit Camera Inertia", () => PlayerSettings.cockpitCamInertia, value => SetPlayerFloat("CockpitCamInertia", value, static assigned => PlayerSettings.cockpitCamInertia = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value * 100f:0}%");
        AddFloatRow("Cockpit FOV", () => PlayerSettings.defaultFoV, value => SetPlayerFloat("DefaultFoV", value, static assigned => PlayerSettings.defaultFoV = assigned, apply: true), 30f, 120f, 1f, value => $"{value:0} deg");
        AddFloatRow("External FOV", () => PlayerSettings.defaultExternalFoV, value => SetPlayerFloat("DefaultExternalFoV", value, static assigned => PlayerSettings.defaultExternalFoV = assigned, apply: true), 30f, 120f, 1f, value => $"{value:0} deg");
        AddToggleRow("Zoom on Boresight", () => PlayerSettings.zoomOnBoresight, value => SetPlayerBool("ZoomOnBoresight", value, static assigned => PlayerSettings.zoomOnBoresight = assigned, apply: true));
        AddToggleRow("Padlock Target", () => PlayerSettings.padLockTarget, value => SetPlayerBool("PadLockTarget", value, static assigned => PlayerSettings.padLockTarget = assigned, apply: true));
        AddToggleRow("Tac Screen IR", () => PlayerSettings.tacScreenIR, value => SetPlayerBool("TacScreenIR", value, static assigned => PlayerSettings.tacScreenIR = assigned, apply: true));
        AddToggleRow("Camera Auto NVG", () => PlayerSettings.cameraAutoNVG, value => SetPlayerBool("CameraAutoNVG", value, static assigned => PlayerSettings.cameraAutoNVG = assigned, apply: true));
        AddToggleRow("Hit Markers", () => PlayerSettings.showHitMarkers, value => SetPlayerBool("ShowHitMarkers", value, static assigned => PlayerSettings.showHitMarkers = assigned, apply: true));
    }

    private void RenderControlsTab()
    {
        AddActionRow("Control Bindings", "VIEW", () => SelectTab(SettingsTab.Bindings));
        AddFloatRow("VR Zoom Speed", () => ModConfiguration.Instance.ZoomSpeed.Value, value => ModConfiguration.Instance.ZoomSpeed.Value = value, 0.1f, 20f, 0.1f, value => $"{value:0.0}x/s");
        AddFloatRow("VR Maximum Zoom", () => ModConfiguration.Instance.MaximumZoom.Value, value => ModConfiguration.Instance.MaximumZoom.Value = value, 1f, 10f, 0.25f, value => $"{value:0.##}x");
        AddToggleRow("VR Instant Zoom Out", () => ModConfiguration.Instance.InstantZoomOut.Value, value => ModConfiguration.Instance.InstantZoomOut.Value = value);
        AddToggleRow("Virtual Joystick", () => PlayerSettings.virtualJoystickEnabled, value => SetPlayerBool("VirtualJoystickEnabled", value, static assigned => PlayerSettings.virtualJoystickEnabled = assigned, apply: true));
        AddToggleRow("Invert Virtual Pitch", () => PlayerSettings.virtualJoystickInvertPitch, value => SetPlayerBool("VirtualJoystickInvertPitch", value, static assigned => PlayerSettings.virtualJoystickInvertPitch = assigned, apply: true));
        AddToggleRow("Invert View Pitch", () => PlayerSettings.viewInvertPitch, value => SetPlayerBool("ViewInvertPitch", value, static assigned => PlayerSettings.viewInvertPitch = assigned, apply: true));
        AddToggleRow("Throttle Uses Negative Axis", () => PlayerSettings.throttleUseNegative, value => SetPlayerBool("ThrottleUseNegative", value, static assigned => PlayerSettings.throttleUseNegative = assigned, apply: true));
        AddToggleRow("Relative Throttle", () => PlayerSettings.throttleUseRelative, value => SetPlayerBool("ThrottleUseRelative", value, static assigned => PlayerSettings.throttleUseRelative = assigned, apply: true));
        AddToggleRow("Controller Menu Navigation", () => PlayerSettings.controllerMenuNavigation, value => SetPlayerBool("ControllerMenuNavigation", value, static assigned => PlayerSettings.controllerMenuNavigation = assigned, apply: true));
        AddToggleRow("Menu Weapon Safety", () => PlayerSettings.menuWeaponSafety, value => SetPlayerBool("MenuWeaponSafety", value, static assigned => PlayerSettings.menuWeaponSafety = assigned, apply: true));
        AddToggleRow("Invert Collective", () => PlayerSettings.invertCollective, value => SetPlayerBool("InvertCollective", value, static assigned => PlayerSettings.invertCollective = assigned, apply: true));
        AddToggleRow("TrackIR", () => PlayerSettings.useTrackIR, value => SetPlayerBool("UseTrackIR", value, static assigned => PlayerSettings.useTrackIR = assigned, apply: true));
        AddFloatRow("Virtual Joystick Sensitivity", () => PlayerSettings.virtualJoystickSensitivity, value => SetPlayerFloat("VirtualJoystickSensitivity", value, static assigned => PlayerSettings.virtualJoystickSensitivity = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00}");
        AddFloatRow("Virtual Joystick Centering", () => PlayerSettings.virtualJoystickCentering, value => SetPlayerFloat("VirtualJoystickCentering", value, static assigned => PlayerSettings.virtualJoystickCentering = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00}");
        AddFloatRow("View Sensitivity", () => PlayerSettings.viewSensitivity, value => SetPlayerFloat("ViewSensitivity", value, static assigned => PlayerSettings.viewSensitivity = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00}");
        AddFloatRow("View Smoothing", () => PlayerSettings.viewSmoothing, value => SetPlayerFloat("ViewSmoothing", value, static assigned => PlayerSettings.viewSmoothing = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00}");
        AddFloatRow("Button Click Delay", () => PlayerSettings.clickDelay, value => SetPlayerFloat("ClickDelay", value, static assigned => PlayerSettings.clickDelay = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00} s");
        AddFloatRow("Button Hold Delay", () => PlayerSettings.pressDelay, value => SetPlayerFloat("PressDelay", value, static assigned => PlayerSettings.pressDelay = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00} s");
    }

    private void RenderBindingsTab()
    {
        if (_contentRoot == null) return;

        EnsureBindingEntriesLoaded();

        CreateText(
            "Bindings Hint",
            _contentRoot,
            "Bindings for keyboard, mouse, and assigned controllers. REMAP or ASSIGN listens for the next button, key, or axis.",
            new Vector2(0f, 292f),
            new Vector2(900f, 28f),
            13,
            TextAnchor.MiddleCenter,
            new Color(0.80f, 0.86f, 0.88f, 1f));

        if (!ReInput.isReady)
        {
            AddReadOnlyRow("Rewired is not ready yet.");
            return;
        }

        if (_bindingEntries.Count == 0)
        {
            AddReadOnlyRow("No keyboard, mouse, or controller bindings were found.");
            return;
        }

        RenderBindingDeviceFilter();
        RenderBindingVisibilityFilter();

        var visibleEntries = GetVisibleBindingEntries();
        if (visibleEntries.Count == 0)
        {
            _nextRowY = 172f;
            AddReadOnlyRow($"No {GetBindingVisibilityFilterLabel().ToLowerInvariant()} bindings were found for {GetCurrentBindingDeviceFilter().Label}.");
            return;
        }

        RenderBindingScrollList(visibleEntries);

        if (_queuedBinding != null || _activeBinding != null)
        {
            CreateMenuButton("CANCEL", _contentRoot, new Vector2(520f, -300f), new Vector2(120f, 32f), BackButtonColor, () =>
            {
                CancelBindingCapture("Binding capture canceled.");
                RenderCurrentTab();
            }, 13);
        }

        CreateText("Bindings Status", _contentRoot, _bindingStatus, new Vector2(0f, -345f), new Vector2(880f, 30f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));
    }

    private void RenderBindingScrollList(IList<BindingEntry> visibleEntries)
    {
        if (_contentRoot == null) return;

        CreateBindingColumnHeader();

        var viewportSize = new Vector2(1250f, BindingScrollViewportHeight);
        var viewport = CreateImage("Bindings Scroll Viewport", _contentRoot, new Color(0.03f, 0.04f, 0.045f, 0.72f), new Vector2(0f, -44f), viewportSize);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var scrollThumb = CreateBindingScrollIndicator();

        var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 34f;
        scrollRect.viewport = viewport;

        var contentHeight = Mathf.Max(viewportSize.y, visibleEntries.Count * BindingScrollRowHeight);
        var content = CreateContainer("Bindings Scroll Content", viewport, new Vector2(viewportSize.x, contentHeight));
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        scrollRect.content = content;

        var focusIndex = GetFocusedBindingIndex(visibleEntries);
        if (focusIndex >= 0)
        {
            _bindingScrollOffset = GetScrollOffsetForFocusedRow(focusIndex, contentHeight, viewportSize.y);
        }

        _bindingScrollOffset = Mathf.Clamp(_bindingScrollOffset, 0f, Mathf.Max(0f, contentHeight - viewportSize.y));
        content.anchoredPosition = new Vector2(0f, _bindingScrollOffset);
        UpdateBindingScrollIndicator(scrollThumb, contentHeight, viewportSize.y);

        scrollRect.onValueChanged.AddListener(_ =>
        {
            _bindingScrollOffset = Mathf.Clamp(content.anchoredPosition.y, 0f, Mathf.Max(0f, contentHeight - viewportSize.y));
            UpdateBindingScrollIndicator(scrollThumb, contentHeight, viewportSize.y);
        });

        for (var index = 0; index < visibleEntries.Count; index++)
        {
            AddBindingRow(visibleEntries[index], content, index);
        }
    }

    private RectTransform? CreateBindingScrollIndicator()
    {
        if (_contentRoot == null) return null;

        var track = CreateImage("Bindings Scroll Indicator Track", _contentRoot, new Color(0.16f, 0.18f, 0.19f, 0.70f), new Vector2(642f, -44f), new Vector2(10f, BindingScrollIndicatorHeight));
        var thumb = CreateImage("Bindings Scroll Indicator Thumb", track, new Color(0.54f, 0.62f, 0.64f, 0.94f), Vector2.zero, new Vector2(8f, BindingScrollIndicatorHeight));
        thumb.anchorMin = new Vector2(0.5f, 0.5f);
        thumb.anchorMax = new Vector2(0.5f, 0.5f);
        thumb.pivot = new Vector2(0.5f, 0.5f);
        return thumb;
    }

    private void UpdateBindingScrollIndicator(RectTransform? thumb, float contentHeight, float viewportHeight)
    {
        if (thumb == null) return;

        var maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);
        var trackHeight = BindingScrollIndicatorHeight;
        var thumbHeight = maxOffset <= 0f
            ? trackHeight
            : Mathf.Clamp(trackHeight * viewportHeight / Mathf.Max(viewportHeight, contentHeight), BindingScrollIndicatorMinThumbHeight, trackHeight);
        var travel = Mathf.Max(0f, trackHeight - thumbHeight);
        var scrollRatio = maxOffset <= 0f ? 0f : Mathf.Clamp01(_bindingScrollOffset / maxOffset);

        thumb.sizeDelta = new Vector2(8f, thumbHeight);
        thumb.anchoredPosition = new Vector2(0f, travel * 0.5f - scrollRatio * travel);

        var image = thumb.GetComponent<Image>();
        if (image != null)
        {
            image.color = maxOffset <= 0f
                ? new Color(0.34f, 0.40f, 0.42f, 0.42f)
                : new Color(0.54f, 0.62f, 0.64f, 0.94f);
        }
    }

    private void CreateBindingColumnHeader()
    {
        if (_contentRoot == null) return;

        const float y = 178f;
        CreateText("Bindings Header Action", _contentRoot, "ACTION", new Vector2(-470f, y), new Vector2(240f, 24f), 12, TextAnchor.MiddleLeft, new Color(0.68f, 0.76f, 0.78f, 1f));
        CreateText("Bindings Header Source", _contentRoot, "SOURCE", new Vector2(-205f, y), new Vector2(160f, 24f), 12, TextAnchor.MiddleCenter, new Color(0.68f, 0.76f, 0.78f, 1f));
        CreateText("Bindings Header Binding", _contentRoot, "BINDING", new Vector2(45f, y), new Vector2(220f, 24f), 12, TextAnchor.MiddleCenter, new Color(0.68f, 0.76f, 0.78f, 1f));
        CreateText("Bindings Header Value", _contentRoot, "VALUE", new Vector2(220f, y), new Vector2(90f, 24f), 12, TextAnchor.MiddleCenter, new Color(0.68f, 0.76f, 0.78f, 1f));
    }

    private int GetFocusedBindingIndex(IList<BindingEntry> visibleEntries)
    {
        if (string.IsNullOrWhiteSpace(_focusedBindingKey))
        {
            return -1;
        }

        for (var index = 0; index < visibleEntries.Count; index++)
        {
            if (visibleEntries[index].Key == _focusedBindingKey)
            {
                return index;
            }
        }

        return -1;
    }

    private static float GetScrollOffsetForFocusedRow(int rowIndex, float contentHeight, float viewportHeight)
    {
        var centeredOffset = rowIndex * BindingScrollRowHeight - viewportHeight * 0.45f + BindingScrollRowHeight;
        return Mathf.Clamp(centeredOffset, 0f, Mathf.Max(0f, contentHeight - viewportHeight));
    }

    private void RenderBindingDeviceFilter()
    {
        if (_contentRoot == null) return;

        var filter = GetCurrentBindingDeviceFilter();
        var label = ShortenLabel(filter.Label, 42);
        const float y = 252f;

        CreateText("Bindings Device Filter Label", _contentRoot, "DEVICE", new Vector2(-385f, y), new Vector2(120f, 30f), 13, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.86f, 1f));
        CreateMenuButton("<", _contentRoot, new Vector2(-215f, y), new Vector2(50f, 30f), ButtonColor, () => CycleBindingDeviceFilter(-1), 15);
        CreateText("Bindings Device Filter Value", _contentRoot, label, new Vector2(0f, y), new Vector2(360f, 30f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", _contentRoot, new Vector2(215f, y), new Vector2(50f, 30f), ButtonColor, () => CycleBindingDeviceFilter(1), 15);
    }

    private void RenderBindingVisibilityFilter()
    {
        if (_contentRoot == null) return;

        const float y = 216f;
        CreateText("Bindings Visibility Filter Label", _contentRoot, "SHOW", new Vector2(-385f, y), new Vector2(120f, 30f), 13, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.86f, 1f));
        CreateMenuButton("<", _contentRoot, new Vector2(-215f, y), new Vector2(50f, 30f), ButtonColor, () => CycleBindingVisibilityFilter(-1), 15);
        CreateText("Bindings Visibility Filter Value", _contentRoot, GetBindingVisibilityFilterLabel(), new Vector2(0f, y), new Vector2(360f, 30f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", _contentRoot, new Vector2(215f, y), new Vector2(50f, 30f), ButtonColor, () => CycleBindingVisibilityFilter(1), 15);
    }

    private void CycleBindingDeviceFilter(int delta)
    {
        EnsureBindingEntriesLoaded();
        if (_bindingDeviceFilters.Count == 0) return;

        _bindingDeviceFilterIndex += delta;
        if (_bindingDeviceFilterIndex < 0)
        {
            _bindingDeviceFilterIndex = _bindingDeviceFilters.Count - 1;
        }
        else if (_bindingDeviceFilterIndex >= _bindingDeviceFilters.Count)
        {
            _bindingDeviceFilterIndex = 0;
        }

        ResetBindingScroll();
        RenderCurrentTab();
    }

    private void CycleBindingVisibilityFilter(int delta)
    {
        var next = (int)_bindingVisibilityFilter + delta;
        var count = Enum.GetValues(typeof(BindingVisibilityFilter)).Length;

        if (next < 0)
        {
            next = count - 1;
        }
        else if (next >= count)
        {
            next = 0;
        }

        _bindingVisibilityFilter = (BindingVisibilityFilter)next;
        ResetBindingScroll();
        RenderCurrentTab();
    }

    private string GetBindingVisibilityFilterLabel()
    {
        return _bindingVisibilityFilter switch
        {
            BindingVisibilityFilter.Assigned => "ASSIGNED",
            BindingVisibilityFilter.Unassigned => "UNASSIGNED",
            _ => "ALL"
        };
    }

    private BindingDeviceFilter GetCurrentBindingDeviceFilter()
    {
        if (_bindingDeviceFilters.Count == 0)
        {
            return new BindingDeviceFilter(string.Empty, "NO DEVICE");
        }

        _bindingDeviceFilterIndex = ClampInt(_bindingDeviceFilterIndex, 0, _bindingDeviceFilters.Count - 1);
        return _bindingDeviceFilters[_bindingDeviceFilterIndex];
    }

    private IList<BindingEntry> GetVisibleBindingEntries()
    {
        var filter = GetCurrentBindingDeviceFilter();
        var visibleEntries = new List<BindingEntry>();
        for (var index = 0; index < _bindingEntries.Count; index++)
        {
            var entry = _bindingEntries[index];
            if (entry.DeviceKey == filter.Key && IsBindingVisible(entry))
            {
                visibleEntries.Add(entry);
            }
        }

        return visibleEntries;
    }

    private bool IsBindingVisible(BindingEntry entry)
    {
        return _bindingVisibilityFilter switch
        {
            BindingVisibilityFilter.Assigned => entry.IsAssigned,
            BindingVisibilityFilter.Unassigned => !entry.IsAssigned,
            _ => true
        };
    }

    private void ResetBindingScroll()
    {
        _bindingScrollOffset = 0f;
        _focusedBindingKey = null;
        _lastActiveBindingKey = null;
    }

    private void RenderHudTab()
    {
        var hmdWidthMax = Mathf.Max(1080f, 1080f * Screen.width / Mathf.Max(1f, Screen.height));
        var hmdSideDistMax = Mathf.Max(100f, PlayerSettings.hmdWidth * 0.5f);
        var hmdTopHeightMax = Mathf.Max(100f, PlayerSettings.hmdHeight * 0.5f);

        AddToggleRow("Lag PIP", () => PlayerSettings.lagPip, value => SetHudBool("LagPip", value, static assigned => PlayerSettings.lagPip = assigned));
        AddToggleRow("Range Circle", () => PlayerSettings.rangeCircle, value => SetHudBool("RangeCircle", value, static assigned => PlayerSettings.rangeCircle = assigned));
        AddToggleRow("Gauges", () => PlayerSettings.gauges, value => SetHudBool("Gauges", value, static assigned => PlayerSettings.gauges = assigned));
        AddToggleRow("HUD Weapons", () => PlayerSettings.hudWeapons, value => SetHudBool("HUDWeapons", value, static assigned => PlayerSettings.hudWeapons = assigned));
        AddFloatRow("HMD Width", () => PlayerSettings.hmdWidth, value => SetHudFloat("HMDWidth", value, static assigned => PlayerSettings.hmdWidth = assigned), 500f, hmdWidthMax, 10f, value => $"{value:0} px");
        AddFloatRow("HMD Height", () => PlayerSettings.hmdHeight, value => SetHudFloat("HMDHeight", value, static assigned => PlayerSettings.hmdHeight = assigned), 500f, 1080f, 10f, value => $"{value:0} px");
        AddFloatRow("HMD Side Distance", () => PlayerSettings.hmdSideDist, value => SetHudFloat("HMDSideDist", value, static assigned => PlayerSettings.hmdSideDist = assigned), 100f, hmdSideDistMax, 5f, value => $"{value:0} px");
        AddFloatRow("HMD Side Angle", () => PlayerSettings.hmdSideAngle, value => SetHudFloat("HMDSideAngle", value, static assigned => PlayerSettings.hmdSideAngle = assigned), 0f, 90f, 1f, value => $"{value:0} deg");
        AddFloatRow("HMD Top Height", () => PlayerSettings.hmdTopHeight, value => SetHudFloat("HMDTopHeight", value, static assigned => PlayerSettings.hmdTopHeight = assigned), -hmdTopHeightMax, hmdTopHeightMax, 5f, value => $"{value:0} px");
        AddFloatRow("HMD Hide Distance", () => PlayerSettings.hmdHideDist, value => SetHudFloat("HMDHideDist", value, static assigned => PlayerSettings.hmdHideDist = assigned), 0.2f, 1f, 0.05f, value => $"{value * 100f:0}%");
        AddFloatRow("HMD Icon Size", () => PlayerSettings.hmdIconSize, value => SetHudFloat("HMDIconSize", value, static assigned => PlayerSettings.hmdIconSize = assigned), 10f, 80f, 5f, value => $"{value:0}");
        AddFloatRow("HUD Text Size", () => PlayerSettings.hudTextSize, value => SetHudFloat("HUDTextSize", value, static assigned => PlayerSettings.hudTextSize = assigned), 20f, 80f, 2f, value => $"{value:0}");
        AddFloatRow("HMD Text Size", () => PlayerSettings.hmdTextSize, value => SetHudFloat("HMDTextSize", value, static assigned => PlayerSettings.hmdTextSize = assigned), 20f, 80f, 2f, value => $"{value:0}");
        AddFloatRow("Overlay Text Size", () => PlayerSettings.overlayTextSize, value => SetHudFloat("OverlayTextSize", value, static assigned => PlayerSettings.overlayTextSize = assigned), 16f, 80f, 2f, value => $"{value:0}");
    }

    private void RenderChatTab()
    {
        AddToggleRow("Chat", () => PlayerSettings.chatEnabled, value => SetPlayerBool("ChatEnabled", value, static assigned => PlayerSettings.chatEnabled = assigned));
        AddToggleRow("Chat Filter", () => PlayerSettings.chatFilter, value => SetPlayerBool("ChatFilter", value, static assigned => PlayerSettings.chatFilter = assigned));
        AddToggleRow("Text To Speech", () => PlayerSettings.chatTts, value => SetPlayerBool("ChatTts", value, static assigned => PlayerSettings.chatTts = assigned));
        AddIntRow("TTS Speed", () => PlayerSettings.chatTtsSpeed, value => SetPlayerInt("ChatTtsSpeed", value, static assigned => PlayerSettings.chatTtsSpeed = assigned), -10, 10, 1, value => $"{value}");
        AddIntRow("TTS Volume", () => PlayerSettings.chatTtsVolume, value => SetPlayerInt("ChatTtsVolume", value, static assigned => PlayerSettings.chatTtsVolume = assigned), 0, 100, 5, value => $"{value}%");
    }

    private void AddAudioRow(string label, string channel)
    {
        AddFloatRow(label, () => AudioMixerVolume.GetPref(channel), value => AudioMixerVolume.SetValue(channel, value), 0f, 1f, 0.05f, value => $"{value * 100f:0}%");
    }

    private void AddToggleRow(string label, Func<bool> getValue, Action<bool> setValue)
    {
        if (_contentRoot == null) return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton(
            getValue() ? "ON" : "OFF",
            _contentRoot,
            new Vector2(310f, y),
            new Vector2(170f, 32f),
            getValue() ? ApplyButtonColor : ButtonColor,
            () =>
            {
                setValue(!getValue());
                ApplyAndRefresh();
            },
            14);
    }

    private void AddOptionRow(string label, IList<string> options, Func<int> getValue, Action<int> setValue)
    {
        if (_contentRoot == null || options.Count == 0) return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("-", _contentRoot, new Vector2(150f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(ClampInt(getValue() - 1, 0, options.Count - 1));
            ApplyAndRefresh();
        }, 18);
        var index = ClampInt(getValue(), 0, options.Count - 1);
        CreateText($"{label} Value", _contentRoot, options[index], new Vector2(310f, y), new Vector2(230f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("+", _contentRoot, new Vector2(470f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(ClampInt(getValue() + 1, 0, options.Count - 1));
            ApplyAndRefresh();
        }, 18);
    }

    private void AddFloatRow(string label, Func<float> getValue, Action<float> setValue, float min, float max, float step, Func<float, string> format)
    {
        if (_contentRoot == null) return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("-", _contentRoot, new Vector2(150f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(AdjustFloat(getValue(), -step, min, max, step));
            ApplyAndRefresh();
        }, 18);
        CreateText($"{label} Value", _contentRoot, format(getValue()), new Vector2(310f, y), new Vector2(230f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("+", _contentRoot, new Vector2(470f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(AdjustFloat(getValue(), step, min, max, step));
            ApplyAndRefresh();
        }, 18);
    }

    private void AddIntRow(string label, Func<int> getValue, Action<int> setValue, int min, int max, int step, Func<int, string> format)
    {
        if (_contentRoot == null) return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("-", _contentRoot, new Vector2(150f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(ClampInt(getValue() - step, min, max));
            ApplyAndRefresh();
        }, 18);
        CreateText($"{label} Value", _contentRoot, format(getValue()), new Vector2(310f, y), new Vector2(230f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("+", _contentRoot, new Vector2(470f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(ClampInt(getValue() + step, min, max));
            ApplyAndRefresh();
        }, 18);
    }

    private void AddActionRow(string label, string buttonLabel, UnityEngine.Events.UnityAction action)
    {
        if (_contentRoot == null) return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton(buttonLabel, _contentRoot, new Vector2(310f, y), new Vector2(170f, 32f), ButtonColor, action, 14);
    }

    private void AddBindingRow(BindingEntry entry, RectTransform parent, int rowIndex)
    {
        var y = -BindingScrollRowHeight * rowIndex - BindingScrollRowHeight * 0.5f;
        var active = IsBindingButtonInputActive(entry);
        var pending = ReferenceEquals(_queuedBinding, entry) || ReferenceEquals(_activeBinding, entry);
        var rowColor = active
            ? new Color(0.18f, 0.35f, 0.22f, 0.88f)
            : pending
                ? new Color(0.22f, 0.25f, 0.28f, 0.88f)
                : rowIndex % 2 == 0
                    ? new Color(0.075f, 0.085f, 0.09f, 0.58f)
                    : new Color(0.055f, 0.065f, 0.07f, 0.52f);

        AnchorBindingRowElement(CreateImage($"{entry.Key} Row", parent, rowColor, new Vector2(0f, y), new Vector2(1210f, 36f)));

        var remapLabel = pending ? "..." : entry.IsAssigned ? "REMAP" : "ASSIGN";
        var bindingColor = entry.IsAssigned ? Color.white : new Color(0.96f, 0.82f, 0.42f, 1f);
        if (active)
        {
            bindingColor = new Color(0.80f, 1f, 0.72f, 1f);
        }

        AnchorBindingRowElement(CreateText($"{entry.Key} Action", parent, entry.DisplayName, new Vector2(-470f, y), new Vector2(270f, 32f), 13, TextAnchor.MiddleLeft, Color.white).rectTransform);
        AnchorBindingRowElement(CreateText($"{entry.Key} Source", parent, $"{entry.ControllerLabel} {entry.BindingKind}", new Vector2(-205f, y), new Vector2(190f, 32f), 11, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.86f, 1f)).rectTransform);
        AnchorBindingRowElement(CreateText($"{entry.Key} Binding", parent, entry.BindingName, new Vector2(45f, y), new Vector2(220f, 32f), 13, TextAnchor.MiddleCenter, bindingColor).rectTransform);
        if (entry.CanShowAxisValue)
        {
            var axisText = CreateText($"{entry.Key} Axis Value", parent, GetBindingAxisValueText(entry), new Vector2(220f, y), new Vector2(90f, 32f), 12, TextAnchor.MiddleCenter, new Color(0.82f, 0.92f, 1f, 1f));
            AnchorBindingRowElement(axisText.rectTransform);
            _bindingAxisValueTexts[entry] = axisText;
        }

        if (entry.CanInvert)
        {
            AnchorBindingRowElement((RectTransform)CreateMenuButton(entry.IsInverted ? "INV ON" : "INV", parent, new Vector2(310f, y), new Vector2(76f, 30f), entry.IsInverted ? ApplyButtonColor : ButtonColor, () => ToggleBindingInvert(entry), 11).transform);
        }

        if (entry.IsAssigned)
        {
            AnchorBindingRowElement((RectTransform)CreateMenuButton("CLEAR", parent, new Vector2(405f, y), new Vector2(82f, 30f), BackButtonColor, () => ClearBinding(entry), 11).transform);
        }

        AnchorBindingRowElement((RectTransform)CreateMenuButton(remapLabel, parent, new Vector2(520f, y), new Vector2(105f, 30f), pending ? ButtonSelectedColor : ButtonColor, () => QueueBindingCapture(entry), 12).transform);
    }

    private static void AnchorBindingRowElement(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void AddReadOnlyRow(string label)
    {
        if (_contentRoot == null) return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(0f, y), new Vector2(820f, 32f), 15, TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.90f, 1f));
    }

    private float ConsumeRowY()
    {
        var y = _nextRowY;
        _nextRowY -= RowSpacing;
        return y;
    }

    private void BackToMainMenu()
    {
        CancelBindingCapture(null);
        ApplyAndSave();
        if (_actions?.TryCloseSettingsMenu() != true)
        {
            _actions?.TryInvokeCurrentMenuButton("Back", "< BACK", "BACK", "CLOSE", "MenuExit_Button");
        }
    }

    private void ResetGraphicsSettings()
    {
        if (PlayerSettings.graphics == null || PlayerSettings.DetailSettings == null) return;

        PlayerSettings.graphics.Clear();
        PlayerSettings.DetailSettings.Clear();
        ApplyAndRefresh();
    }

    private void EnsureBindingEntriesLoaded()
    {
        if (!_bindingEntriesDirty) return;

        var selectedDeviceKey = GetSelectedBindingDeviceKey();
        _bindingEntriesDirty = false;
        _bindingEntries.Clear();
        _bindingDeviceFilters.Clear();
        _bindingDeviceFilterIndex = 0;

        try
        {
            if (!ReInput.isReady || GameManager.playerInput == null)
            {
                return;
            }

            AddDeviceBindingEntries(GameManager.playerInput.controllers.maps.GetMaps<KeyboardMap>(0), "KEYBOARD", "KEYBOARD");
            AddDeviceBindingEntries(GameManager.playerInput.controllers.maps.GetMaps<MouseMap>(0), "MOUSE", "MOUSE");

            var joysticks = GameManager.playerInput.controllers.Joysticks;
            for (var index = 0; index < joysticks.Count; index++)
            {
                var joystick = joysticks[index];
                if (joystick == null) continue;

                var label = GetControllerLabel(joystick, $"CONTROLLER {index + 1}");
                AddDeviceBindingEntries(GameManager.playerInput.controllers.maps.GetMaps<JoystickMap>(joystick.id), $"JOYSTICK:{joystick.id}", label);
            }

            _bindingEntries.Sort(static (left, right) => string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase));
            RestoreBindingDeviceFilter(selectedDeviceKey);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to load control bindings: {exception}");
            _bindingEntries.Clear();
            _bindingDeviceFilters.Clear();
            _bindingDeviceFilterIndex = 0;
        }
    }

    private string? GetSelectedBindingDeviceKey()
    {
        if (_bindingDeviceFilters.Count == 0)
        {
            return null;
        }

        _bindingDeviceFilterIndex = ClampInt(_bindingDeviceFilterIndex, 0, _bindingDeviceFilters.Count - 1);
        return _bindingDeviceFilters[_bindingDeviceFilterIndex].Key;
    }

    private void RestoreBindingDeviceFilter(string? selectedDeviceKey)
    {
        if (_bindingDeviceFilters.Count == 0)
        {
            _bindingDeviceFilterIndex = 0;
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedDeviceKey))
        {
            for (var index = 0; index < _bindingDeviceFilters.Count; index++)
            {
                if (_bindingDeviceFilters[index].Key != selectedDeviceKey) continue;

                _bindingDeviceFilterIndex = index;
                return;
            }
        }

        _bindingDeviceFilterIndex = ClampInt(_bindingDeviceFilterIndex, 0, _bindingDeviceFilters.Count - 1);
    }

    private void AddDeviceBindingEntries<TMap>(IEnumerable<TMap> maps, string deviceKey, string controllerLabel)
        where TMap : ControllerMap
    {
        var userAssignableMaps = GetUserAssignableMaps(maps);
        var countBefore = _bindingEntries.Count;
        AddAssignedBindingEntriesForMaps(userAssignableMaps, deviceKey, ShortenLabel(controllerLabel, 18));
        AddUnassignedBindingEntriesForMaps(userAssignableMaps, deviceKey, ShortenLabel(controllerLabel, 18));
        if (_bindingEntries.Count > countBefore)
        {
            AddBindingDeviceFilter(deviceKey, controllerLabel);
        }
    }

    private static List<ControllerMap> GetUserAssignableMaps<TMap>(IEnumerable<TMap> maps)
        where TMap : ControllerMap
    {
        var userAssignableMaps = new List<ControllerMap>();
        foreach (var controllerMap in maps)
        {
            if (controllerMap == null || !IsUserAssignableMap(controllerMap)) continue;
            userAssignableMaps.Add(controllerMap);
        }

        return userAssignableMaps;
    }

    private void AddAssignedBindingEntriesForMaps(IList<ControllerMap> maps, string deviceKey, string controllerLabel)
    {
        foreach (var controllerMap in maps)
        {
            var categoryName = GetMapCategoryName(controllerMap);
            foreach (var actionElementMap in controllerMap.AllMaps)
            {
                if (actionElementMap == null || !actionElementMap.enabled) continue;

                var action = ReInput.mapping.GetAction(actionElementMap.actionId);
                if (action == null || !action.userAssignable) continue;

                _bindingEntries.Add(new BindingEntry(
                    controllerMap,
                    actionElementMap,
                    action,
                    deviceKey,
                    controllerLabel,
                    categoryName,
                    GetActionDisplayName(action, actionElementMap),
                    GetBindingName(actionElementMap),
                    GetBindingKind(actionElementMap),
                    GetActionRange(actionElementMap)));
            }
        }
    }

    private void AddUnassignedBindingEntriesForMaps(IList<ControllerMap> maps, string deviceKey, string controllerLabel)
    {
        if (maps.Count == 0) return;

        var assignedActionSlots = new HashSet<string>();
        var preferredMapByActionCategory = new Dictionary<int, ControllerMap>();
        var fallbackMap = maps[0];

        for (var mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            var controllerMap = maps[mapIndex];
            foreach (var actionElementMap in controllerMap.AllMaps)
            {
                if (actionElementMap == null) continue;

                var mappedAction = ReInput.mapping.GetAction(actionElementMap.actionId);
                if (mappedAction == null || !mappedAction.userAssignable) continue;

                if (actionElementMap.enabled)
                {
                    var assignedActionRange = mappedAction.type == InputActionType.Axis
                        ? GetActionRange(actionElementMap)
                        : AxisRange.Positive;
                    AddAssignedActionSlot(assignedActionSlots, mappedAction.id, assignedActionRange);
                }

                if (!preferredMapByActionCategory.ContainsKey(mappedAction.categoryId))
                {
                    preferredMapByActionCategory.Add(mappedAction.categoryId, controllerMap);
                }
            }
        }

        foreach (var action in ReInput.mapping.UserAssignableActions)
        {
            if (action == null || !action.userAssignable) continue;

            if (!preferredMapByActionCategory.TryGetValue(action.categoryId, out var controllerMap))
            {
                controllerMap = fallbackMap;
            }

            var actionRanges = GetAssignableActionRanges(action);
            for (var index = 0; index < actionRanges.Count; index++)
            {
                var actionRange = actionRanges[index];
                if (IsActionRangeAssigned(assignedActionSlots, action.id, actionRange)) continue;

                _bindingEntries.Add(new BindingEntry(
                    controllerMap,
                    action,
                    deviceKey,
                    controllerLabel,
                    GetActionCategoryName(action),
                    GetActionDisplayName(action, actionRange),
                    "UNASSIGNED",
                    GetDefaultBindingKind(action, actionRange),
                    actionRange));
            }
        }
    }

    private void AddBindingDeviceFilter(string key, string label)
    {
        for (var index = 0; index < _bindingDeviceFilters.Count; index++)
        {
            if (_bindingDeviceFilters[index].Key == key)
            {
                return;
            }
        }

        _bindingDeviceFilters.Add(new BindingDeviceFilter(key, label));
    }

    private void QueueBindingCapture(BindingEntry entry)
    {
        if (_inputMapper != null)
        {
            CancelBindingCapture(null);
        }

        _queuedBinding = entry;
        _queuedBindingStartTime = Time.unscaledTime + BindingCaptureDelaySeconds;
        _bindingStatus = $"Release the mouse, then press a new input or move an axis for {entry.DisplayName}.";
        RenderCurrentTab();
    }

    private void ClearBinding(BindingEntry entry)
    {
        CancelBindingCapture(null);
        if (!entry.IsAssigned || entry.ActionElementMap == null)
        {
            return;
        }

        try
        {
            entry.ControllerMap.DeleteElementMap(entry.ActionElementMap.id);
            SaveRewiredBindings();
            _bindingEntriesDirty = true;
            _focusedBindingKey = null;
            _lastActiveBindingKey = null;
            _bindingStatus = $"Cleared {entry.DisplayName} binding {entry.BindingName}.";
        }
        catch (Exception exception)
        {
            _bindingStatus = $"Could not clear {entry.DisplayName}.";
            Debug.LogWarning($"[NOVR] Native settings failed to clear binding '{entry.DisplayName}' on '{entry.ControllerLabel}': {exception}");
        }

        RenderCurrentTab();
    }

    private void StartBindingCapture(BindingEntry entry)
    {
        if (!ReInput.isReady)
        {
            _bindingStatus = "Cannot remap because Rewired is not ready.";
            RenderCurrentTab();
            return;
        }

        var mapper = new InputMapper
        {
            options = new InputMapper.Options
            {
                allowAxes = true,
                allowButtons = true,
                allowButtonsOnFullAxisAssignment = true,
                timeout = BindingCaptureTimeoutSeconds,
                checkForConflicts = true,
                checkForConflictsWithAllPlayers = false,
                checkForConflictsWithSelf = true,
                checkForConflictsWithSystemPlayer = true,
                defaultActionWhenConflictFound = InputMapper.ConflictResponse.Replace,
                ignoreMouseXAxis = true,
                ignoreMouseYAxis = true,
                allowKeyboardKeysWithModifiers = true,
                allowKeyboardModifierKeyAsPrimary = true
            }
        };

        mapper.InputMappedEvent += OnBindingMapped;
        mapper.CanceledEvent += OnBindingCanceled;
        mapper.ErrorEvent += OnBindingError;
        mapper.TimedOutEvent += OnBindingTimedOut;
        mapper.ConflictFoundEvent += OnBindingConflictFound;

        _activeBinding = entry;
        _inputMapper = mapper;
        _bindingStatus = $"Listening for {entry.DisplayName}. Press a button, key, or move an axis.";

        var context = new InputMapper.Context
        {
            actionId = entry.ActionId,
            controllerMap = entry.ControllerMap,
            actionElementMapToReplace = entry.ActionElementMap,
            actionRange = entry.ActionRange
        };

        if (!mapper.Start(context))
        {
            mapper.RemoveAllEventListeners();
            _inputMapper = null;
            _activeBinding = null;
            _bindingStatus = $"Could not start remapping {entry.DisplayName}.";
        }

        RenderCurrentTab();
    }

    private void ToggleBindingInvert(BindingEntry entry)
    {
        CancelBindingCapture(null);
        if (!entry.CanInvert)
        {
            return;
        }

        if (entry.ActionElementMap == null) return;

        entry.ActionElementMap.invert = !entry.ActionElementMap.invert;
        SaveRewiredBindings();
        _bindingEntriesDirty = true;
        _bindingStatus = $"{entry.DisplayName} axis invert {(entry.ActionElementMap.invert ? "enabled" : "disabled")}.";
        RenderCurrentTab();
    }

    private void UpdateActiveBindingFocus()
    {
        if (Time.unscaledTime < _nextBindingActivePollTime) return;
        _nextBindingActivePollTime = Time.unscaledTime + BindingActivePollSeconds;

        if (!ReInput.isReady || GameManager.playerInput == null)
        {
            return;
        }

        EnsureBindingEntriesLoaded();
        var visibleEntries = GetVisibleBindingEntries();
        BindingEntry? activeEntry = null;
        for (var index = 0; index < visibleEntries.Count; index++)
        {
            if (!IsBindingButtonInputActive(visibleEntries[index])) continue;

            activeEntry = visibleEntries[index];
            break;
        }

        var activeKey = activeEntry?.Key;
        if (activeKey == _lastActiveBindingKey)
        {
            return;
        }

        _lastActiveBindingKey = activeKey;
        _focusedBindingKey = activeKey;
        if (activeEntry != null)
        {
            _bindingStatus = $"Pressed input is bound to {activeEntry.DisplayName}.";
        }

        RenderCurrentTab();
    }

    private void UpdateVisibleBindingAxisValues()
    {
        if (Time.unscaledTime < _nextBindingAxisValueUpdateTime) return;
        _nextBindingAxisValueUpdateTime = Time.unscaledTime + BindingAxisValueRefreshSeconds;

        foreach (var pair in _bindingAxisValueTexts)
        {
            if (pair.Value == null) continue;

            pair.Value.text = GetBindingAxisValueText(pair.Key);
        }
    }

    private static bool IsBindingButtonInputActive(BindingEntry entry)
    {
        if (!entry.IsAssigned || GameManager.playerInput == null || !ReInput.isReady)
        {
            return false;
        }

        if (entry.ActionType != InputActionType.Button || entry.ActionElementMap?.elementType == ControllerElementType.Axis)
        {
            return false;
        }

        try
        {
            return GameManager.playerInput.GetButton(entry.ActionId);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string GetBindingAxisValueText(BindingEntry entry)
    {
        if (!entry.CanShowAxisValue || GameManager.playerInput == null || !ReInput.isReady)
        {
            return string.Empty;
        }

        try
        {
            var value = GameManager.playerInput.GetAxis(entry.ActionId);
            if (Mathf.Abs(value) < 0.005f)
            {
                value = 0f;
            }

            return $"{value:+0.00;-0.00;0.00}";
        }
        catch (Exception)
        {
            return "--";
        }
    }

    private void OnBindingMapped(InputMapper.InputMappedEventData eventData)
    {
        var mappedName = eventData.actionElementMap != null
            ? GetBindingName(eventData.actionElementMap)
            : "input";
        var actionName = eventData.actionElementMap != null
            ? GetActionDisplayName(ReInput.mapping.GetAction(eventData.actionElementMap.actionId), eventData.actionElementMap)
            : _activeBinding?.DisplayName ?? "Binding";

        ReleaseInputMapper();
        SaveRewiredBindings();
        _bindingEntriesDirty = true;
        _bindingStatus = $"{actionName} mapped to {mappedName}.";
        RenderCurrentTab();
    }

    private void OnBindingCanceled(InputMapper.CanceledEventData eventData)
    {
        var message = string.IsNullOrWhiteSpace(eventData.message) ? "Binding capture canceled." : eventData.message;
        ReleaseInputMapper();
        _bindingStatus = message;
        RenderCurrentTab();
    }

    private void OnBindingError(InputMapper.ErrorEventData eventData)
    {
        var message = string.IsNullOrWhiteSpace(eventData.message) ? "Binding capture failed." : eventData.message;
        ReleaseInputMapper();
        _bindingStatus = message;
        RenderCurrentTab();
    }

    private void OnBindingTimedOut(InputMapper.TimedOutEventData eventData)
    {
        ReleaseInputMapper();
        _bindingStatus = "Binding capture timed out.";
        RenderCurrentTab();
    }

    private void OnBindingConflictFound(InputMapper.ConflictFoundEventData eventData)
    {
        _bindingStatus = "Replacing conflicting binding.";
        eventData.responseCallback(InputMapper.ConflictResponse.Replace);
    }

    private void CancelBindingCapture(string? status)
    {
        _queuedBinding = null;
        if (_inputMapper != null)
        {
            var mapper = _inputMapper;
            _inputMapper = null;
            _activeBinding = null;
            mapper.RemoveAllEventListeners();
            mapper.Clear();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            _bindingStatus = status!;
        }
    }

    private void ReleaseInputMapper()
    {
        if (_inputMapper == null) return;

        _inputMapper.RemoveAllEventListeners();
        _inputMapper = null;
        _activeBinding = null;
    }

    private static void SaveRewiredBindings()
    {
        try
        {
            ReInput.userDataStore?.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to save Rewired bindings: {exception}");
        }
    }

    private static bool IsUserAssignableMap(ControllerMap controllerMap)
    {
        var mapCategory = ReInput.mapping.GetMapCategory(controllerMap.categoryId);
        return mapCategory == null || mapCategory.userAssignable;
    }

    private static string GetMapCategoryName(ControllerMap controllerMap)
    {
        var mapCategory = ReInput.mapping.GetMapCategory(controllerMap.categoryId);
        if (mapCategory != null)
        {
            if (!string.IsNullOrWhiteSpace(mapCategory.descriptiveName))
            {
                return mapCategory.descriptiveName;
            }

            if (!string.IsNullOrWhiteSpace(mapCategory.name))
            {
                return mapCategory.name;
            }
        }

        return string.IsNullOrWhiteSpace(controllerMap.name) ? "General" : controllerMap.name;
    }

    private static string GetActionCategoryName(InputAction action)
    {
        var actionCategory = ReInput.mapping.GetActionCategory(action.categoryId);
        if (actionCategory != null)
        {
            if (!string.IsNullOrWhiteSpace(actionCategory.descriptiveName))
            {
                return actionCategory.descriptiveName;
            }

            if (!string.IsNullOrWhiteSpace(actionCategory.name))
            {
                return actionCategory.name;
            }
        }

        return "General";
    }

    private static string GetActionDisplayName(InputAction action)
    {
        if (!string.IsNullOrWhiteSpace(action.descriptiveName))
        {
            return action.descriptiveName;
        }

        if (!string.IsNullOrWhiteSpace(action.name))
        {
            return action.name;
        }

        return $"Action {action.id}";
    }

    private static string GetActionDisplayName(InputAction action, AxisRange actionRange)
    {
        if (action.type == InputActionType.Axis)
        {
            if (actionRange == AxisRange.Negative && !string.IsNullOrWhiteSpace(action.negativeDescriptiveName))
            {
                return action.negativeDescriptiveName;
            }

            if (actionRange == AxisRange.Positive && !string.IsNullOrWhiteSpace(action.positiveDescriptiveName))
            {
                return action.positiveDescriptiveName;
            }
        }

        return GetActionDisplayName(action);
    }

    private static string GetActionDisplayName(InputAction? action, ActionElementMap actionElementMap)
    {
        if (!string.IsNullOrWhiteSpace(actionElementMap.actionDescriptiveName))
        {
            return actionElementMap.actionDescriptiveName;
        }

        if (action != null)
        {
            if (action.type == InputActionType.Axis)
            {
                var actionRange = GetActionRange(actionElementMap);
                if (actionRange == AxisRange.Negative && !string.IsNullOrWhiteSpace(action.negativeDescriptiveName))
                {
                    return action.negativeDescriptiveName;
                }

                if (actionRange == AxisRange.Positive && !string.IsNullOrWhiteSpace(action.positiveDescriptiveName))
                {
                    return action.positiveDescriptiveName;
                }
            }

            if (!string.IsNullOrWhiteSpace(action.descriptiveName))
            {
                return action.descriptiveName;
            }

            if (!string.IsNullOrWhiteSpace(action.name))
            {
                return action.name;
            }
        }

        return $"Action {actionElementMap.actionId}";
    }

    private static string GetBindingName(ActionElementMap actionElementMap)
    {
        if (!string.IsNullOrWhiteSpace(actionElementMap.elementIdentifierName))
        {
            return actionElementMap.elementIdentifierName;
        }

        var keyCode = actionElementMap.keyCode;
        if (keyCode != KeyCode.None)
        {
            return keyCode.ToString();
        }

        return $"Element {actionElementMap.elementIdentifierId}";
    }

    private static string GetBindingKind(ActionElementMap actionElementMap)
    {
        if (actionElementMap.elementType != ControllerElementType.Axis)
        {
            return "BTN";
        }

        var actionRange = GetActionRange(actionElementMap);
        if (actionRange == AxisRange.Negative)
        {
            return "AXIS -";
        }

        if (actionRange == AxisRange.Positive && actionElementMap.axisRange != AxisRange.Full)
        {
            return "AXIS +";
        }

        return actionElementMap.invert ? "AXIS INV" : "AXIS";
    }

    private static string GetDefaultBindingKind(InputAction action, AxisRange actionRange)
    {
        if (action.type != InputActionType.Axis)
        {
            return "BTN";
        }

        return actionRange switch
        {
            AxisRange.Negative => "AXIS -",
            AxisRange.Positive => "AXIS +",
            _ => "AXIS"
        };
    }

    private static List<AxisRange> GetAssignableActionRanges(InputAction action)
    {
        var ranges = new List<AxisRange>();
        if (action.type != InputActionType.Axis)
        {
            ranges.Add(AxisRange.Positive);
            return ranges;
        }

        var hasNegative = !string.IsNullOrWhiteSpace(action.negativeDescriptiveName);
        var hasPositive = !string.IsNullOrWhiteSpace(action.positiveDescriptiveName);

        if (hasNegative || hasPositive)
        {
            if (hasNegative)
            {
                ranges.Add(AxisRange.Negative);
            }

            if (hasPositive)
            {
                ranges.Add(AxisRange.Positive);
            }

            return ranges;
        }

        ranges.Add(AxisRange.Full);
        return ranges;
    }

    private static void AddAssignedActionSlot(HashSet<string> assignedActionSlots, int actionId, AxisRange actionRange)
    {
        assignedActionSlots.Add(GetBindingActionSlotKey(actionId, actionRange));
        if (actionRange != AxisRange.Full) return;

        assignedActionSlots.Add(GetBindingActionSlotKey(actionId, AxisRange.Negative));
        assignedActionSlots.Add(GetBindingActionSlotKey(actionId, AxisRange.Positive));
    }

    private static bool IsActionRangeAssigned(HashSet<string> assignedActionSlots, int actionId, AxisRange actionRange)
    {
        return assignedActionSlots.Contains(GetBindingActionSlotKey(actionId, actionRange))
            || assignedActionSlots.Contains(GetBindingActionSlotKey(actionId, AxisRange.Full));
    }

    private static string GetBindingActionSlotKey(int actionId, AxisRange actionRange)
    {
        return $"{actionId}:{actionRange}";
    }

    private static AxisRange GetActionRange(ActionElementMap actionElementMap)
    {
        if (actionElementMap.axisRange == AxisRange.Full)
        {
            return AxisRange.Full;
        }

        if (actionElementMap.axisRange == AxisRange.Positive || actionElementMap.axisRange == AxisRange.Negative)
        {
            return actionElementMap.axisRange;
        }

        if (actionElementMap.axisContribution == Pole.Negative)
        {
            return AxisRange.Negative;
        }

        if (actionElementMap.axisContribution == Pole.Positive)
        {
            return AxisRange.Positive;
        }

        return actionElementMap.axisRange;
    }

    private static string GetControllerLabel(Controller controller, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(controller.name))
        {
            return controller.name;
        }

        return fallback;
    }

    private static string ShortenLabel(string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        var trimmed = label.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
    }

    private void SetUnitSystem(int value)
    {
        PlayerSettings.unitSystem = (PlayerSettings.UnitSystem)value;
        PlayerPrefs.SetInt("UnitSystem", value);
        ApplyPlayerSettings();
    }

    private void SetCinematicMode(bool value)
    {
        PlayerSettings.cinematicMode = value;
        PlayerPrefs.SetInt("CinematicMode", value ? 1 : 0);
    }

    private void SetDebugVisuals(bool value)
    {
        PlayerSettings.debugVis = value;
        PlayerPrefs.SetInt("DebugVis", value ? 1 : 0);
    }

    private static void SetPlayerBool(string key, bool value, Action<bool> assign, bool apply = false)
    {
        assign(value);
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        if (apply)
        {
            ApplyPlayerSettings();
        }
    }

    private static void SetHudBool(string key, bool value, Action<bool> assign)
    {
        assign(value);
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        ApplyHudSettings();
    }

    private static void SetPlayerFloat(string key, float value, Action<float> assign, bool apply = false)
    {
        assign(value);
        PlayerPrefs.SetFloat(key, value);
        if (apply)
        {
            ApplyPlayerSettings();
        }
    }

    private static void SetHudFloat(string key, float value, Action<float> assign)
    {
        assign(value);
        PlayerPrefs.SetFloat(key, value);
        ApplyHudSettings();
    }

    private static void SetPlayerInt(string key, int value, Action<int> assign, bool apply = false)
    {
        assign(value);
        PlayerPrefs.SetInt(key, value);
        if (apply)
        {
            ApplyPlayerSettings();
        }
    }

    private void ApplyAndRefresh()
    {
        PlayerPrefs.Save();
        RenderCurrentTab();
    }

    private void ApplyAndSave()
    {
        PlayerPrefs.Save();
        ApplyPlayerSettings();
        ApplyHudSettings();
    }

    private static void ReloadPlayerSettings()
    {
        try
        {
            PlayerSettings.LoadPrefs();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to reload PlayerSettings: {exception}");
        }
    }

    private static void ApplyPlayerSettings()
    {
        try
        {
            PlayerSettings.ApplyPrefs();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to apply PlayerSettings: {exception}");
        }
    }

    private static void ApplyHudSettings()
    {
        try
        {
            PlayerSettings.ApplyPrefs();
            if (SceneSingleton<HUDOptions>.i != null)
            {
                SceneSingleton<HUDOptions>.i.ApplyHUDSettings();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to apply HUD settings: {exception}");
        }
    }

    private static float AdjustFloat(float value, float delta, float min, float max, float step)
    {
        var adjusted = Mathf.Clamp(value + delta, min, max);
        return Mathf.Clamp(Mathf.Round(adjusted / step) * step, min, max);
    }

    private static int ClampInt(int value, int min, int max)
    {
        return Mathf.Clamp(value, min, max);
    }

    private RectTransform CreateContainer(string name, RectTransform parent, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;
        return rectTransform;
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        return CreateImage(name, parent, color, anchoredPosition, size);
    }

    private RectTransform CreateImage(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var image = gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }

    private Text CreateText(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var textComponent = gameObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = _font;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        return textComponent;
    }

    private Button CreateMenuButton(string label, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 15)
    {
        var rectTransform = CreateImage(label, parent, color, anchoredPosition, size);
        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = rectTransform.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        NativeButtonFeedback.Configure(button, color);

        CreateText($"{label} Text", rectTransform, label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white);
        return button;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        var image = button.targetGraphic;
        if (image != null)
        {
            image.color = color;
        }

        NativeButtonFeedback.SetNormalColor(button, color);
    }

    private enum SettingsTab
    {
        Audio,
        Graphics,
        Gameplay,
        Controls,
        Bindings,
        Hud,
        Chat
    }

    private enum BindingVisibilityFilter
    {
        All,
        Assigned,
        Unassigned
    }

    private sealed class BindingEntry
    {
        public BindingEntry(
            ControllerMap controllerMap,
            ActionElementMap actionElementMap,
            InputAction action,
            string deviceKey,
            string controllerLabel,
            string categoryName,
            string displayName,
            string bindingName,
            string bindingKind,
            AxisRange actionRange)
        {
            ControllerMap = controllerMap;
            ActionElementMap = actionElementMap;
            ActionId = action.id;
            ActionType = action.type;
            DeviceKey = deviceKey;
            ControllerLabel = controllerLabel;
            CategoryName = categoryName;
            DisplayName = displayName;
            BindingName = bindingName;
            BindingKind = bindingKind;
            ActionRange = actionRange;
            IsAssigned = true;
            SortKey = $"{DeviceKey}|{CategoryName}|{DisplayName}|{ControllerLabel}|{BindingName}";
            Key = $"{DeviceKey}-{ActionId}-{ActionElementMap.id}";
        }

        public BindingEntry(
            ControllerMap controllerMap,
            InputAction action,
            string deviceKey,
            string controllerLabel,
            string categoryName,
            string displayName,
            string bindingName,
            string bindingKind,
            AxisRange actionRange)
        {
            ControllerMap = controllerMap;
            ActionId = action.id;
            ActionType = action.type;
            DeviceKey = deviceKey;
            ControllerLabel = controllerLabel;
            CategoryName = categoryName;
            DisplayName = displayName;
            BindingName = bindingName;
            BindingKind = bindingKind;
            ActionRange = actionRange;
            IsAssigned = false;
            SortKey = $"{DeviceKey}|{CategoryName}|{DisplayName}|{ControllerLabel}|{BindingName}";
            Key = $"{DeviceKey}-{ActionId}-Unassigned-{ActionRange}-{ControllerMap.id}";
        }

        public ControllerMap ControllerMap { get; }
        public ActionElementMap? ActionElementMap { get; }
        public int ActionId { get; }
        public InputActionType ActionType { get; }
        public string DeviceKey { get; }
        public string ControllerLabel { get; }
        public string CategoryName { get; }
        public string DisplayName { get; }
        public string BindingName { get; }
        public string BindingKind { get; }
        public AxisRange ActionRange { get; }
        public bool IsAssigned { get; }
        public string SortKey { get; }
        public string Key { get; }
        public bool CanInvert => ActionElementMap != null && ActionElementMap.elementType == ControllerElementType.Axis && ActionElementMap.axisRange == AxisRange.Full;
        public bool CanShowAxisValue => IsAssigned && ActionType == InputActionType.Axis;
        public bool IsInverted => ActionElementMap != null && ActionElementMap.invert;
    }

    private readonly struct BindingDeviceFilter
    {
        public BindingDeviceFilter(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; }
        public string Label { get; }
    }
}
