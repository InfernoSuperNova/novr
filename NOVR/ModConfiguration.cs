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
    public readonly ConfigEntry<float> ZoomSpeed;
    public readonly ConfigEntry<float> MaximumZoom;
    public readonly ConfigEntry<bool> InstantZoomOut;

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

        ZoomSpeed = config.Bind(
            "VR Zoom",
            "Zoom Speed",
            2.0f,
            new ConfigDescription(
                "How quickly headset magnification changes while Zoom View is held, in magnification units per second.",
                new AcceptableValueRange<float>(0.1f, 20.0f)));

        MaximumZoom = config.Bind(
            "VR Zoom",
            "Maximum Zoom",
            4.0f,
            new ConfigDescription(
                "Maximum binocular-style headset magnification.",
                new AcceptableValueRange<float>(1.0f, 10.0f)));

        InstantZoomOut = config.Bind(
            "VR Zoom",
            "Instant Zoom Out",
            false,
            "When enabled, any Zoom View out input immediately returns the headset view to 1x magnification.");
    }
}
