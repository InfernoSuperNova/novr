using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR;

public class ModConfiguration
{
    public static ModConfiguration Instance;
    

    public readonly ConfigFile Config;
    public readonly ConfigEntry<float> TargetDesignatorOvershoot;
    public readonly ConfigEntry<bool> EnableNativeMenuUi;
    public readonly ConfigEntry<float> NativeMenuScale;
    public readonly ConfigEntry<float> NativeMenuDistance;
    public readonly ConfigEntry<float> NativeMenuHeightOffset;
    public readonly ConfigEntry<string> CursorInputMode;
    public readonly ConfigEntry<bool> EnableNativeMenuEnvironment;
    public readonly ConfigEntry<bool> EnableExperimentalSteamVrControllerProfiles;
    public readonly ConfigEntry<bool> LogXrStartupDiagnostics;
    public readonly ConfigEntry<bool> VerboseDiagnostics;
    public readonly ConfigEntry<float> CockpitHeadForwardOffset;
    public readonly ConfigEntry<float> CockpitHeadRightOffset;
    public readonly ConfigEntry<KeyCode> RecenterShortcut;
    public readonly ConfigEntry<bool> ShowRecenterInPauseMenu;
    public readonly ConfigEntry<bool> SavePositionTrigger;
    public readonly ConfigEntry<float> MapClickMaxRadius;
    public readonly ConfigEntry<float> HudMinimapOpacity;
    public readonly ConfigEntry<bool> CombineNovrHudCameras;
    public readonly ConfigEntry<bool> LogHudCameraConsolidation;
    public readonly ConfigEntry<bool> DiagnosticMissionAutoStart;
    public readonly ConfigEntry<string> DiagnosticMissionAutoStartQuery;
    public readonly ConfigEntry<float> DiagnosticMissionAutoStartDelaySeconds;

    private readonly Dictionary<string, (ConfigEntry<float> Forward, ConfigEntry<float> Right)> _perPlaneEntries = new();

    public ModConfiguration(ConfigFile config)
    {
        Instance = this;

        Config = config;
        TargetDesignatorOvershoot = config.Bind(
            "General",
            "Target Designator Overshoot",
            1.2f,
            "How much the target designator should multiply rotation to make for easier high off boresight target designation. Set to 1.0 to disable");

        EnableNativeMenuUi = config.Bind(
            "Experimental",
            "Enable Native Menu UI",
            true,
            "Use NOVR's native VR menu UI for non-flight menus. Disable to fall back to the existing patched game UI.");

        NativeMenuScale = config.Bind(
            "Experimental",
            "Native Menu Scale",
            1.25f,
            "Size multiplier for NOVR's native VR menu UI. Values from 0.75 to 2.0 are supported.");

        NativeMenuDistance = config.Bind(
            "Experimental",
            "Native Menu Distance",
            3.0f,
            "Distance in meters from the headset when NOVR's native VR menu UI is opened or recentered. Values from 1.5 to 6.0 are supported.");

        NativeMenuHeightOffset = config.Bind(
            "Experimental",
            "Native Menu Height Offset",
            0.0f,
            "Vertical offset in meters applied when NOVR's native VR menu UI is opened or recentered. Values from -0.25 to 1.0 are supported.");

        CursorInputMode = config.Bind(
            "Experimental",
            "Cursor Input Mode",
            "Auto",
            "Selects input source for the VR UI cursor. 'Auto' = use controller if tracked, else mouse. 'Mouse' = always use mouse. 'Controller' = always use controller ray.");

        EnableNativeMenuEnvironment = config.Bind(
            "Experimental",
            "Enable Native Menu Environment",
            false,
            "Show an experimental 3D native menu environment using real game preview assets.");

        EnableExperimentalSteamVrControllerProfiles = config.Bind(
            "Experimental",
            "Enable Experimental SteamVR Controller Profiles",
            false,
            "Register a minimal set of OpenXR controller interaction profiles before VR startup. Enables Valve Index, HTC Vive, and Khronos Simple Controller profiles only; hand tracking is not enabled.");

        LogXrStartupDiagnostics = config.Bind(
            "Diagnostics",
            "Log XR Startup Diagnostics",
            false,
            "Log read-only XR loader, OpenXR runtime, subsystem, and input device state during VR startup.");

        VerboseDiagnostics = config.Bind(
            "Diagnostics",
            "Verbose Diagnostics",
            false,
            "When enabled, NOVR emits per-frame and per-second diagnostic logs " +
            "(controller laser dumps, controller input pose dumps, cursor mode dumps, " +
            "asset-cache waiting messages). Disabled by default for performance — " +
            "enable only when troubleshooting.");

        CockpitHeadForwardOffset = config.Bind(
            "Experimental",
            "Cockpit Head Forward Offset",
            0.08f,
            "Offset in meters applied to the cockpit head forward vector. Helps keep the ejection seat bars out of your face.");

        CockpitHeadRightOffset = config.Bind(
            "Experimental",
            "Cockpit Head Right Offset",
            0.0f,
            "Offset in meters applied to the cockpit head right vector. Moves you left (negative) or right (positive) to correct off-center seating.");

        RecenterShortcut = config.Bind(
            "Input",
            "Recenter Shortcut",
            KeyCode.F9,
            "Keyboard shortcut to recenter the VR view. For HOTAS users, map a joystick button to this key via external software.");

        ShowRecenterInPauseMenu = config.Bind(
            "Input",
            "Show Recenter In Pause Menu",
            true,
            "Add a 'RECENTER VIEW' button to the in-game pause menu while seated in a cockpit. Clicking it recenters the VR view immediately.");

        SavePositionTrigger = config.Bind(
            "Experimental",
            "Save Position For Current Aircraft",
            false,
            "Check this box to save the current Cockpit Head Forward/Right Offset values for the aircraft you're currently in. Automatically unchecks itself.");

        MapClickMaxRadius = config.Bind(
            "Map",
            "Click Max Radius",
            0.0375f,
            "Maximum normalized distance from the VR cursor to a map icon for the icon to be selectable on click. Distance is measured as a fraction of the map image's smaller dimension, so it stays consistent regardless of zoom level, HUD scale, or HMD resolution. Values from 0.0 to 0.5 are supported.");

        HudMinimapOpacity = config.Bind(
            "HUD",
            "Minimap Opacity",
            1.0f,
            "Opacity of the in-cockpit minimap (the small map in the HUD, not the full clickable map). " +
            "1.0 is fully opaque, 0.0 hides it completely. Applies only when the minimap is shown; the full map view is unaffected.");

        CombineNovrHudCameras = config.Bind(
            "Performance",
            "Combine NOVR HUD Cameras",
            false,
            "Render normal and cockpit-clipped VR HUD layers through one NOVR stereo overlay " +
            "instead of two. Does not reduce headset refresh rate.");

        LogHudCameraConsolidation = config.Bind(
            "Diagnostics",
            "Log HUD Camera Consolidation",
            true,
            "Log one compact HUD/post-processing camera callback summary every 10 seconds while testing.");

        DiagnosticMissionAutoStart = config.Bind(
            "Diagnostics",
            "Diagnostic Mission Auto Start",
            true,
            "Automatically launch an offline single-player mission when menus are unusable during diagnostics.");

        DiagnosticMissionAutoStartQuery = config.Bind(
            "Diagnostics",
            "Diagnostic Mission Auto Start Query",
            "Furball",
            "Case-insensitive mission name fragment used by diagnostic auto-start.");

        DiagnosticMissionAutoStartDelaySeconds = config.Bind(
            "Diagnostics",
            "Diagnostic Mission Auto Start Delay Seconds",
            5.0f,
            "Real-time seconds after NOVR starts before attempting diagnostic mission auto-start.");

        SavePositionTrigger.SettingChanged += (_, _) =>
        {
            if (!SavePositionTrigger.Value) return;
            SavePositionTrigger.Value = false;

            if (string.IsNullOrEmpty(Core.CurrentAircraftId))
            {
                Debug.LogWarning("[NOVR] Cannot save cockpit offset: no aircraft is currently active.");
                return;
            }

            SaveCurrentOffsetFor(Core.CurrentAircraftId, CockpitHeadForwardOffset.Value, CockpitHeadRightOffset.Value);
        };

        PreloadPerPlaneEntries();
    }

    private void PreloadPerPlaneEntries()
    {
        if (!File.Exists(Config.ConfigFilePath)) return;

        var inPerPlaneSection = false;
        foreach (var rawLine in File.ReadAllLines(Config.ConfigFilePath))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                inPerPlaneSection = line.Equals("[PerPlaneOffsets]");
                continue;
            }

            if (!inPerPlaneSection) continue;

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0) continue;

            var key = line.Substring(0, equalsIndex).Trim();
            if (key.EndsWith("_Forward"))
            {
                GetOrCreatePlaneEntries(key.Substring(0, key.Length - "_Forward".Length));
            }
            else if (key.EndsWith("_Right"))
            {
                GetOrCreatePlaneEntries(key.Substring(0, key.Length - "_Right".Length));
            }
        }
    }

    private (ConfigEntry<float> Forward, ConfigEntry<float> Right) GetOrCreatePlaneEntries(string aircraftId)
    {
        if (_perPlaneEntries.TryGetValue(aircraftId, out var entries)) return entries;

        var forward = Config.Bind(
            "PerPlaneOffsets",
            $"{aircraftId}_Forward",
            float.NaN,
            "Saved Cockpit Head Forward Offset for this aircraft type. NaN means no value has been saved yet.");

        var right = Config.Bind(
            "PerPlaneOffsets",
            $"{aircraftId}_Right",
            float.NaN,
            "Saved Cockpit Head Right Offset for this aircraft type. NaN means no value has been saved yet.");

        entries = (forward, right);
        _perPlaneEntries[aircraftId] = entries;
        return entries;
    }

    public bool TryGetSavedOffset(string aircraftId, out float forward, out float right)
    {
        forward = 0f;
        right = 0f;
        if (string.IsNullOrEmpty(aircraftId)) return false;

        var entries = GetOrCreatePlaneEntries(aircraftId);
        if (float.IsNaN(entries.Forward.Value) || float.IsNaN(entries.Right.Value)) return false;

        forward = entries.Forward.Value;
        right = entries.Right.Value;
        return true;
    }

    public void SaveCurrentOffsetFor(string aircraftId, float forward, float right)
    {
        if (string.IsNullOrEmpty(aircraftId)) return;

        var entries = GetOrCreatePlaneEntries(aircraftId);
        entries.Forward.Value = forward;
        entries.Right.Value = right;
    }
}
