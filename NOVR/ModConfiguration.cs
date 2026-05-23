using System.ComponentModel;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR;

public class ModConfiguration
{
    public static ModConfiguration Instance;

    public enum CameraTrackingMode
    {
        [Description("Absolute")] Absolute,
        [Description("Relative matrix")] RelativeMatrix,
#if MODERN
        // TODO: could add this for legacy too.
        [Description("Relative Transform")] RelativeTransform,
#endif
        [Description("Child")] Child,
    }

#if MODERN
    public enum VrApi
    {
        [Description("OpenVR")] OpenVr,
        [Description("OpenXR")] OpenXr,
    }
#endif

    public enum UiRenderMode
    {
        [Description("Overlay camera (draws on top of everything)")]
        OverlayCamera,

        [Description("In world (can be occluded)")]
        InWorld,
    }

    public enum RenderProfilerPatchMode
    {
        [Description("Unity lifecycle callbacks only")]
        LifecycleCallbacks,

        [Description("Every patchable method in selected assemblies")]
        AllMethods,
    }

    public readonly ConfigFile Config;
    public readonly ConfigEntry<bool> DisableUnityXrCameraAutoTracking; // Is used
    public readonly ConfigEntry<bool> AlignCameraToHorizon; // Is used
    public readonly ConfigEntry<bool> PhysicsMatchHeadsetRefreshRate; // Is used
    public readonly ConfigEntry<string> ObjectsToDeactivateByComponent; // Is used
    public readonly ConfigEntry<string> ComponentsToDisable; // Is used
    public readonly ConfigEntry<float> ComponentSearchInterval; // Is used

    public ModConfiguration(ConfigFile config)
    {
        Instance = this;

        Config = config;
        
        
        DisableUnityXrCameraAutoTracking = config.Bind(
            "Camera",
            "Disable Unity XR Camera Auto Tracking",
            true,
            "Disables Unity's automatic XR camera tracking so NOVR can drive camera rotation manually. Turn off to test whether Unity's native camera tracking fixes Single Pass Instanced rendering.");

        AlignCameraToHorizon = config.Bind(
            "Camera",
            "Align To Horizon",
            false,
            "Prevents pitch and roll changes on the camera, allowing only yaw changes.");
        
        PhysicsMatchHeadsetRefreshRate = config.Bind(
            "General",
            "Force physics rate to match headset refresh rate",
            false,
            "Can help fix jiterriness in games that rely a lot on physics. Might break a lot of games too.");

       
        ObjectsToDeactivateByComponent = config.Bind(
            "Fixes",
            "Objects to Deactivate by Component",
            "",
            "Any objects that contains one of these components gets deactivated. List of fully qualified C# type names, separated by /. Example: 'Canvas, UnityEngine/HUD, Assembly-CSharp'");

        ComponentsToDisable = config.Bind(
            "Fixes",
            "Components to Disable.",
            "",
            "Names of components to disable. List of fully qualified C# type names, separated by /. Example: 'Canvas, UnityEngine/HUD, Assembly-CSharp'");

        ComponentSearchInterval = config.Bind(
            "Fixes",
            "Component Search Interval",
            1f,
            new ConfigDescription("Value in seconds, the interval between searches for components to disable.",
                new AcceptableValueRange<float>(0.5f, 30f)));
    }
}
