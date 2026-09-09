using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NOVR.McpBridge;

public sealed class ToolDescriptor
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public MethodInfo Method { get; set; } = null!;
    public ParameterInfo[] Parameters { get; set; } = Array.Empty<ParameterInfo>();
    public string InputSchemaJson { get; set; } = "{}";
}

public static class ToolRegistry
{
    private static readonly Dictionary<string, ToolDescriptor> _tools = new();
    public static IReadOnlyDictionary<string, ToolDescriptor> Tools => _tools;

    public static void DiscoverFromAssembly(Assembly assembly)
    {
        foreach (var method in assembly.GetTypes()
                     .Where(t => t.IsClass && t.IsAbstract && t.IsSealed)
                     .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)))
        {
            var attr = method.GetCustomAttribute<McpToolAttribute>();
            if (attr == null) continue;

            var parameters = method.GetParameters();
            var schema = BuildSchema(method, parameters);

            _tools[attr.Name] = new ToolDescriptor
            {
                Name = attr.Name,
                Description = attr.Description,
                Method = method,
                Parameters = parameters,
                InputSchemaJson = schema,
            };
        }
    }

    public static object? Invoke(ToolDescriptor tool, Dictionary<string, object?> args)
    {
        var callArgs = new object?[tool.Parameters.Length];
        for (var i = 0; i < tool.Parameters.Length; i++)
        {
            var p = tool.Parameters[i];
            var paramAttr = p.GetCustomAttribute<McpParamAttribute>();
            var paramName = paramAttr?.Name ?? p.Name ?? "";

            if (args.TryGetValue(paramName, out var val))
            {
                callArgs[i] = ConvertValue(val, p.ParameterType);
            }
            else if (p.HasDefaultValue)
            {
                callArgs[i] = p.DefaultValue;
            }
            else if (!(paramAttr?.Required ?? true))
            {
                callArgs[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
            }
            else
            {
                throw new ArgumentException($"Missing required parameter '{paramName}' for tool '{tool.Name}'");
            }
        }

        return tool.Method.Invoke(null, callArgs);
    }

    private static object? ConvertValue(object? val, Type targetType)
    {
        if (val == null) return null;
        if (targetType == typeof(int)) return Convert.ToInt32(val);
        if (targetType == typeof(long)) return Convert.ToInt64(val);
        if (targetType == typeof(float)) return Convert.ToSingle(val);
        if (targetType == typeof(double)) return Convert.ToDouble(val);
        if (targetType == typeof(bool)) return Convert.ToBoolean(val);
        if (targetType == typeof(string)) return val.ToString();
        if (targetType == typeof(int?)) return val != null ? Convert.ToInt32(val) : (int?)null;
        if (targetType == typeof(float?)) return val != null ? Convert.ToSingle(val) : (float?)null;
        if (targetType == typeof(double?)) return val != null ? Convert.ToDouble(val) : (double?)null;
        if (targetType == typeof(bool?)) return val != null ? Convert.ToBoolean(val) : (bool?)null;
        return val;
    }

    private static string BuildSchema(MethodInfo method, ParameterInfo[] parameters)
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"object\",\"properties\":{");
        var reqList = new List<string>();

        var first = true;
        foreach (var p in parameters)
        {
            var paramAttr = p.GetCustomAttribute<McpParamAttribute>();
            var paramName = paramAttr?.Name ?? p.Name ?? "";
            var description = paramAttr?.Description ?? "";
            var schemaType = JsonSchemaType(p.ParameterType);
            var isRequired = (paramAttr?.Required ?? true) && !p.HasDefaultValue;

            if (!first) sb.Append(',');
            first = false;

            sb.Append('"');
            sb.Append(EscapeJson(paramName));
            sb.Append("\":{\"type\":\"");
            sb.Append(schemaType);
            sb.Append("\",\"description\":\"");
            sb.Append(EscapeJson(description));
            sb.Append("\"}");

            if (isRequired) reqList.Add(paramName);
        }

        sb.Append("},\"required\":[");
        if (reqList.Count > 0)
        {
            sb.Append('"');
            sb.Append(string.Join("\",\"", reqList.Select(EscapeJson)));
            sb.Append('"');
        }
        sb.Append("]}");

        return sb.ToString();
    }

    private static string JsonSchemaType(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        if (underlying == typeof(string)) return "string";
        if (underlying == typeof(bool)) return "boolean";
        if (underlying == typeof(int) || underlying == typeof(long) ||
            underlying == typeof(short) || underlying == typeof(byte) ||
            underlying == typeof(float) || underlying == typeof(double) ||
            underlying == typeof(decimal))
            return "number";
        if (underlying.IsArray) return "array";
        return "object";
    }

    internal static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    internal static string ToJsonString(object? value)
    {
        if (value == null) return "null";
        if (value is string s) return $"\"{EscapeJson(s)}\"";
        if (value is bool b) return b ? "true" : "false";
        if (value is int || value is long || value is short || value is byte ||
            value is float || value is double || value is decimal)
            return Convert.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value is IEnumerable<KeyValuePair<string, object?>> dict)
        {
            var sb = new StringBuilder("{");
            var first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"');
                sb.Append(EscapeJson(kv.Key));
                sb.Append("\":");
                sb.Append(ToJsonString(kv.Value));
            }
            sb.Append('}');
            return sb.ToString();
        }
        return $"\"{EscapeJson(value.ToString() ?? "")}\"";
    }
}