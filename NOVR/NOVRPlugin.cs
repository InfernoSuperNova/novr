using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using NOVR.Profiling;
using NOVR.VrCamera;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

#if CPP
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
#endif

namespace NOVR;

[BepInPlugin(
    "deltawing.novr",
    "NOVR",
    "0.2.0")]
public class NOVRPlugin : BaseUnityPlugin
{
    private static NOVRPlugin _instance;
    public static string ModFolderPath { get; private set; }

    public NOVRPlugin()
    {
        InputTracking.trackingAcquired += TrackingAcquired;
    }

    private void TrackingAcquired(XRNodeState obj)
    {
        NOVRPoseDriver.Calibrate();
    }
     
    private void Awake()
    {
        _instance = this;
        ModFolderPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(NOVRPlugin)).Location);
        
        new ModConfiguration(Config);
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Core.Create();
    }
}
