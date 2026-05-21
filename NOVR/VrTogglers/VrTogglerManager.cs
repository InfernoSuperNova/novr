using System;
using BepInEx.Configuration;

namespace NOVR.VrTogglers;

public class VrTogglerManager
{
    private VrToggler _toggler;
    
    public VrTogglerManager()
    {
        SetUpToggler();
        _toggler?.SetVrEnabled(true);
    }

    private void SetUpToggler()
    {
        if (_toggler != null)
        {
            _toggler.SetVrEnabled(false);
        }
        _toggler = new XrPluginOpenXrToggler();
    }

    public void ToggleVr()
    {
        _toggler.SetVrEnabled(!_toggler.IsVrEnabled);
    }
}
