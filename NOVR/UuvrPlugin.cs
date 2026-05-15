using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using NOVR.Profiling;
using NOVR.VrCamera;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;

#if CPP
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
#endif

namespace NOVR;

[BepInPlugin(
    "deltawing.novr",
    "NOVR",
    "0.1.0")]
public class UuvrPlugin : BaseUnityPlugin
{
    private static UuvrPlugin _instance;
    public static string ModFolderPath { get; private set; }
     
    private void Awake()
    {
        _instance = this;
        ModFolderPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(UuvrPlugin)).Location);
        
        new ModConfiguration(Config);
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Core.Create();
    }
}
