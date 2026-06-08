using System;
using System.Collections.Generic;
using NuclearOption.Effects;
using Rewired;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeSettingsPanel : MonoBehaviour
{
    private const float RowStartY = 270f;
    private const float RowSpacing = 42f;
    private const int BindingPageSize = 10;
    private const float BindingCaptureDelaySeconds = 0.25f;
    private const float BindingCaptureTimeoutSeconds = 8f;
    private const string AllBindingsFilterKey = "ALL";

    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.93f);
    private static readonly Color PanelColor = new(0.05f, 0.06f, 0.065f, 0.94f);
    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);
    private static readonly Color ButtonSelectedColor = new(0.44f, 0.49f, 0.50f, 1f);
    private static readonly Color ButtonHoverColor = new(0.34f, 0.40f, 0.42f, 1f);
    private static readonly Color ButtonPressedColor = new(0.16f, 0.20f, 0.22f, 1f);
    private static readonly Color BackButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);
    private static readonly Color ApplyButtonColor = new(0.12f, 0.34f, 0.20f, 0.96f);

    private readonly Dictionary<SettingsTab, Button> _tabButtons = new();
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
    private int _bindingPage;
    private int _bindingDeviceFilterIndex;
    private BindingVisibilityFilter _bindingVisibilityFilter = BindingVisibilityFilter.All;
    private bool _bindingEntriesDirty = true;
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
        if (_container.gameObject.activeSelf != visible)
        {
            _container.gameObject.SetActive(visible);
        }
    }

    private void Update()
    {
        if (_queuedBinding == null || Time.unscaledTime < _queuedBindingStartTime) return;

        var binding = _queuedBinding;
        _queuedBinding = null;
        StartBindingCapture(binding);
    }

    private void BuildLayout(RectTransform root)
    {
        _container = CreateContainer("Native Settings", root, root.sizeDelta);
        CreateImage("Background", _container, BackgroundColor, Vector2.zero, _container.sizeDelta);
        CreateText("Header", _container, "SETTINGS", new Vector2(0f, 395f), new Vector2(1000f, 34f), 22, TextAnchor.MiddleCenter, Color.white);

        var tabPanel = CreatePanel("Settings Tabs", _container, PanelColor, new Vector2(-610f, 0f), new Vector2(260f, 760f));
        CreateText("Tab Header", tabPanel, "CATEGORY", new Vector2(0f, 330f), new Vector2(220f, 30f), 17, TextAnchor.MiddleCenter, Color.white);

        AddTabButton(tabPanel, SettingsTab.Audio, "AUDIO", 265f);
        AddTabButton(tabPanel, SettingsTab.Graphics, "GRAPHICS", 211f);
        AddTabButton(tabPanel, SettingsTab.Gameplay, "GAMEPLAY", 157f);
        AddTabButton(tabPanel, SettingsTab.Controls, "CONTROLS", 103f);
        AddTabButton(tabPanel, SettingsTab.Bindings, "BINDINGS", 49f);
        AddTabButton(tabPanel, SettingsTab.Hud, "HUD", -5f);
        AddTabButton(tabPanel, SettingsTab.Chat, "CHAT", -59f);

        _contentRoot = CreatePanel("Settings Content", _container, PanelColor, new Vector2(150f, 0f), new Vector2(1040f, 760f));
        CreateMenuButton("BACK", _container, new Vector2(-690f, -405f), new Vector2(170f, 40f), BackButtonColor, BackToMainMenu, 15);
        CreateMenuButton("APPLY", _container, new Vector2(610f, -405f), new Vector2(170f, 40f), ApplyButtonColor, ApplyAndSave, 15);

        SetActiveTabButtonColors();
        RenderCurrentTab();
        _container.gameObject.SetActive(false);
    }

    private void AddTabButton(RectTransform parent, SettingsTab tab, string label, float y)
    {
        var button = CreateMenuButton(label, parent, new Vector2(0f, y), new Vector2(200f, 38f), ButtonColor, () => SelectTab(tab), 15);
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

        for (var index = _contentRoot.childCount - 1; index >= 0; index--)
        {
            Destroy(_contentRoot.GetChild(index).gameObject);
        }
    }

    private void CreateContentTitle(string title)
    {
        if (_contentRoot == null) return;

        CreateText($"{title} Title", _contentRoot, title, new Vector2(0f, 330f), new Vector2(960f, 32f), 20, TextAnchor.MiddleCenter, Color.white);
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

        _nextRowY = 172f;
        var pageCount = Mathf.Max(1, Mathf.CeilToInt(visibleEntries.Count / (float)BindingPageSize));
        _bindingPage = ClampInt(_bindingPage, 0, pageCount - 1);

        var startIndex = _bindingPage * BindingPageSize;
        var endIndex = Mathf.Min(startIndex + BindingPageSize, visibleEntries.Count);
        for (var index = startIndex; index < endIndex; index++)
        {
            AddBindingRow(visibleEntries[index]);
        }

        var footerY = -300f;
        CreateMenuButton("<", _contentRoot, new Vector2(-165f, footerY), new Vector2(56f, 32f), ButtonColor, () =>
        {
            _bindingPage = ClampInt(_bindingPage - 1, 0, pageCount - 1);
            RenderCurrentTab();
        }, 16);
        CreateText("Bindings Page", _contentRoot, $"PAGE {_bindingPage + 1} / {pageCount}", new Vector2(0f, footerY), new Vector2(210f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", _contentRoot, new Vector2(165f, footerY), new Vector2(56f, 32f), ButtonColor, () =>
        {
            _bindingPage = ClampInt(_bindingPage + 1, 0, pageCount - 1);
            RenderCurrentTab();
        }, 16);

        if (_queuedBinding != null || _activeBinding != null)
        {
            CreateMenuButton("CANCEL", _contentRoot, new Vector2(410f, footerY), new Vector2(120f, 32f), BackButtonColor, () =>
            {
                CancelBindingCapture("Binding capture canceled.");
                RenderCurrentTab();
            }, 13);
        }

        CreateText("Bindings Status", _contentRoot, _bindingStatus, new Vector2(0f, -345f), new Vector2(880f, 30f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));
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

        _bindingPage = 0;
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
        _bindingPage = 0;
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
            return new BindingDeviceFilter(AllBindingsFilterKey, "ALL DEVICES");
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
            if ((filter.Key == AllBindingsFilterKey || entry.DeviceKey == filter.Key) && IsBindingVisible(entry))
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

    private void AddBindingRow(BindingEntry entry)
    {
        if (_contentRoot == null) return;

        var y = ConsumeRowY();
        var pending = ReferenceEquals(_queuedBinding, entry) || ReferenceEquals(_activeBinding, entry);
        var remapLabel = pending ? "..." : entry.IsAssigned ? "REMAP" : "ASSIGN";
        var bindingColor = entry.IsAssigned ? Color.white : new Color(0.96f, 0.82f, 0.42f, 1f);

        CreateText($"{entry.Key} Action", _contentRoot, entry.DisplayName, new Vector2(-385f, y), new Vector2(300f, 32f), 13, TextAnchor.MiddleLeft, Color.white);
        CreateText($"{entry.Key} Source", _contentRoot, $"{entry.ControllerLabel} {entry.BindingKind}", new Vector2(-110f, y), new Vector2(210f, 32f), 11, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.86f, 1f));
        CreateText($"{entry.Key} Binding", _contentRoot, entry.BindingName, new Vector2(135f, y), new Vector2(260f, 32f), 13, TextAnchor.MiddleCenter, bindingColor);

        if (entry.CanInvert)
        {
            CreateMenuButton(entry.IsInverted ? "INV ON" : "INV", _contentRoot, new Vector2(315f, y), new Vector2(80f, 32f), entry.IsInverted ? ApplyButtonColor : ButtonColor, () => ToggleBindingInvert(entry), 11);
        }

        CreateMenuButton(remapLabel, _contentRoot, new Vector2(430f, y), new Vector2(105f, 32f), pending ? ButtonSelectedColor : ButtonColor, () => QueueBindingCapture(entry), 12);
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

        _bindingEntriesDirty = false;
        _bindingEntries.Clear();
        _bindingDeviceFilters.Clear();
        _bindingDeviceFilters.Add(new BindingDeviceFilter(AllBindingsFilterKey, "ALL DEVICES"));

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
            _bindingDeviceFilterIndex = ClampInt(_bindingDeviceFilterIndex, 0, _bindingDeviceFilters.Count - 1);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to load control bindings: {exception}");
            _bindingEntries.Clear();
            _bindingDeviceFilters.Clear();
            _bindingDeviceFilters.Add(new BindingDeviceFilter(AllBindingsFilterKey, "ALL DEVICES"));
            _bindingDeviceFilterIndex = 0;
        }
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

        var assignedActionIds = new HashSet<int>();
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
                    assignedActionIds.Add(mappedAction.id);
                }

                if (!preferredMapByActionCategory.ContainsKey(mappedAction.categoryId))
                {
                    preferredMapByActionCategory.Add(mappedAction.categoryId, controllerMap);
                }
            }
        }

        foreach (var action in ReInput.mapping.UserAssignableActions)
        {
            if (action == null || !action.userAssignable || assignedActionIds.Contains(action.id)) continue;

            if (!preferredMapByActionCategory.TryGetValue(action.categoryId, out var controllerMap))
            {
                controllerMap = fallbackMap;
            }

            _bindingEntries.Add(new BindingEntry(
                controllerMap,
                action,
                deviceKey,
                controllerLabel,
                GetActionCategoryName(action),
                GetActionDisplayName(action),
                "UNASSIGNED",
                GetDefaultBindingKind(action),
                GetDefaultActionRange(action)));
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

    private static string GetDefaultBindingKind(InputAction action)
    {
        return action.type == InputActionType.Axis ? "AXIS" : "BTN";
    }

    private static AxisRange GetDefaultActionRange(InputAction action)
    {
        return action.type == InputActionType.Axis ? AxisRange.Full : AxisRange.Positive;
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

        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        colors.selectedColor = ButtonHoverColor;
        colors.disabledColor = new Color(0.16f, 0.18f, 0.19f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

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

        var colors = button.colors;
        colors.normalColor = color;
        button.colors = colors;
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
            DeviceKey = deviceKey;
            ControllerLabel = controllerLabel;
            CategoryName = categoryName;
            DisplayName = displayName;
            BindingName = bindingName;
            BindingKind = bindingKind;
            ActionRange = actionRange;
            IsAssigned = false;
            SortKey = $"{DeviceKey}|{CategoryName}|{DisplayName}|{ControllerLabel}|{BindingName}";
            Key = $"{DeviceKey}-{ActionId}-Unassigned-{ControllerMap.id}";
        }

        public ControllerMap ControllerMap { get; }
        public ActionElementMap? ActionElementMap { get; }
        public int ActionId { get; }
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
