using NOVR.PatchHelper;
using NuclearOption.SavedMission;

namespace NOVR.Patches.UI;

internal static class MissionTagListItemPatch
{
    [PatchPrefix(typeof(MissionTagListItem), "SetValue")]
    private static void SetValue(MissionTagListItem __instance, MissionTag tag)
    {
        __instance.transform.localPosition = __instance.transform.localPosition with { z = 0 };

    }

}