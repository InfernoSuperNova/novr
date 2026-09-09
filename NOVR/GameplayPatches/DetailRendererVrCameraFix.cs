using System.Reflection;
using HarmonyLib;
using NuclearOption.Effects;
using UnityEngine;

namespace NOVR.GameplayPatches;

/// <summary>
/// Keeps Nuclear Option's GPU detail culling attached to NOVR's tracked camera.
/// </summary>
internal static class DetailRendererVrCameraFix
{
    private static readonly FieldInfo? DetailCameraField =
        AccessTools.Field(typeof(DetailRenderer), "camera");

    private static DetailRenderer? _boundRenderer;
    private static Camera? _boundCamera;
    private static bool _loggedMissingField;

    internal static void BindToTrackedCamera(Camera trackedCamera)
    {
        var detailRenderer = DetailRenderer.i;
        if (trackedCamera == null || detailRenderer == null)
        {
            return;
        }

        if (ReferenceEquals(_boundRenderer, detailRenderer) &&
            ReferenceEquals(_boundCamera, trackedCamera))
        {
            return;
        }

        if (DetailCameraField == null)
        {
            if (!_loggedMissingField)
            {
                Debug.LogError("[NOVR.TreeFix] Could not find DetailRenderer.camera; GPU detail culling was not rebound.");
                _loggedMissingField = true;
            }

            return;
        }

        DetailCameraField.SetValue(detailRenderer, trackedCamera);
        _boundRenderer = detailRenderer;
        _boundCamera = trackedCamera;

        Debug.Log(
            $"[NOVR.TreeFix] Bound DetailRenderer GPU detail culling to camera '{trackedCamera.gameObject.name}'.");
    }
}
