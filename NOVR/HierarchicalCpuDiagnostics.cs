using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace NOVR;

internal sealed class HierarchicalCpuDiagnostics : MonoBehaviour
{
    private const float IntervalSeconds = 10f;
    private const int MaxSamplesPerNode = 2048;
    private const double SpikeThresholdMs = 15.0;
    private const float SpikeLogCooldownSeconds = 5f;
    private const string HarmonyId = "deltawing.novr.diagnostics.update-timing";
    private static readonly double TickToMs = 1000.0 / Stopwatch.Frequency;
    private static bool _playerLoopProbesInstalled;
    private static HierarchicalCpuDiagnostics? _current;

    private readonly Dictionary<string, long> _loopStarts = new();
    private readonly Dictionary<string, TimingStats> _loopStats = new();
    private readonly Dictionary<Camera, long> _cameraStarts = new();
    private readonly Dictionary<string, TimingStats> _cameraStats = new();
    private readonly Dictionary<string, double> _currentFrameValues = new();
    private readonly Dictionary<string, double> _currentCameraValues = new();
    private readonly Dictionary<string, double> _currentNovrMethodValues = new();
    private readonly Dictionary<Camera, string> _cameraClassifications = new();
    private readonly FrameSnapshot[] _frameHistory = new FrameSnapshot[4];
    private readonly FrameTiming[] _frameTimings = new FrameTiming[1];
    private readonly TimingStats _renderPipeline = new("RenderPipeline");
    private readonly TimingStats _sendPreRender = new("PlayerSend/pre-render");
    private readonly TimingStats _sendRender = new("PlayerSend/render");
    private readonly TimingStats _sendPostRender = new("PlayerSend/post-render");
    private readonly TimingStats _finishFrameLeaf = new("FinishFrameRendering/native-leaf");
    private readonly List<MarkerRecorder> _markerRecorders = new();
    private readonly Dictionary<MethodBase, string> _novrMethodNames = new();

    private long _renderPipelineStart;
    private long _playerSendStart;
    private long _beginFrameTick;
    private long _endFrameTick;
    private float _nextLogTime;
    private float _nextSpikeLogTime;
    private long _instrumentedFrameStart;
    private int _historyWriteIndex;
    private int _historyCount;
    private int _lastGc0;
    private int _lastGc1;
    private int _lastGc2;
    private long _lastManagedHeapBytes;
    private ProfilerRecorder _gcAllocatedRecorder;
    private bool _gcAllocatedRecorderValid;
    private Harmony? _diagnosticHarmony;

    private void OnEnable()
    {
        _current = this;
        InstallPlayerLoopProbes();
        RenderPipelineManager.beginFrameRendering += BeginFrameRendering;
        RenderPipelineManager.endFrameRendering += EndFrameRendering;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
        CreateMarkerRecorders();
        InstallNovrMethodProbes();
        InitializeMemoryCounters();
        _nextLogTime = Time.realtimeSinceStartup + IntervalSeconds;
        Debug.Log("[NOVR CPU] hierarchical probes enabled: PlayerLoop L1/L2 + cameras + Unity markers + GC/allocation + NOVR method ownership");
    }

    private void OnDisable()
    {
        if (_current == this) _current = null;
        RenderPipelineManager.beginFrameRendering -= BeginFrameRendering;
        RenderPipelineManager.endFrameRendering -= EndFrameRendering;
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        foreach (var recorder in _markerRecorders) recorder.Dispose();
        _markerRecorders.Clear();
        if (_gcAllocatedRecorderValid) _gcAllocatedRecorder.Dispose();
        _gcAllocatedRecorderValid = false;
        if (_diagnosticHarmony != null)
        {
            _diagnosticHarmony.UnpatchSelf();
            _diagnosticHarmony = null;
        }
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup < _nextLogTime) return;
        // Reset aggregate storage without producing a multi-line logging hitch. Spikes are
        // emitted from the rolling per-frame recorder at the next frame boundary.
        ResetWindow();
        _nextLogTime = Time.realtimeSinceStartup + IntervalSeconds;
    }

    private void BeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
    {
        _renderPipelineStart = Stopwatch.GetTimestamp();
        _beginFrameTick = _renderPipelineStart;
    }

    private void EndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
    {
        if (_renderPipelineStart == 0) return;
        _renderPipeline.Record((Stopwatch.GetTimestamp() - _renderPipelineStart) * TickToMs);
        _endFrameTick = Stopwatch.GetTimestamp();
        _renderPipelineStart = 0;
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != null) _cameraStarts[camera] = Stopwatch.GetTimestamp();
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == null || !_cameraStarts.TryGetValue(camera, out var start)) return;
        _cameraStarts.Remove(camera);
        var key = ClassifyCameraCached(camera);
        var elapsedMs = (Stopwatch.GetTimestamp() - start) * TickToMs;
        GetStats(_cameraStats, key).Record(elapsedMs);
        _currentCameraValues.TryGetValue(key, out var cameraTotal);
        _currentCameraValues[key] = cameraTotal + elapsedMs;
    }

    private string ClassifyCameraCached(Camera camera)
    {
        if (_cameraClassifications.TryGetValue(camera, out var classification)) return classification;
        classification = ClassifyCamera(camera);
        _cameraClassifications[camera] = classification;
        return classification;
    }

    private static string ClassifyCamera(Camera camera)
    {
        var name = camera.name ?? "unnamed";
        if (camera == APIBus.MainCamera || name == "Main Camera" || name == "NOVR Main Camera") return "WORLD/" + name;
        if (name.IndexOf("VrCockpitHud", StringComparison.OrdinalIgnoreCase) >= 0) return "NOVR-HUD/" + name;
        if (name.IndexOf("VrClippedHud", StringComparison.OrdinalIgnoreCase) >= 0) return "NOVR-CLIPPED/" + name;
        if (name.IndexOf("cockpitRenderer", StringComparison.OrdinalIgnoreCase) >= 0) return "COCKPIT/" + name;
        if (name.IndexOf("postProcessing", StringComparison.OrdinalIgnoreCase) >= 0) return "POST/" + name;
        if (camera.targetTexture != null) return "OFFSCREEN/" + name;
        return "OTHER/" + name;
    }

    private static void InstallPlayerLoopProbes()
    {
        if (_playerLoopProbesInstalled) return;
        try
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (!RewritePlayerLoop(ref loop, null, 0)) return;
            PlayerLoop.SetPlayerLoop(loop);
            _playerLoopProbesInstalled = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR CPU] PlayerLoop probe installation failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static bool RewritePlayerLoop(ref PlayerLoopSystem system, string? parentName, int depth)
    {
        var children = system.subSystemList;
        if (children == null || children.Length == 0) return false;
        var changed = false;
        var rewritten = new List<PlayerLoopSystem>(children.Length * 3);
        foreach (var original in children)
        {
            var child = original;
            var name = LoopName(child);
            changed |= RewritePlayerLoop(ref child, name, depth + 1);
            if (ShouldProbe(child, parentName, depth + 1))
            {
                var key = depth switch
                {
                    0 => $"L1/{name}",
                    1 => $"L2/{parentName}/{name}",
                    _ => $"L3/{parentName}/{name}"
                };
                rewritten.Add(Marker(key, true));
                rewritten.Add(child);
                rewritten.Add(Marker(key, false));
                changed = true;
            }
            else
            {
                rewritten.Add(child);
            }
        }

        if (changed) system.subSystemList = rewritten.ToArray();
        return changed;
    }

    private static bool ShouldProbe(PlayerLoopSystem system, string? parentName, int depth)
    {
        if (system.type == null || system.type == typeof(HierarchicalCpuDiagnostics)) return false;
        var name = LoopName(system);
        if (name.IndexOf("NOVR", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (depth == 1)
        {
            return name is "Initialization" or "EarlyUpdate" or "FixedUpdate" or "PreUpdate" or
                "Update" or "PreLateUpdate" or "PostLateUpdate";
        }

        if (depth == 2)
        {
            return parentName is "Initialization" or "EarlyUpdate" or "FixedUpdate" or
                "PreUpdate" or "Update" or "PreLateUpdate" or "PostLateUpdate";
        }

        return depth == 3;
    }

    private static PlayerLoopSystem Marker(string key, bool begin)
    {
        return new PlayerLoopSystem
        {
            type = typeof(HierarchicalCpuDiagnostics),
            updateDelegate = () => RecordLoopMarker(key, begin)
        };
    }

    private static void RecordLoopMarker(string key, bool begin)
    {
        var current = _current;
        if (current == null) return;
        var now = Stopwatch.GetTimestamp();
        if (begin)
        {
            if (key == "L1/Initialization")
            {
                current.BeginInstrumentedFrame(now);
            }
            current._loopStarts[key] = now;
            if (key.EndsWith("PlayerSendFrameStarted", StringComparison.Ordinal))
            {
                current._playerSendStart = now;
                current._beginFrameTick = 0;
                current._endFrameTick = 0;
            }
            return;
        }

        if (!current._loopStarts.TryGetValue(key, out var start)) return;
        current._loopStarts.Remove(key);
        var elapsedMs = (now - start) * TickToMs;
        GetStats(current._loopStats, key).Record(elapsedMs);
        current._currentFrameValues[key] = elapsedMs;
        if (key.EndsWith("PlayerSendFrameStarted", StringComparison.Ordinal))
        {
            current.RecordPlayerSendBreakdown(now);
        }
        else if (key.EndsWith("FinishFrameRendering", StringComparison.Ordinal))
        {
            current._finishFrameLeaf.Record((now - start) * TickToMs);
        }
    }

    private static string LoopName(PlayerLoopSystem system)
    {
        if (system.type == null) return "null";
        var full = system.type.FullName ?? system.type.Name;
        var dot = full.LastIndexOf('.');
        return dot >= 0 ? full.Substring(dot + 1) : full;
    }

    private void CreateMarkerRecorders()
    {
        var markers = new[]
        {
            "PlayerLoop", "ScriptRunBehaviourUpdate", "ScriptRunBehaviourFixedUpdate",
            "ScriptRunBehaviourLateUpdate", "Physics.Simulate", "Camera.Render",
            "RenderPipelineManager.DoRenderLoop_Internal", "RenderLoop.Draw", "CullResults.Cull", "Culling",
            "Shadows.RenderShadowMap", "Gfx.WaitForPresentOnGfxThread", "Gfx.PresentFrame",
            "WaitForTargetFPS", "Semaphore.WaitForSignal", "XR.WaitForGPU", "XR.SubmitFrame"
        };
        var categories = new[] { ProfilerCategory.Internal, ProfilerCategory.Render, ProfilerCategory.Scripts, ProfilerCategory.Physics };
        foreach (var marker in markers)
        foreach (var category in categories)
        {
            try
            {
                var recorder = new MarkerRecorder(category, marker);
                if (recorder.Valid) _markerRecorders.Add(recorder);
                else recorder.Dispose();
            }
            catch
            {
                // Marker availability differs by Unity backend and build configuration.
            }
        }

        DiscoverRelevantMarkerRecorders();
    }

    private void InitializeMemoryCounters()
    {
        _lastGc0 = GC.CollectionCount(0);
        _lastGc1 = GC.CollectionCount(1);
        _lastGc2 = GC.CollectionCount(2);
        _lastManagedHeapBytes = GC.GetTotalMemory(false);
        try
        {
            _gcAllocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            _gcAllocatedRecorderValid = _gcAllocatedRecorder.Valid;
        }
        catch
        {
            _gcAllocatedRecorderValid = false;
        }
    }

    private void InstallNovrMethodProbes()
    {
        try
        {
            var assembly = typeof(Core).Assembly;
            var prefix = new HarmonyMethod(typeof(HierarchicalCpuDiagnostics), nameof(NovrMethodPrefix));
            var postfix = new HarmonyMethod(typeof(HierarchicalCpuDiagnostics), nameof(NovrMethodPostfix));
            _diagnosticHarmony = new Harmony(HarmonyId);
            var patched = 0;

            foreach (var type in assembly.GetTypes())
            {
                if (type == typeof(HierarchicalCpuDiagnostics) || type.IsAbstract ||
                    !typeof(MonoBehaviour).IsAssignableFrom(type)) continue;

                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if ((method.Name != "Update" && method.Name != "FixedUpdate" && method.Name != "LateUpdate") ||
                        method.IsAbstract || method.IsGenericMethod || method.ReturnType != typeof(void) ||
                        method.GetParameters().Length != 0 || method.GetMethodBody() == null) continue;

                    _novrMethodNames[method] = type.FullName + "." + method.Name;
                    _diagnosticHarmony.Patch(method, prefix, postfix: postfix);
                    patched++;
                }
            }

            Debug.Log($"[NOVR CPU] NOVR-only managed method probes={patched}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR CPU] NOVR method probes unavailable: {exception.GetType().Name}: {exception.Message}");
            _diagnosticHarmony?.UnpatchSelf();
            _diagnosticHarmony = null;
            _novrMethodNames.Clear();
        }
    }

    private static void NovrMethodPrefix(out long __state) => __state = Stopwatch.GetTimestamp();

    private static void NovrMethodPostfix(MethodBase __originalMethod, long __state)
    {
        var current = _current;
        if (current == null || __state == 0 ||
            !current._novrMethodNames.TryGetValue(__originalMethod, out var name)) return;
        var elapsedMs = (Stopwatch.GetTimestamp() - __state) * TickToMs;
        current._currentNovrMethodValues.TryGetValue(name, out var total);
        current._currentNovrMethodValues[name] = total + elapsedMs;
    }

    private void DiscoverRelevantMarkerRecorders()
    {
        try
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var existing = new HashSet<string>(_markerRecorders.Select(recorder => recorder.Key));
            var keywords = new[]
            {
                "wait", "present", "finishframe", "sendframe", "render", "camera", "cull", "shadow",
                "submit", "xr", "gfx", "job", "semaphore", "fence", "sync", "script", "physics", "gc", "mono"
            };

            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                var name = description.Name;
                if (description.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds ||
                    string.IsNullOrWhiteSpace(name) ||
                    !keywords.Any(keyword => name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    continue;
                }

                var key = description.Category.Name + "/" + name;
                if (!existing.Add(key)) continue;
                try
                {
                    var recorder = new MarkerRecorder(description.Category, name);
                    if (recorder.Valid) _markerRecorders.Add(recorder);
                    else recorder.Dispose();
                }
                catch
                {
                    // Marker may disappear while Unity changes subsystems or scenes.
                }
            }

            Debug.Log($"[NOVR CPU] discovered relevant profiler markers={_markerRecorders.Count}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR CPU] profiler marker discovery unavailable: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private void LogSnapshot()
    {
        LogStatsLine("[NOVR CPU L1]", _loopStats.Where(pair => pair.Key.StartsWith("L1/")));

        foreach (var parent in new[] { "Initialization", "EarlyUpdate", "FixedUpdate", "PreUpdate", "Update", "PreLateUpdate", "PostLateUpdate" })
        {
            LogStatsLine($"[NOVR CPU L2/{parent}]", _loopStats.Where(pair => pair.Key.StartsWith($"L2/{parent}/")));
        }

        LogStatsLine("[NOVR CPU L3]", _loopStats.Where(pair => pair.Key.StartsWith("L3/")));

        LogStatsLine("[NOVR CPU Cameras]", _cameraStats);
        Debug.Log($"[NOVR CPU RenderPipeline] {_renderPipeline.Format()}");
        Debug.Log($"[NOVR CPU PlayerSend-L3] preRender={_sendPreRender.Format()}; render={_sendRender.Format()}; postRender={_sendPostRender.Format()}");
        Debug.Log($"[NOVR CPU FinishFrame-L3] total={_finishFrameLeaf.Format()}; nested wait/render/submit markers follow in [NOVR CPU Markers]");

        var markerValues = _markerRecorders
            .Select(recorder => recorder.Snapshot())
            .Where(value => value != null && (value.Value.AverageMs > 0.001 || value.Value.MaxMs > 0.01))
            .OrderByDescending(value => value!.Value.AverageMs)
            .Select(value => value!.Value.Format())
            .ToArray();
        Debug.Log(markerValues.Length == 0
            ? "[NOVR CPU Markers] unavailable"
            : "[NOVR CPU Markers] " + string.Join("; ", markerValues));
    }

    private static void LogStatsLine(string prefix, IEnumerable<KeyValuePair<string, TimingStats>> source)
    {
        var values = source.OrderByDescending(pair => pair.Value.AverageMs)
            .Select(pair => $"{pair.Key}={pair.Value.Format()}")
            .ToArray();
        Debug.Log(values.Length == 0 ? prefix + " unavailable" : prefix + " " + string.Join("; ", values));
    }

    private void ResetWindow()
    {
        _loopStats.Clear();
        _cameraStats.Clear();
        _loopStarts.Clear();
        _cameraStarts.Clear();
        _cameraClassifications.Clear();
        _renderPipeline.Reset();
        _sendPreRender.Reset();
        _sendRender.Reset();
        _sendPostRender.Reset();
        _finishFrameLeaf.Reset();
    }

    private void BeginInstrumentedFrame(long now)
    {
        if (_instrumentedFrameStart != 0)
        {
            var frameMs = (now - _instrumentedFrameStart) * TickToMs;
            double gpuMs = 0;
            double cpuRenderMs = 0;
            try
            {
                if (FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
                {
                    gpuMs = _frameTimings[0].gpuFrameTime;
                    cpuRenderMs = _frameTimings[0].cpuRenderThreadFrameTime;
                }
            }
            catch
            {
                // FrameTimingManager availability varies during scene transitions.
            }

            var gc0 = GC.CollectionCount(0);
            var gc1 = GC.CollectionCount(1);
            var gc2 = GC.CollectionCount(2);
            var managedHeapBytes = GC.GetTotalMemory(false);
            var allocatedBytes = _gcAllocatedRecorderValid ? Math.Max(0, _gcAllocatedRecorder.LastValue) : -1;
            var novrTotal = 0.0;
            var topNovrMs = 0.0;
            var topNovrMethod = "none";
            foreach (var pair in _currentNovrMethodValues)
            {
                novrTotal += pair.Value;
                if (pair.Value <= topNovrMs) continue;
                topNovrMs = pair.Value;
                topNovrMethod = pair.Key;
            }

            var snapshot = new FrameSnapshot(
                frameMs,
                Value("L1/Update"),
                Value("L1/FixedUpdate"),
                Value("L1/PreLateUpdate"),
                Value("L1/PostLateUpdate"),
                Value("L2/Update/Update+ScriptRunBehaviourUpdate"),
                Value("L2/FixedUpdate/FixedUpdate+PhysicsFixedUpdate"),
                Value("L2/PostLateUpdate/PostLateUpdate+PlayerSendFrameStarted"),
                Value("L2/PostLateUpdate/PostLateUpdate+FinishFrameRendering"),
                _renderPipeline.LastMs,
                CameraTotal("WORLD/"),
                CameraTotal("NOVR-HUD/") + CameraTotal("NOVR-CLIPPED/"),
                CameraTotal("POST/"),
                CameraTotal("OFFSCREEN/"),
                cpuRenderMs,
                gpuMs,
                gc0 - _lastGc0,
                gc1 - _lastGc1,
                gc2 - _lastGc2,
                allocatedBytes,
                managedHeapBytes - _lastManagedHeapBytes,
                novrTotal,
                topNovrMethod,
                topNovrMs);

            _lastGc0 = gc0;
            _lastGc1 = gc1;
            _lastGc2 = gc2;
            _lastManagedHeapBytes = managedHeapBytes;

            _frameHistory[_historyWriteIndex] = snapshot;
            _historyWriteIndex = (_historyWriteIndex + 1) % _frameHistory.Length;
            _historyCount = Math.Min(_historyCount + 1, _frameHistory.Length);

            if (frameMs >= SpikeThresholdMs &&
                Time.realtimeSinceStartup >= _nextSpikeLogTime &&
                SceneManager.GetActiveScene().path.IndexOf("GameWorld", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _nextSpikeLogTime = Time.realtimeSinceStartup + SpikeLogCooldownSeconds;
                LogSpikeHistory();
            }
        }

        _currentFrameValues.Clear();
        _currentCameraValues.Clear();
        _currentNovrMethodValues.Clear();
        _instrumentedFrameStart = now;
    }

    private double Value(string key) => _currentFrameValues.TryGetValue(key, out var value) ? value : 0;

    private double CameraTotal(string prefix)
    {
        double total = 0;
        foreach (var pair in _currentCameraValues)
            if (pair.Key.StartsWith(prefix, StringComparison.Ordinal)) total += pair.Value;
        return total;
    }

    private void LogSpikeHistory()
    {
        var frameCount = Math.Min(2, _historyCount);
        var frames = new List<string>(frameCount);
        for (var age = frameCount - 1; age >= 0; age--)
        {
            var index = (_historyWriteIndex - 1 - age + _frameHistory.Length) % _frameHistory.Length;
            frames.Add($"t-{age}:{_frameHistory[index].Format()}");
        }

        var markers = _markerRecorders
            .Select(recorder => recorder.Snapshot())
            .Where(value => value != null && value.Value.AverageMs > 0.01)
            .OrderByDescending(value => value!.Value.AverageMs)
            .Take(3)
            .Select(value => value!.Value.Format());

        Debug.Log("[NOVR Spike] " + string.Join(" | ", frames) + " markers=" + string.Join(",", markers));
    }

    private static TimingStats GetStats(Dictionary<string, TimingStats> stats, string key)
    {
        if (stats.TryGetValue(key, out var value)) return value;
        value = new TimingStats(key);
        stats.Add(key, value);
        return value;
    }

    private void RecordPlayerSendBreakdown(long playerSendEnd)
    {
        if (_playerSendStart == 0) return;
        if (_beginFrameTick >= _playerSendStart)
        {
            _sendPreRender.Record((_beginFrameTick - _playerSendStart) * TickToMs);
        }
        if (_beginFrameTick > 0 && _endFrameTick >= _beginFrameTick)
        {
            _sendRender.Record((_endFrameTick - _beginFrameTick) * TickToMs);
        }
        if (_endFrameTick > 0 && playerSendEnd >= _endFrameTick)
        {
            _sendPostRender.Record((playerSendEnd - _endFrameTick) * TickToMs);
        }
        _playerSendStart = 0;
    }

    private sealed class TimingStats
    {
        private readonly List<double> _samples = new(MaxSamplesPerNode);
        private double _sum;
        private double _max;
        public string Name { get; }
        public int Count { get; private set; }
        public double AverageMs => Count == 0 ? 0 : _sum / Count;
        public double LastMs { get; private set; }

        public TimingStats(string name) => Name = name;

        public void Record(double milliseconds)
        {
            Count++;
            LastMs = milliseconds;
            _sum += milliseconds;
            _max = Math.Max(_max, milliseconds);
            if (_samples.Count < MaxSamplesPerNode) _samples.Add(milliseconds);
        }

        public string Format()
        {
            if (Count == 0) return "n/a";
            var sorted = _samples.OrderBy(value => value).ToArray();
            var p95 = sorted.Length == 0 ? 0 : sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * 0.95) - 1)];
            return $"avg {AverageMs:0.000}/p95 {p95:0.000}/max {_max:0.000}ms n={Count}";
        }

        public void Reset()
        {
            _samples.Clear();
            _sum = 0;
            _max = 0;
            Count = 0;
            LastMs = 0;
        }
    }

    private readonly struct FrameSnapshot
    {
        private readonly double _frame, _update, _fixed, _preLate, _postLate, _scripts, _physics;
        private readonly double _send, _finish, _pipeline, _world, _hud, _post, _offscreen, _cpuRender, _gpu;
        private readonly int _gc0, _gc1, _gc2;
        private readonly long _allocatedBytes, _heapDeltaBytes;
        private readonly double _novrTotal, _topNovrMs;
        private readonly string _topNovrMethod;

        public FrameSnapshot(double frame, double update, double fixedUpdate, double preLate, double postLate,
            double scripts, double physics, double send, double finish, double pipeline, double world, double hud,
            double post, double offscreen, double cpuRender, double gpu,
            int gc0, int gc1, int gc2, long allocatedBytes, long heapDeltaBytes,
            double novrTotal, string topNovrMethod, double topNovrMs)
        {
            _frame = frame; _update = update; _fixed = fixedUpdate; _preLate = preLate; _postLate = postLate;
            _scripts = scripts; _physics = physics; _send = send; _finish = finish; _pipeline = pipeline;
            _world = world; _hud = hud; _post = post; _offscreen = offscreen; _cpuRender = cpuRender; _gpu = gpu;
            _gc0 = gc0; _gc1 = gc1; _gc2 = gc2; _allocatedBytes = allocatedBytes; _heapDeltaBytes = heapDeltaBytes;
            _novrTotal = novrTotal; _topNovrMethod = topNovrMethod; _topNovrMs = topNovrMs;
        }

        public string Format() =>
            $"frame={_frame:0.00} update={_update:0.00} fixed={_fixed:0.00} preLate={_preLate:0.00} postLate={_postLate:0.00} " +
            $"scripts={_scripts:0.00} physics={_physics:0.00} send={_send:0.00} finish={_finish:0.00} pipe={_pipeline:0.00} " +
            $"cams[world/hud/post/off]={_world:0.00}/{_hud:0.00}/{_post:0.00}/{_offscreen:0.00} " +
            $"cpuRender={_cpuRender:0.00} gpu={_gpu:0.00} " +
            $"gc={_gc0}/{_gc1}/{_gc2} allocKB={(_allocatedBytes < 0 ? -1 : _allocatedBytes / 1024.0):0.0} " +
            $"heapDeltaKB={_heapDeltaBytes / 1024.0:0.0} novr={_novrTotal:0.00} topNovr={_topNovrMethod}:{_topNovrMs:0.00}";
    }

    private sealed class MarkerRecorder : IDisposable
    {
        private readonly ProfilerCategory _category;
        private readonly string _name;
        private readonly ProfilerRecorder _recorder;
        private readonly List<ProfilerRecorderSample> _samples = new();
        public bool Valid => _recorder.Valid;
        public string Key => _category.Name + "/" + _name;

        public MarkerRecorder(ProfilerCategory category, string name)
        {
            _category = category;
            _name = name;
            _recorder = ProfilerRecorder.StartNew(category, name, 128);
        }

        public MarkerValue? Snapshot()
        {
            if (!_recorder.Valid) return null;
            _samples.Clear();
            _recorder.CopyTo(_samples);
            if (_samples.Count == 0) return null;
            var average = _samples.Average(sample => sample.Value) / 1_000_000.0;
            var max = _samples.Max(sample => sample.Value) / 1_000_000.0;
            return new MarkerValue(_category.Name, _name, average, max, _samples.Count);
        }

        public void Dispose() => _recorder.Dispose();
    }

    private readonly struct MarkerValue
    {
        public string Category { get; }
        public string Name { get; }
        public double AverageMs { get; }
        public double MaxMs { get; }
        public int Count { get; }

        public MarkerValue(string category, string name, double averageMs, double maxMs, int count)
        {
            Category = category;
            Name = name;
            AverageMs = averageMs;
            MaxMs = maxMs;
            Count = count;
        }

        public string Format() => $"{Category}/{Name}=avg {AverageMs:0.000}/max {MaxMs:0.000}ms n={Count}";
    }
}
