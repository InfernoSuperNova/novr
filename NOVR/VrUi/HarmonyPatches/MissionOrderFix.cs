using HarmonyLib;
using NuclearOption.SavedMission;

namespace NOVR.VrUi.HarmonyPatches;


// Fixes invisible mission select items
internal static class MissionOrderFix
{
    [HarmonyPatch(typeof(global::MissionSelectListItem), "Awake")]
    private static class AwakePatch
    {
        [HarmonyPrefix]
        private static void Prefix(global::MissionSelectListItem __instance)
        {
            __instance.transform.localPosition = __instance.transform.localPosition with { z = 0 };
            var text = __instance.transform.Find("Text (TMP)");
            text.localPosition = text.localPosition with { z = 0 };
        }
    }
}
