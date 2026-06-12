using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.OpenXR.Input;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace NOVR;

internal static class OpenXrControllerProfileBootstrap
{
    private static readonly FieldInfo? FeaturesField = typeof(OpenXRSettings)
        .GetField("features", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly ProfileSpec[] SteamVrControllerProfiles =
    {
        new(
            "UnityEngine.XR.OpenXR.Features.Interactions.ValveIndexControllerProfile, Unity.XR.OpenXR",
            "Valve Index Controller Profile"),
        new(
            "UnityEngine.XR.OpenXR.Features.Interactions.HTCViveControllerProfile, Unity.XR.OpenXR",
            "HTC Vive Controller Profile"),
        new(
            "UnityEngine.XR.OpenXR.Features.Interactions.KHRSimpleControllerProfile, Unity.XR.OpenXR",
            "Khronos Simple Controller Profile")
    };

    public static int ConfigureSteamVrControllerProfiles()
    {
        RegisterOpenXrInputSystemSupportLayouts();

        var settings = OpenXRSettings.Instance;
        if (settings == null)
        {
            Debug.LogWarning("[NOVR] OpenXR controller profile bootstrap skipped because OpenXRSettings.Instance is unavailable.");
            return 0;
        }

        if (FeaturesField == null)
        {
            Debug.LogWarning("[NOVR] OpenXR controller profile bootstrap skipped because OpenXRSettings.features could not be found.");
            return 0;
        }

        var features = ReadFeatures(settings);
        var configuredCount = 0;

        foreach (var profile in SteamVrControllerProfiles)
        {
            if (EnsureProfileFeature(features, profile))
            {
                configuredCount++;
            }
        }

        var dedupedFeatures = features
            .Where(feature => feature != null)
            .GroupBy(feature => feature.GetType())
            .Select(group => group.First())
            .OrderByDescending(feature => ReadIntFeatureField(feature, "priority"))
            .ThenBy(feature => ReadStringFeatureField(feature, "nameUi"))
            .ToArray();

        FeaturesField.SetValue(settings, dedupedFeatures);

        Debug.Log($"[NOVR] OpenXR controller profile bootstrap configured {configuredCount} SteamVR-compatible controller profiles. OpenXRSettings featureCount={settings.featureCount}.");
        return configuredCount;
    }

    private static void RegisterOpenXrInputSystemSupportLayouts()
    {
        try
        {
            InputSystem.RegisterLayout<HapticControl>("Haptic");
            Debug.Log("[NOVR] Registered OpenXR InputSystem support layout 'Haptic'.");

            InputSystem.RegisterLayout<PoseControl>("Pose");
            Debug.Log("[NOVR] Registered OpenXR InputSystem support layout 'Pose'.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Failed to register OpenXR InputSystem support layouts. Continuing controller profile bootstrap. Exception: {exception}");
        }
    }

    private static List<OpenXRFeature> ReadFeatures(OpenXRSettings settings)
    {
        var features = FeaturesField?.GetValue(settings) as OpenXRFeature[];
        return features?
            .Where(feature => feature != null)
            .ToList() ?? new List<OpenXRFeature>();
    }

    private static bool EnsureProfileFeature(List<OpenXRFeature> features, ProfileSpec profile)
    {
        var featureType = Type.GetType(profile.TypeName, throwOnError: false);
        if (featureType == null)
        {
            Debug.LogWarning($"[NOVR] OpenXR controller profile bootstrap could not find feature type '{profile.TypeName}'.");
            return false;
        }

        if (!typeof(OpenXRFeature).IsAssignableFrom(featureType))
        {
            Debug.LogWarning($"[NOVR] OpenXR controller profile bootstrap skipped '{featureType.FullName}' because it is not an OpenXRFeature.");
            return false;
        }

        var feature = features.FirstOrDefault(existingFeature => existingFeature.GetType() == featureType);
        if (feature == null)
        {
            feature = (OpenXRFeature)ScriptableObject.CreateInstance(featureType);
            feature.name = profile.UiName;
            features.Add(feature);
        }

        ApplyFeatureMetadata(feature, profile);
        feature.enabled = true;
        return true;
    }

    private static void ApplyFeatureMetadata(OpenXRFeature feature, ProfileSpec profile)
    {
        SetFeatureField(feature, "nameUi", profile.UiName);
        SetFeatureField(feature, "version", "0.0.1");
        SetFeatureField(feature, "company", "Unity");
        SetFeatureField(feature, "featureIdInternal", ReadFeatureId(feature.GetType()));
        SetFeatureField(feature, "openxrExtensionStrings", "");
        SetFeatureField(feature, "targetOpenXRApiVersion", "");
        SetFeatureField(feature, "required", false);
        SetFeatureField(feature, "priority", 0);
        SetFeatureField(feature, "customRuntimeLoaderName", null);
    }

    private static string ReadFeatureId(Type featureType)
    {
        var field = featureType.GetField("featureId", BindingFlags.Public | BindingFlags.Static);
        return field?.GetValue(null) as string ?? featureType.FullName ?? featureType.Name;
    }

    private static void SetFeatureField(OpenXRFeature feature, string fieldName, object? value)
    {
        var field = typeof(OpenXRFeature).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            Debug.LogWarning($"[NOVR] OpenXR controller profile bootstrap could not set OpenXRFeature.{fieldName}; field was not found.");
            return;
        }

        field.SetValue(feature, value);
    }

    private static int ReadIntFeatureField(OpenXRFeature feature, string fieldName)
    {
        var field = typeof(OpenXRFeature).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(feature) is int value ? value : 0;
    }

    private static string ReadStringFeatureField(OpenXRFeature feature, string fieldName)
    {
        var field = typeof(OpenXRFeature).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(feature) as string ?? "";
    }

    private readonly struct ProfileSpec
    {
        public readonly string TypeName;
        public readonly string UiName;

        public ProfileSpec(string typeName, string uiName)
        {
            TypeName = typeName;
            UiName = uiName;
        }
    }
}
