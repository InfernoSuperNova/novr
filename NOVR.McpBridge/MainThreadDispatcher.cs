using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace NOVR.McpBridge;

public sealed class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher? _instance;
    private readonly ConcurrentQueue<Action> _queue = new();

    public static MainThreadDispatcher Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("NOVR.McpBridge.MainThreadDispatcher")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MainThreadDispatcher>();
            return _instance;
        }
    }

    public Task<T> RunAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        _queue.Enqueue(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public Task RunAsync(Action action)
    {
        var tcs = new TaskCompletionSource<object?>();
        _queue.Enqueue(() =>
        {
            try { action(); tcs.SetResult(null); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    private void Update()
    {
        while (_queue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[NOVR.McpBridge] Main-thread action threw: {ex}");
            }
        }
    }

    private void OnDestroy()
    {
        _instance = null;
    }
}