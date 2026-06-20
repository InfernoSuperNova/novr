using System.ComponentModel;
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
    public readonly ConfigEntry<bool> EnableNativeMenuEnvironment;
    public readonly ConfigEntry<bool> EnableExperimentalSteamVrControllerProfiles;
    public readonly ConfigEntry<bool> LogXrStartupDiagnostics;
    public readonly ConfigEntry<float> CockpitHeadForwardOffset;
    public readonly ConfigEntry<float> CockpitHeadRightOffset;
    public readonly ConfigEntry<KeyCode> RecenterShortcut;

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

        CockpitHeadForwardOffset = config.Bind(
            "Experimental",
            "Cockpit Head Forward Offset",
            0.05f,
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
    }
}
