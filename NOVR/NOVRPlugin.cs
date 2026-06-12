using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using NOVR.PatchHelper;
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
    "0.4.0")]
public class NOVRPlugin : BaseUnityPlugin
{
    
    private static NOVRPlugin _instance;
    public static string ModFolderPath { get; private set; }

    public NOVRPlugin()
    {
        
        InputTracking.trackingAcquired += TrackingAcquired;
        _instance = this;
        ModFolderPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(NOVRPlugin)).Location);
        
        new ModConfiguration(Config);
        var harm = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        PatchLoader.Apply(harm);
        NOVRShaders.Setup();
        Core.Create();
    }

    private void TrackingAcquired(XRNodeState obj)
    {
        NOVRHeadsetData.CalibrateTranslation();
    }
     
    private void Awake()
    {

    }
    
    
}
