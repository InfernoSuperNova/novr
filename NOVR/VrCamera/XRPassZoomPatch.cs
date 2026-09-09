using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace NOVR.VrCamera;

/// <summary>
/// URP renders XR views from XRPass rather than Camera's stereo projection properties.
/// Magnify the matrix at that final pipeline boundary without modifying the stored
/// OpenXR matrix, avoiding both silent Camera API no-ops and cumulative scaling.
/// </summary>
[HarmonyPatch(typeof(XRPass), nameof(XRPass.GetProjMatrix))]
internal static class XRPassZoomPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref Matrix4x4 __result)
    {
        var magnification = VrZoomController.Magnification;
        if (magnification <= 1f)
        {
            return;
        }

        // Preserve the asymmetric optical centre (m02/m12) supplied by OpenXR.
        __result.m00 *= magnification;
        __result.m11 *= magnification;
    }
}
