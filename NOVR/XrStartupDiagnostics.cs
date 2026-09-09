using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using InputSystemDevice = UnityEngine.InputSystem.InputDevice;
using XrInputDevice = UnityEngine.XR.InputDevice;

namespace NOVR;

internal static class XrStartupDiagnostics
{
    private static readonly List<XrInputDevice> XrDevices = new();
    private static readonly List<InputFeatureUsage> FeatureUsages = new();
    private static readonly List<XRDisplaySubsystem> DisplaySubsystems = new();
    private static readonly List<XRInputSubsystem> InputSubsystems = new();
    private static XRManagerSettings? _lastManager;
    private static XRLoader? _lastLoader;

    private static readonly string[] InterestingExtensions =
    {
        "XR_KHR_binding_modification",
        "XR_EXT_dpad_binding",
        "XR_EXT_hand_tracking",
        "XR_EXT_hand_interaction",
        "XR_EXT_palm_pose",
        "XR_MSFT_hand_interaction",
        "XR_EXT_hp_mixed_reality_controller",
        "XR_FB_touch_controller_pro",
        "XR_META_touch_controller_plus"
    };

    public static bool Enabled =>
        ModConfiguration.Instance != null &&
        ModConfiguration.Instance.LogXrStartupDiagnostics.Value;

    public static void LogBeforeInitialize(XRManagerSettings manager, XRLoader loader)
    {
        LogSnapshot("before InitializeLoaderSync", manager, loader, includeOpenXrSettings: false, includeOpenXrRuntime: false, includeDevices: false);
    }

    public static void LogAfterInitialize(XRManagerSettings manager)
    {
        LogSnapshot("after InitializeLoaderSync", manager, manager.activeLoader, includeOpenXrSettings: true, includeOpenXrRuntime: true, includeDevices: true);
    }

    public static void LogBeforeStart(XRManagerSettings manager)
    {
        LogSnapshot("before StartSubsystems", manager, manager.activeLoader, includeOpenXrSettings: true, includeOpenXrRuntime: true, includeDevices: true);
    }

    public static void LogAfterStart(XRManagerSettings manager)
    {
        LogSnapshot("after StartSubsystems", manager, manager.activeLoader, includeOpenXrSettings: true, includeOpenXrRuntime: true, includeDevices: true);
    }

    public static void LogBeforeStop(XRManagerSettings manager)
    {
        LogSnapshot("before StopSubsystems", manager, manager.activeLoader, includeOpenXrSettings: true, includeOpenXrRuntime: true, includeDevices: true);
    }

    public static void LogAfterStop(XRManagerSettings manager)
    {
        LogSnapshot("after StopSubsystems", manager, manager.activeLoader, includeOpenXrSettings: true, includeOpenXrRuntime: true, includeDevices: true);
    }

    public static void LogAfterDeinitialize(XRManagerSettings manager)
    {
        LogSnapshot("after DeinitializeLoader", manager, manager.activeLoader, includeOpenXrSettings: true, includeOpenXrRuntime: false, includeDevices: true);
    }

    public static void LogRuntimeSnapshot(string phase)
    {
        LogSnapshot(phase, _lastManager, _lastLoader, includeOpenXrSettings: true, includeOpenXrRuntime: true, includeDevices: true);
    }

    public static void LogXrDeviceEvent(string action, XrInputDevice device)
    {
        if (!Enabled) return;

        Debug.Log($"[NOVR] XR diagnostics device {action}: {FormatXrDevice(device)}");
    }

    public static void LogInputSystemDeviceEvent(InputSystemDevice device, InputDeviceChange change)
    {
        if (!Enabled) return;

        Debug.Log($"[NOVR] XR diagnostics InputSystem device {change}: {FormatInputSystemDevice(device)}");
    }

    private static void LogSnapshot(
        string phase,
        XRManagerSettings? manager,
        XRLoader? loader,
        bool includeOpenXrSettings,
        bool includeOpenXrRuntime,
        bool includeDevices)
    {
        if (!Enabled) return;

        if (manager != null)
        {
            _lastManager = manager;
        }

        if (loader != null)
        {
            _lastLoader = loader;
        }

        manager ??= _lastManager;
        loader ??= _lastLoader;

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[NOVR] XR diagnostics snapshot: {phase}");
            AppendUnityState(builder);
            AppendManagerState(builder, manager, loader);

            if (includeOpenXrSettings)
            {
                AppendOpenXrSettings(builder);
            }

            if (includeOpenXrRuntime)
            {
                AppendOpenXrRuntime(builder);
            }

            AppendSubsystemState(builder, loader);

            if (includeDevices)
            {
                AppendXrDevices(builder);
                AppendInputSystemDevices(builder);
            }

            Debug.Log(builder.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NOVR] XR diagnostics failed during '{phase}': {ex}");
        }
    }

    private static void AppendUnityState(StringBuilder builder)
    {
        builder.AppendLine(
            $"[NOVR]   Unity: version='{Application.unityVersion}', product='{Application.productName}', " +
            $"platform='{Application.platform}'");
        builder.AppendLine(
            $"[NOVR]   Graphics: type='{SystemInfo.graphicsDeviceType}', name='{SystemInfo.graphicsDeviceName}', " +
            $"vendor='{SystemInfo.graphicsDeviceVendor}', version='{SystemInfo.graphicsDeviceVersion}'");
        builder.AppendLine(
            $"[NOVR]   XRSettings: enabled={SafeValue(() => XRSettings.enabled.ToString())}, " +
            $"isDeviceActive={SafeValue(() => XRSettings.isDeviceActive.ToString())}, " +
            $"loadedDeviceName='{SafeValue(() => XRSettings.loadedDeviceName)}', " +
            $"eyeTexture={SafeValue(() => $"{XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}")}, " +
            $"renderViewportScale={SafeValue(() => XRSettings.renderViewportScale.ToString("0.###"))}");
    }

    private static void AppendManagerState(StringBuilder builder, XRManagerSettings? manager, XRLoader? loader)
    {
        builder.AppendLine(
            $"[NOVR]   XRManager: exists={manager != null}, initializationComplete={SafeValue(() => manager?.isInitializationComplete.ToString() ?? "n/a")}, " +
            $"activeLoader='{FormatObject(manager?.activeLoader)}', passedLoader='{FormatObject(loader)}'");

        if (manager == null) return;

        var loaderList = manager.activeLoaders
            .Select((managedLoader, index) => $"{index}:{FormatObject(managedLoader)}")
            .ToArray();

        builder.AppendLine($"[NOVR]   XRManager loaders: [{string.Join(", ", loaderList)}]");
    }

    private static void AppendOpenXrSettings(StringBuilder builder)
    {
        var settings = SafeResult(() => OpenXRSettings.Instance);
        if (settings == null)
        {
            builder.AppendLine("[NOVR]   OpenXRSettings: unavailable");
            return;
        }

        builder.AppendLine(
            $"[NOVR]   OpenXRSettings: renderMode={SafeValue(() => settings.renderMode.ToString())}, " +
            $"featureCount={SafeValue(() => settings.featureCount.ToString())}");

        var features = SafeResult(() => settings.GetFeatures()) ?? Array.Empty<OpenXRFeature>();
        if (features.Length == 0)
        {
            builder.AppendLine("[NOVR]   OpenXRSettings features: none");
            return;
        }

        foreach (var feature in features.Where(feature => feature != null))
        {
            builder.AppendLine(
                $"[NOVR]     feature type='{feature.GetType().FullName}', enabled={SafeValue(() => feature.enabled.ToString())}, " +
                $"nameUi='{ReadMember(feature, "nameUi")}', id='{ReadMember(feature, "featureIdInternal")}', " +
                $"required='{ReadMember(feature, "required")}', priority='{ReadMember(feature, "priority")}', " +
                $"extensions='{ReadMember(feature, "openxrExtensionStrings")}'");
        }
    }

    private static void AppendOpenXrRuntime(StringBuilder builder)
    {
        builder.AppendLine(
            $"[NOVR]   OpenXRRuntime: name='{SafeValue(() => OpenXRRuntime.name)}', " +
            $"version='{SafeValue(() => OpenXRRuntime.version)}', apiVersion='{SafeValue(() => OpenXRRuntime.apiVersion)}', " +
            $"pluginVersion='{SafeValue(() => OpenXRRuntime.pluginVersion)}'");

        var enabledExtensions = SafeResult(OpenXRRuntime.GetEnabledExtensions);
        if (enabledExtensions == null)
        {
            builder.AppendLine("[NOVR]   OpenXRRuntime enabled extensions: unavailable");
        }
        else
        {
            builder.AppendLine($"[NOVR]   OpenXRRuntime enabled extensions ({enabledExtensions.Length}): {string.Join(", ", enabledExtensions)}");
        }

        builder.AppendLine("[NOVR]   OpenXRRuntime interesting extension states:");
        foreach (var extension in InterestingExtensions)
        {
            builder.AppendLine(
                $"[NOVR]     {extension}: enabled={SafeValue(() => OpenXRRuntime.IsExtensionEnabled(extension).ToString())}, " +
                $"version={SafeValue(() => OpenXRRuntime.GetExtensionVersion(extension).ToString())}");
        }
    }

    private static void AppendSubsystemState(StringBuilder builder, XRLoader? loader)
    {
        var loadedDisplay = SafeResult(() => loader?.GetLoadedSubsystem<XRDisplaySubsystem>());
        var loadedInput = SafeResult(() => loader?.GetLoadedSubsystem<XRInputSubsystem>());

        builder.AppendLine($"[NOVR]   Loaded display subsystem: {FormatSubsystem(loadedDisplay)}");
        builder.AppendLine($"[NOVR]   Loaded input subsystem: {FormatSubsystem(loadedInput)}");

        if (loadedDisplay != null)
        {
            builder.AppendLine(
                $"[NOVR]     display refreshRate={TryInvokeOutValue(loadedDisplay, "TryGetDisplayRefreshRate")}, " +
                $"renderPassCount={TryInvokeNoArg(loadedDisplay, "GetRenderPassCount")}");
        }

        if (loadedInput != null)
        {
            builder.AppendLine(
                $"[NOVR]     input trackingOriginMode={TryInvokeNoArg(loadedInput, "GetTrackingOriginMode")}, " +
                $"supportedTrackingOriginModes={TryInvokeNoArg(loadedInput, "GetSupportedTrackingOriginModes")}");
        }

        DisplaySubsystems.Clear();
        InputSubsystems.Clear();
        SubsystemManager.GetInstances(DisplaySubsystems);
        SubsystemManager.GetInstances(InputSubsystems);

        builder.AppendLine($"[NOVR]   All display subsystems ({DisplaySubsystems.Count}):");
        foreach (var subsystem in DisplaySubsystems)
        {
            builder.AppendLine($"[NOVR]     {FormatSubsystem(subsystem)}");
        }

        builder.AppendLine($"[NOVR]   All input subsystems ({InputSubsystems.Count}):");
        foreach (var subsystem in InputSubsystems)
        {
            builder.AppendLine($"[NOVR]     {FormatSubsystem(subsystem)}");
        }
    }

    private static void AppendXrDevices(StringBuilder builder)
    {
        XrDevices.Clear();
        InputDevices.GetDevices(XrDevices);

        builder.AppendLine($"[NOVR]   XR input devices ({XrDevices.Count}):");
        foreach (var device in XrDevices)
        {
            builder.AppendLine($"[NOVR]     {FormatXrDevice(device)}");
        }
    }

    private static void AppendInputSystemDevices(StringBuilder builder)
    {
        builder.AppendLine($"[NOVR]   InputSystem devices ({SafeValue(() => InputSystem.devices.Count.ToString())}):");

        foreach (var device in InputSystem.devices)
        {
            builder.AppendLine($"[NOVR]     {FormatInputSystemDevice(device)}");
        }
    }

    private static string FormatXrDevice(XrInputDevice device)
    {
        var isTracked = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked;
        var hasTrackingState = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState, out var trackingState);

        FeatureUsages.Clear();
        var featureSummary = device.TryGetFeatureUsages(FeatureUsages)
            ? string.Join(", ", FeatureUsages.Select(usage => $"{usage.name}:{usage.type.Name}").Take(24))
            : "unavailable";

        return $"name='{device.name}', manufacturer='{device.manufacturer}', characteristics='{device.characteristics}', " +
               $"valid={device.isValid}, isTracked={isTracked}, trackingState='{(hasTrackingState ? trackingState.ToString() : "n/a")}', " +
               $"features=[{featureSummary}]";
    }

    private static string FormatInputSystemDevice(InputSystemDevice device)
    {
        return $"name='{device.name}', layout='{device.layout}', displayName='{device.displayName}', " +
               $"description='{device.description}', usages='{string.Join(",", device.usages.Select(usage => usage.ToString()))}'";
    }

    private static string FormatSubsystem(ISubsystem? subsystem)
    {
        if (subsystem == null) return "null";

        return $"type='{subsystem.GetType().FullName}', running={subsystem.running}, descriptor='{ReadMember(subsystem, "SubsystemDescriptor")}'";
    }

    private static string FormatObject(object? value)
    {
        return value == null ? "null" : value.GetType().FullName;
    }

    private static string ReadMember(object target, string memberName)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = target.GetType();
            var property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                return FormatValue(property.GetValue(target, null));
            }

            var field = type.GetField(memberName, flags);
            return field != null ? FormatValue(field.GetValue(target)) : "n/a";
        }
        catch (Exception ex)
        {
            return $"unavailable:{ex.GetType().Name}";
        }
    }

    private static string TryInvokeNoArg(object target, string methodName)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var method = target.GetType().GetMethod(methodName, flags, null, Type.EmptyTypes, null);
            return method == null ? "n/a" : FormatValue(method.Invoke(target, null));
        }
        catch (Exception ex)
        {
            return $"unavailable:{ex.GetType().Name}";
        }
    }

    private static string TryInvokeOutValue(object target, string methodName)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var method = target.GetType().GetMethod(methodName, flags);
            if (method == null) return "n/a";

            var parameters = method.GetParameters();
            if (parameters.Length != 1 || !parameters[0].ParameterType.IsByRef) return "n/a";

            var outValue = parameters[0].ParameterType.GetElementType()?.IsValueType == true
                ? Activator.CreateInstance(parameters[0].ParameterType.GetElementType())
                : null;
            var args = new[] { outValue };
            var result = method.Invoke(target, args);
            return result is bool ok && ok ? FormatValue(args[0]) : "unavailable";
        }
        catch (Exception ex)
        {
            return $"unavailable:{ex.GetType().Name}";
        }
    }

    private static string SafeValue(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            return $"unavailable:{ex.GetType().Name}";
        }
    }

    private static T? SafeResult<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            Array array => $"[{string.Join(", ", array.Cast<object>().Select(FormatValue))}]",
            _ => value.ToString() ?? ""
        };
    }
}

internal sealed class XrStartupDiagnosticsBehaviour : MonoBehaviour
{
    private Coroutine? _delayedSnapshots;

    private void OnEnable()
    {
        if (!XrStartupDiagnostics.Enabled)
        {
            enabled = false;
            return;
        }

        InputDevices.deviceConnected += OnXrDeviceConnected;
        InputDevices.deviceDisconnected += OnXrDeviceDisconnected;
        InputSystem.onDeviceChange += OnInputSystemDeviceChange;
        _delayedSnapshots = StartCoroutine(LogDelayedSnapshots());
    }

    private void OnDisable()
    {
        InputDevices.deviceConnected -= OnXrDeviceConnected;
        InputDevices.deviceDisconnected -= OnXrDeviceDisconnected;
        InputSystem.onDeviceChange -= OnInputSystemDeviceChange;

        if (_delayedSnapshots != null)
        {
            StopCoroutine(_delayedSnapshots);
            _delayedSnapshots = null;
        }
    }

    private IEnumerator LogDelayedSnapshots()
    {
        yield return null;
        XrStartupDiagnostics.LogRuntimeSnapshot("delayed first frame");

        yield return new WaitForSecondsRealtime(1f);
        XrStartupDiagnostics.LogRuntimeSnapshot("delayed 1s");

        yield return new WaitForSecondsRealtime(4f);
        XrStartupDiagnostics.LogRuntimeSnapshot("delayed 5s");
    }

    private static void OnXrDeviceConnected(XrInputDevice device)
    {
        XrStartupDiagnostics.LogXrDeviceEvent("connected", device);
    }

    private static void OnXrDeviceDisconnected(XrInputDevice device)
    {
        XrStartupDiagnostics.LogXrDeviceEvent("disconnected", device);
    }

    private static void OnInputSystemDeviceChange(InputSystemDevice device, InputDeviceChange change)
    {
        if (change is not (InputDeviceChange.Added
            or InputDeviceChange.Removed
            or InputDeviceChange.Disconnected
            or InputDeviceChange.Reconnected
            or InputDeviceChange.ConfigurationChanged
            or InputDeviceChange.UsageChanged))
        {
            return;
        }

        XrStartupDiagnostics.LogInputSystemDeviceEvent(device, change);
    }
}
