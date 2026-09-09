using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace NOVR.Patches.VR;

/// <summary>
/// Ensures VR controller interaction profiles are registered with OpenXR before
/// the loader requests features. Without this, the OpenXR session won't bind an
/// interaction profile for Oculus/Meta Touch controllers, and no controller
/// devices will enumerate via any Unity API (InputDevices, Input System, InputTracking).
/// </summary>
[HarmonyPatch]
public static class OpenXRInteractionProfilePatches
{
    private static readonly Type[] RequiredProfiles = new[]
    {
        typeof(OculusTouchControllerProfile),
    };

    private static FieldInfo? _featuresField;
    private static FieldInfo? _enabledField;

    private static OpenXRFeature[] GetFeatures(OpenXRSettings instance)
    {
        _featuresField ??= typeof(OpenXRSettings).GetField("features",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return _featuresField?.GetValue(instance) as OpenXRFeature[] ?? Array.Empty<OpenXRFeature>();
    }

    private static void SetFeatures(OpenXRSettings instance, OpenXRFeature[] features)
    {
        _featuresField ??= typeof(OpenXRSettings).GetField("features",
            BindingFlags.Instance | BindingFlags.NonPublic);
        _featuresField?.SetValue(instance, features);
    }

    private static void SetFeatureEnabled(OpenXRFeature feature, bool enabled)
    {
        _enabledField ??= typeof(OpenXRFeature).GetField("m_enabled",
            BindingFlags.Instance | BindingFlags.NonPublic);
        _enabledField?.SetValue(feature, enabled);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(OpenXRLoaderBase), "RequestOpenXRFeatures")]
    private static void EnsureControllerProfiles()
    {
        var instance = OpenXRSettings.Instance;
        if (instance == null)
        {
            Debug.LogWarning("[NOVR] OpenXRSettings.Instance is null, cannot inject controller profiles.");
            return;
        }

        var existingFeatures = GetFeatures(instance);
        var existingTypes = new HashSet<Type>();
        foreach (var f in existingFeatures)
        {
            if (f != null)
                existingTypes.Add(f.GetType());
        }

        var toAdd = new List<OpenXRFeature>();
        foreach (var profileType in RequiredProfiles)
        {
            if (existingTypes.Contains(profileType))
                continue;

            var feature = ScriptableObject.CreateInstance(profileType) as OpenXRFeature;
            if (feature == null)
            {
                Debug.LogWarning($"[NOVR] Failed to create instance of {profileType.Name}");
                continue;
            }

            SetFeatureEnabled(feature, true);
            toAdd.Add(feature);
        }

        if (toAdd.Count > 0)
        {
            var newArray = new OpenXRFeature[existingFeatures.Length + toAdd.Count];
            existingFeatures.CopyTo(newArray, 0);
            for (int i = 0; i < toAdd.Count; i++)
                newArray[existingFeatures.Length + i] = toAdd[i];

            SetFeatures(instance, newArray);
            Debug.Log($"[NOVR] Injected {toAdd.Count} OpenXR interaction profile(s): {string.Join(", ", toAdd.Select(f => f.name))}");
        }
    }
}