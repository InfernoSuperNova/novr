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
    public readonly ConfigEntry<CameraTrackingMode> CameraTracking;
    public readonly ConfigEntry<bool> RelativeCameraSetStereoView;
    public readonly ConfigEntry<bool> DisableUnityXrCameraAutoTracking;
    public readonly ConfigEntry<int> VrCameraDepth;
    public readonly ConfigEntry<int> VrUiLayerOverride;
    public readonly ConfigEntry<Vector3> VrUiPosition;
    public readonly ConfigEntry<float> VrUiScale;
    public readonly ConfigEntry<string> VrUiShader;
    public readonly ConfigEntry<int> VrUiRenderQueue;
    public readonly ConfigEntry<bool> AlignCameraToHorizon;
    public readonly ConfigEntry<float> CameraPositionOffsetX;
    public readonly ConfigEntry<float> CameraPositionOffsetY;
    public readonly ConfigEntry<float> CameraPositionOffsetZ;
    public readonly ConfigEntry<bool> OverrideDepth;
    public readonly ConfigEntry<bool> PhysicsMatchHeadsetRefreshRate;
    public readonly ConfigEntry<UiRenderMode> PreferredUiRenderMode;
    public readonly ConfigEntry<string> VrUiCanvasNames;
    public readonly ConfigEntry<string> ObjectsToDeactivateByComponent;
    public readonly ConfigEntry<string> ComponentsToDisable;
    public readonly ConfigEntry<float> ComponentSearchInterval;
    public readonly ConfigEntry<bool> RenderProfilerEnabled;
    public readonly ConfigEntry<RenderProfilerPatchMode> RenderProfilerPatchModeSetting;
    public readonly ConfigEntry<string> RenderProfilerAssemblies;
    public readonly ConfigEntry<string> RenderProfilerMethodNames;
    public readonly ConfigEntry<float> RenderProfilerReportInterval;
    public readonly ConfigEntry<int> RenderProfilerMaxReportRows;
    public readonly ConfigEntry<bool> RenderProfilerMainThreadOnly;
    public readonly ConfigEntry<bool> RenderProfilerIncludeNovr;
    public readonly ConfigEntry<string> RenderProfilerTypeNameContains;
    public readonly ConfigEntry<string> RenderProfilerExcludeTypeNameContains;
    public readonly ConfigEntry<string> RenderProfilerMethodNameContains;
    public readonly ConfigEntry<string> RenderProfilerExcludeMethodNameContains;
    public readonly ConfigEntry<bool> RenderProfilerPatchConstructors;
    public readonly ConfigEntry<bool> RenderProfilerLogEachPatch;
    public readonly ConfigEntry<bool> OpenXrSymmetricProjection;

#if MODERN
    public readonly ConfigEntry<VrApi> PreferredVrApi;
#endif

    public ModConfiguration(ConfigFile config)
    {
        Instance = this;

        Config = config;

#if MODERN
        PreferredVrApi = config.Bind(
            "General",
            "Preferred VR APi",
            VrApi.OpenXr,
            "VR API to use. Depending on the game, some APIs might be unavailable, so UUVR will fall back to one that works.");
#endif

        CameraTracking = config.Bind(
            "Camera",
            "Camera Tracking Mode",
#if LEGACY
            CameraTrackingMode.RelativeMatrix,
#else
            CameraTrackingMode.RelativeTransform,
#endif
            "Defines how camera tracking is done. Relative is usually preferred, but not all games support it. Changing this might require restarting the level.");

        RelativeCameraSetStereoView = config.Bind(
            "Relative Camera",
            "Use SetStereoView for Relative Camera",
            false,
            "Some games are better with this on, some are better with this off. Just try it and see which one is better. Changing this might require restarting the level.");

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

        CameraPositionOffsetX = config.Bind(
            "Camera",
            "Camera Position Offset X",
            0f,
            "Changes position of tracked VR cameras");

        CameraPositionOffsetY = config.Bind(
            "Camera",
            "Camera Position Offset Y",
            0f,
            "Changes position of tracked VR cameras");

        CameraPositionOffsetZ = config.Bind(
            "Camera",
            "Camera Position Offset Z",
            0f,
            "Changes position of tracked VR cameras");

        OverrideDepth = config.Bind(
            "Camera",
            "Override Depth",
            false,
            "In some games, the VR camera won't display anything unless we override the camera depth value.");

        VrCameraDepth = config.Bind(
            "Camera",
            "Depth Value",
            1,
            new ConfigDescription(
                "Requires enabling 'Override Depth'. Range is -100 to 100, but you should try to find the lowest value that fixes visibility.",
                new AcceptableValueRange<int>(-100, 100)));

        PhysicsMatchHeadsetRefreshRate = config.Bind(
            "General",
            "Force physics rate to match headset refresh rate",
            false,
            "Can help fix jiterriness in games that rely a lot on physics. Might break a lot of games too.");

        VrUiLayerOverride = config.Bind(
            "UI",
            "VR UI Layer Override",
            -1,
            new ConfigDescription(
                "Layer to use for VR UI. By default (value -1) UUVR falls back to an unused (unnamed) layer.",
                new AcceptableValueRange<int>(-1, 31)));

        VrUiPosition = config.Bind(
            "UI",
            "VR UI Position",
            Vector3.forward * 1f,
            "Position of the VR UI projection relative to the camera.");

        VrUiScale = config.Bind(
            "UI",
            "VR UI Scale",
            1f,
            "Scale of the VR UI projection.");

        VrUiShader = config.Bind(
            "UI",
            "VR UI Shader",
            "",
            "Name of shader to use for the VR UI projection (passed to Unity's Shader.Find). Leave empty to let UUVR pick for you.");

        VrUiRenderQueue = config.Bind(
            "UI",
            "VR UI Render Queue",
            5000,
            new ConfigDescription(
                "Render queue to use for the VR UI projection. Default is 5000, which is the same as Unity's default canvas material.",
                new AcceptableValueRange<int>(0, 5000)));

        VrUiCanvasNames = config.Bind(
            "UI",
            "VR UI Canvas Names",
            "MainCanvas",
            "Slash-separated list of game canvas names to render into the VR UI texture. Example: 'MainCanvas/PauseCanvas'");

        PreferredUiRenderMode = config.Bind(
            "UI",
            "Preferred UI Plane Render Mode",
#if MODERN
            UiRenderMode.InWorld,
#else
            // Ideally we'd do overlay in all games but that mode can cause a lot of issues.
            // Most of the issues seem to be in more recent games, so at least for legacy we can default to overlay.
            UiRenderMode.OverlayCamera,
#endif
            "How to render the VR UI Plane. Overlay is usually better, but doesn't work in every game.");

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

        RenderProfilerEnabled = config.Bind(
            "Profiler",
            "Render Profiler Enabled",
            false,
            "Enables NOVR's Harmony-based render profiler. This can be very slow; leave off unless actively profiling.");

        RenderProfilerPatchModeSetting = config.Bind(
            "Profiler",
            "Render Profiler Patch Mode",
            RenderProfilerPatchMode.LifecycleCallbacks,
            "LifecycleCallbacks patches common Unity callbacks. AllMethods attempts to patch every patchable method in selected assemblies.");

        RenderProfilerAssemblies = config.Bind(
            "Profiler",
            "Render Profiler Assemblies",
            "Assembly-CSharp",
            "Slash-separated assembly names to patch. Example: 'Assembly-CSharp/NOVR'.");

        RenderProfilerMethodNames = config.Bind(
            "Profiler",
            "Render Profiler Method Names",
            "Update/LateUpdate/FixedUpdate/OnGUI/OnPreCull/OnPreRender/OnPostRender/OnRenderObject/OnWillRenderObject/OnRenderImage",
            "Slash-separated method names to patch when patch mode is LifecycleCallbacks.");

        RenderProfilerReportInterval = config.Bind(
            "Profiler",
            "Render Profiler Report Interval",
            2f,
            new ConfigDescription("Seconds between profiler log reports.",
                new AcceptableValueRange<float>(0.25f, 60f)));

        RenderProfilerMaxReportRows = config.Bind(
            "Profiler",
            "Render Profiler Max Report Rows",
            40,
            new ConfigDescription("Maximum number of slow methods to log per report.",
                new AcceptableValueRange<int>(1, 500)));

        RenderProfilerMainThreadOnly = config.Bind(
            "Profiler",
            "Render Profiler Main Thread Only",
            true,
            "Only records samples taken on Unity's main thread. Usually what you want for render/update profiling.");

        RenderProfilerIncludeNovr = config.Bind(
            "Profiler",
            "Render Profiler Include NOVR",
            false,
            "Include NOVR methods in patching/reporting. Usually leave off to focus on the game.");

        RenderProfilerTypeNameContains = config.Bind(
            "Profiler",
            "Render Profiler Type Name Contains",
            "",
            "Optional slash-separated type-name substrings to include. Empty includes all types in selected assemblies.");

        RenderProfilerExcludeTypeNameContains = config.Bind(
            "Profiler",
            "Render Profiler Exclude Type Name Contains",
            "<>c/<PrivateImplementationDetails>/NuclearOption.SceneLoading/NuclearOption.DedicatedServer/NuclearOption.SavedMission",
            "Slash-separated type-name substrings to exclude from patching.");

        RenderProfilerMethodNameContains = config.Bind(
            "Profiler",
            "Render Profiler Method Name Contains",
            "",
            "Optional slash-separated method-name substrings to include. Empty includes all methods that pass the current patch mode.");

        RenderProfilerExcludeMethodNameContains = config.Bind(
            "Profiler",
            "Render Profiler Exclude Method Name Contains",
            "CombatAI.LookForJammingTargets",
            "Slash-separated formatted method-name substrings to exclude from patching. Use entries like 'TypeName.MethodName' or just 'MethodName'.");

        RenderProfilerPatchConstructors = config.Bind(
            "Profiler",
            "Render Profiler Patch Constructors",
            false,
            "Patch constructors/static constructors in AllMethods mode. Risky; leave off unless needed.");

        RenderProfilerLogEachPatch = config.Bind(
            "Profiler",
            "Render Profiler Log Each Patch",
            true,
            "Log before and after every patched method. Useful because the last 'Patching...' line identifies a hard-crash method.");

#if MODERN
        OpenXrSymmetricProjection = config.Bind(
            "OpenXR",
            "Symmetric Projection",
            false,
            "Sets OpenXR symmetric projection before display subsystem creation. Useful as an SPI isolation test.");

#endif
    }
}
