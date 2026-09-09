#if MODERN
using System;
using UnityEngine;
using UnityEngine.XR.Management;

namespace NOVR.VrTogglers;

public abstract class XrPluginToggler: VrToggler
{
    protected XRGeneralSettings _generalSettings;
    protected XRManagerSettings _managerSetings;
    
    protected override bool SetUp()
    {
        _generalSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
        _managerSetings = ScriptableObject.CreateInstance<XRManagerSettings>();
        _generalSettings.Manager = _managerSetings;
        var loader = CreateLoader();
        XrStartupDiagnostics.LogBeforeInitialize(_managerSetings, loader);
        
        #pragma warning disable CS0618
        /*
         * ManagerSettings.loaders is deprecated but very useful, allows me to add the xr loader without reflection.
         * Should be fine unless the game's Unity version gets majorly updated, in which case the whole mod will be
         * broken, so I'll have to update it anyway.
         */
        _managerSetings.loaders.Add(loader);
        #pragma warning restore CS0618
        
        _managerSetings.InitializeLoaderSync();
        XrStartupDiagnostics.LogAfterInitialize(_managerSetings);
        if (_managerSetings.activeLoader == null) throw new Exception("Cannot initialize OpenXR Loader. Maybe The VR headset wasn't ready?");

        return true;
    }

    protected override bool EnableVr()
    {
        XrStartupDiagnostics.LogBeforeStart(_managerSetings);
        _managerSetings.StartSubsystems();
        XrStartupDiagnostics.LogAfterStart(_managerSetings);
        return _managerSetings.activeLoader != null;
    }

    protected override bool DisableVr()
    {
        if (_managerSetings.activeLoader == null) return true;

        XrStartupDiagnostics.LogBeforeStop(_managerSetings);
        _managerSetings.StopSubsystems();
        XrStartupDiagnostics.LogAfterStop(_managerSetings);
        _managerSetings.DeinitializeLoader();
        XrStartupDiagnostics.LogAfterDeinitialize(_managerSetings);
        return _managerSetings.activeLoader == null;
    }

    protected abstract XRLoader CreateLoader();
}
#endif
