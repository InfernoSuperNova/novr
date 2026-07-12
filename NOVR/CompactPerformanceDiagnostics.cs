using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR;

internal sealed class CompactPerformanceDiagnostics : MonoBehaviour
{
    private const float IntervalSeconds = 10f;
    private const int MaximumSamples = 2048;
    private static readonly List<XRDisplaySubsystem> Displays = new();

    private readonly FrameTiming[] _latestTiming = new FrameTiming[1];
    private readonly List<double> _frameDeltaMs = new(MaximumSamples);
    private readonly List<double> _cpuFrameMs = new(MaximumSamples);
    private readonly List<double> _cpuMainMs = new(MaximumSamples);
    private readonly List<double> _cpuRenderMs = new(MaximumSamples);
    private readonly List<double> _gpuFrameMs = new(MaximumSamples);

    private float _intervalStart;
    private long? _lastDroppedFrames;
    private long? _lastPresentedFrames;

    private void OnEnable()
    {
        _intervalStart = Time.realtimeSinceStartup;
        Debug.Log("[NOVR Perf] enabled interval=10s source=FrameTimingManager+XRDisplaySubsystem");
    }

    private void Update()
    {
        Add(_frameDeltaMs, Time.unscaledDeltaTime * 1000.0);

        try
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _latestTiming) > 0)
            {
                var timing = _latestTiming[0];
                AddIfValid(_cpuFrameMs, timing.cpuFrameTime);
                AddIfValid(_cpuMainMs, timing.cpuMainThreadFrameTime);
                AddIfValid(_cpuRenderMs, timing.cpuRenderThreadFrameTime);
                AddIfValid(_gpuFrameMs, timing.gpuFrameTime);
            }
        }
        catch
        {
            // A zero timing sample is reported below if this backend does not support FrameTimingManager.
        }

        var now = Time.realtimeSinceStartup;
        if (now - _intervalStart < IntervalSeconds)
        {
            return;
        }

        LogAndReset(now);
    }

    private void LogAndReset(float now)
    {
        var elapsed = Math.Max(0.001, now - _intervalStart);
        var frames = _frameDeltaMs.Count;
        var observedFps = frames / elapsed;
        var display = GetRunningDisplay();
        var refresh = TryInvokeOutNumber(display, "TryGetDisplayRefreshRate");
        var dropped = TryInvokeOutLong(display, "TryGetDroppedFrameCount");
        var presented = TryInvokeOutLong(display, "TryGetFramePresentCount");
        var droppedDelta = Delta(dropped, _lastDroppedFrames);
        var presentedDelta = Delta(presented, _lastPresentedFrames);
        if (dropped.HasValue) _lastDroppedFrames = dropped;
        if (presented.HasValue) _lastPresentedFrames = presented;

        Debug.Log(
            $"[NOVR Perf] window={elapsed:0.00}s frames={frames} observedFps={observedFps:0.0} " +
            $"refresh={Format(refresh)}Hz deltaMs[p50/p95/p99]={Stats(_frameDeltaMs)} " +
            $"cpuFrameMs[avg/p95/p99]={StatsWithAverage(_cpuFrameMs)} " +
            $"cpuMainMs[avg/p95/p99]={StatsWithAverage(_cpuMainMs)} " +
            $"cpuRenderMs[avg/p95/p99]={StatsWithAverage(_cpuRenderMs)} " +
            $"gpuFrameMs[avg/p95/p99]={StatsWithAverage(_gpuFrameMs)} " +
            $"xrDropped={Format(dropped)} delta={Format(droppedDelta)} " +
            $"xrPresented={Format(presented)} delta={Format(presentedDelta)}");

        _frameDeltaMs.Clear();
        _cpuFrameMs.Clear();
        _cpuMainMs.Clear();
        _cpuRenderMs.Clear();
        _gpuFrameMs.Clear();
        _intervalStart = now;
    }

    private static XRDisplaySubsystem? GetRunningDisplay()
    {
        Displays.Clear();
        SubsystemManager.GetInstances(Displays);
        return Displays.FirstOrDefault(display => display != null && display.running);
    }

    private static double? TryInvokeOutNumber(object? target, string methodName)
    {
        if (target == null) return null;

        try
        {
            var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                    candidate.Name == methodName &&
                    candidate.GetParameters().Length == 1 &&
                    candidate.GetParameters()[0].ParameterType.IsByRef);
            if (method == null) return null;

            var elementType = method.GetParameters()[0].ParameterType.GetElementType();
            var arguments = new[] { elementType != null && elementType.IsValueType ? Activator.CreateInstance(elementType) : null };
            var succeeded = method.Invoke(target, arguments);
            if (succeeded is bool ok && !ok) return null;
            return arguments[0] == null ? null : Convert.ToDouble(arguments[0]);
        }
        catch
        {
            return null;
        }
    }

    private static long? TryInvokeOutLong(object? target, string methodName)
    {
        var value = TryInvokeOutNumber(target, methodName);
        return value.HasValue ? Convert.ToInt64(value.Value) : null;
    }

    private static long? Delta(long? current, long? previous)
    {
        return current.HasValue && previous.HasValue ? current.Value - previous.Value : null;
    }

    private static void Add(List<double> samples, double value)
    {
        if (samples.Count < MaximumSamples) samples.Add(value);
    }

    private static void AddIfValid(List<double> samples, double value)
    {
        if (value > 0 && !double.IsNaN(value) && !double.IsInfinity(value)) Add(samples, value);
    }

    private static string Stats(List<double> samples)
    {
        if (samples.Count == 0) return "n/a";
        var sorted = samples.OrderBy(value => value).ToArray();
        return $"{Percentile(sorted, 0.50):0.00}/{Percentile(sorted, 0.95):0.00}/{Percentile(sorted, 0.99):0.00}";
    }

    private static string StatsWithAverage(List<double> samples)
    {
        if (samples.Count == 0) return "n/a";
        var sorted = samples.OrderBy(value => value).ToArray();
        return $"{samples.Average():0.00}/{Percentile(sorted, 0.95):0.00}/{Percentile(sorted, 0.99):0.00}";
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
    }

    private static string Format(double? value) => value.HasValue ? value.Value.ToString("0.##") : "n/a";
    private static string Format(long? value) => value.HasValue ? value.Value.ToString() : "n/a";
}
