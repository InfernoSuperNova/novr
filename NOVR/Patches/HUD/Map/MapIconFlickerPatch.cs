using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.HUD.Map;

/// <summary>
/// Suppresses tactical map icon rendering on the initial frame(s) after spawning
/// until the icon's correct map position has been calculated and set.
/// </summary>
internal static class MapIconFlickerPatch
{
    private static readonly ConditionalWeakTable<global::MapIcon, object> _pendingFirstUpdate = new();
    private static readonly object _dummyValue = new();

    [HarmonyPatch(typeof(global::UnitMapIcon), "SetIcon")]
    private static class UnitMapIconSetIconPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::UnitMapIcon __instance)
        {
            if (__instance != null && __instance.iconImage != null)
            {
                __instance.iconImage.enabled = false;
                _pendingFirstUpdate.Remove(__instance);
                _pendingFirstUpdate.Add(__instance, _dummyValue);
            }
        }
    }

    [HarmonyPatch(typeof(global::UnitMapIcon), "UpdateIcon")]
    private static class UnitMapIconUpdateIconPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::UnitMapIcon __instance)
        {
            if (__instance != null && __instance.iconImage != null)
            {
                if (_pendingFirstUpdate.TryGetValue(__instance, out _))
                {
                    _pendingFirstUpdate.Remove(__instance);
                    __instance.iconImage.enabled = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(global::AirbaseMapIcon), "SetIcon")]
    private static class AirbaseMapIconSetIconPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::AirbaseMapIcon __instance)
        {
            if (__instance != null && __instance.iconImage != null)
            {
                __instance.iconImage.enabled = false;
                _pendingFirstUpdate.Remove(__instance);
                _pendingFirstUpdate.Add(__instance, _dummyValue);
            }
        }
    }

    [HarmonyPatch(typeof(global::AirbaseMapIcon), "UpdateIcon")]
    private static class AirbaseMapIconUpdateIconPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::AirbaseMapIcon __instance)
        {
            if (__instance != null && __instance.iconImage != null)
            {
                if (_pendingFirstUpdate.TryGetValue(__instance, out _))
                {
                    _pendingFirstUpdate.Remove(__instance);
                    __instance.iconImage.enabled = true;
                }
            }
        }
    }
}
