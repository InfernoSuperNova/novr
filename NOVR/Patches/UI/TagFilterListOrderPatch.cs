using NOVR.PatchHelper;

namespace NOVR.Patches.UI;

// Fixes invisible mission select items
internal static class TagFilterListOrderPatch
{
    [PatchPrefix(typeof(TagFilterListItem), "SetValue")]
    private static void SetValue(TagFilterListItem __instance, TagFilterListItem.Item value)
    {
        __instance.transform.localPosition = __instance.transform.localPosition with { z = 0 };

    }
}