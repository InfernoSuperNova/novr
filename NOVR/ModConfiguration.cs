using System.ComponentModel;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR;

public class ModConfiguration
{
    public static ModConfiguration Instance;
    

    public readonly ConfigFile Config;
    public readonly ConfigEntry<float> TargetDesignatorOvershoot;
    public readonly ConfigEntry<bool> DynamicMapVrCursorFixEnabled;
    public readonly ConfigEntry<bool> DynamicMapVrCursorDiagnosticsEnabled;

    public ModConfiguration(ConfigFile config)
    {
        Instance = this;

        Config = config;
        TargetDesignatorOvershoot = config.Bind(
            "General",
            "Target Designator Overshoot",
            1.2f,
            "How much the target designator should multiply rotation to make for easier high off boresight target designation. Set to 1.0 to disable");

        DynamicMapVrCursorFixEnabled = config.Bind(
            "UI",
            "Dynamic Map VR Cursor Fix Enabled",
            true,
            "Use NOVR's VR cursor position for DynamicMap hit tests that normally read UnityEngine.Input.mousePosition.");

        DynamicMapVrCursorDiagnosticsEnabled = config.Bind(
            "Diagnostics",
            "Dynamic Map VR Cursor Diagnostics Enabled",
            true,
            "Log DynamicMap VR cursor hit-test details when map icons are clicked or selected.");
    }
}
