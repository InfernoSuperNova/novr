using System.Reflection;
using HarmonyLib;
using NOVR.PatchHelper;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.UI;

internal static class TMP_DropdownPatch
{
    private static readonly FieldInfo DropdownField = AccessTools.Field(typeof(TMP_Dropdown), "m_Dropdown");

    [PatchPostfix(typeof(TMP_Dropdown), nameof(TMP_Dropdown.Show))]
    private static void Show(TMP_Dropdown __instance)
    {
        if (__instance == null || !IsInVrLayerHierarchy(__instance.transform))
            return;

        var dropdown = (GameObject)DropdownField.GetValue(__instance);
        if (dropdown == null)
            return;
            
        LayerHelper.SetLayerRecursive(dropdown.transform, LayerHelper.Layers.VrUi);
            
        var mask = dropdown.gameObject.GetComponentInChildren<Mask>();
        if (mask) mask.enabled = false;
    }
    private static bool IsInVrLayerHierarchy(Transform transform)
    {
        var vrLayer = LayerHelper.Layers.VrUi;
        while (transform != null)
        {
            if (transform.gameObject.layer == (int)vrLayer)
                return true;
            
            transform = transform.parent;
        }

        return false;
    }
}
