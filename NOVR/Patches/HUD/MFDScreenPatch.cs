using NOVR.PatchHelper;

namespace NOVR.Patches.HUD;

public class MFDScreenPatch
{
    [PatchPrefix(typeof(MFDScreen), "Setup")]
    private static void Setup(MFDScreen __instance)
    {
        __instance.highlight.enabled = false;
    }
}