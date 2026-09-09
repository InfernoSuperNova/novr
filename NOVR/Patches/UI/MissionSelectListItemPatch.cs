using NOVR.PatchHelper;
namespace NOVR.Patches.UI;

// Fixes invisible mission select items
internal static class MissionSelectListItemPatch
{
    [PatchPrefix(typeof(MissionSelectListItem), "Awake")]
    private static void Awake(MissionSelectListItem __instance)
    {
        __instance.transform.localPosition = __instance.transform.localPosition with { z = 0 };
        var text = __instance.transform.Find("Text (TMP)");
        text.localPosition = text.localPosition with { z = 0 };
    }
}
