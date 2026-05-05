using HarmonyLib;

namespace NOVR.VrUi.HarmonyPatches;


// Fixes invisible mission select items
internal static class TagFilterListOrderFix
{
    [HarmonyPatch(typeof(global::TagFilterListItem), "SetValue")]
    private static class AwakePatch
    {
        [HarmonyPrefix]
        private static void Prefix(global::TagFilterListItem __instance, TagFilterListItem.Item value)
        {
            __instance.transform.localPosition = __instance.transform.localPosition with { z = 0 };

        }
    }
}