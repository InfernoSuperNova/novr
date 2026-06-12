using UnityEngine.UI;
using NOVR.PatchHelper;

namespace NOVR.Patches.HUD;

public class MFDScreenPatch
{
    [PatchPrefix(typeof(MFDScreen), nameof(MFDScreen.Setup))]
    private static void Setup(MFDScreen __instance)
    {
        __instance.GetComponent<Image>().enabled = false;
    }
}