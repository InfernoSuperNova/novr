using System;
namespace NOVR.McpBridge;

[BepInEx.BepInPlugin("deltawing.novr.mcpbridge", "NOVR McpBridge", "0.1.0")]
public sealed class McpBridgePlugin : BepInEx.BaseUnityPlugin
{
    private const int DefaultPort = 3334;
    private McpHttpServer? _server;

    private void Awake()
    {
        var port = Config.Bind("General", "Port", DefaultPort, "HTTP server port for MCP bridge").Value;

        ToolRegistry.DiscoverFromAssembly(typeof(McpBridgePlugin).Assembly);

        _ = MainThreadDispatcher.Instance;

        _server = new McpHttpServer(port);
        _server.Start();

        Logger.LogInfo($"MCP bridge started on http://localhost:{port}/");
        Logger.LogInfo($"Registered {ToolRegistry.Tools.Count} tool(s): {string.Join(", ", ToolRegistry.Tools.Keys)}");
    }

    private void OnDestroy()
    {
        _server?.Dispose();
    }
}