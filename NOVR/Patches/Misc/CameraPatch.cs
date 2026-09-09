using NOVR.PatchHelper;
using UnityEngine;

namespace NOVR.Patches.Misc;

public class CameraPatch
{
    [PatchPrefix(typeof(Camera), "set_fieldOfView")]
    private static bool PreventChangingFov()
    {
        return false;
    }
}