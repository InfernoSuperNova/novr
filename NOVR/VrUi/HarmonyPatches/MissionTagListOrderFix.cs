using HarmonyLib;
using NuclearOption.SavedMission;

namespace NOVR.VrUi.HarmonyPatches;


// Fixes invisible mission select items
internal static class MissionTagListOrderFix
{
    [HarmonyPatch(typeof(global::MissionTagListItem), "SetValue")]
    private static class AwakePatch
    {
        [HarmonyPrefix]
        private static void Prefix(global::MissionTagListItem __instance, MissionTag tag)
        {
            __instance.transform.localPosition = __instance.transform.localPosition with { z = 0 };

        }
    }
}