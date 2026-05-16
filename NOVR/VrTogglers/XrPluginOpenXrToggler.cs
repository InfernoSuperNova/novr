#if MODERN
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace NOVR.VrTogglers;

public class XrPluginOpenXrToggler : XrPluginToggler
{
    protected override XRLoader CreateLoader()
    {
        var xrLoader = ScriptableObject.CreateInstance<OpenXRLoader>();
        OpenXrRenderModeEnforcer.ApplyConfiguredSettings();
        return xrLoader;
    }
}
#endif 
