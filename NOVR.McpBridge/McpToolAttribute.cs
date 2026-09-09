using System;

namespace NOVR.McpBridge;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }

    public McpToolAttribute(string name, string description = "")
    {
        Name = name;
        Description = description;
    }
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class McpParamAttribute : Attribute
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool Required { get; set; } = true;
}