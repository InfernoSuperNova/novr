using System;

namespace NOVR.McpBridge;

public sealed class McpToolResult
{
    public string? Text { get; init; }
    public string? ImageBase64 { get; init; }
    public string ImageMimeType { get; init; } = "image/png";

    public static McpToolResult FromText(string text) => new() { Text = text };

    public static McpToolResult FromImage(string base64Png, string? caption = null) => new()
    {
        Text = caption,
        ImageBase64 = base64Png,
    };
}
