using System;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace NOVR.VrTogglers;

public class XrPluginOpenXrToggler : XrPluginToggler
{
    protected override bool SetUp()
    {
        if (ModConfiguration.Instance != null && ModConfiguration.Instance.EnableExperimentalSteamVrControllerProfiles.Value)
        {
            try
            {
                OpenXrControllerProfileBootstrap.ConfigureSteamVrControllerProfiles();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NOVR] Failed to configure experimental SteamVR OpenXR controller profiles. Continuing normal VR startup. Exception: {exception}");
            }
        }

        return base.SetUp();
    }

    protected override XRLoader CreateLoader()
    {
        var xrLoader = ScriptableObject.CreateInstance<OpenXRLoader>();
        return xrLoader;
    }
}
