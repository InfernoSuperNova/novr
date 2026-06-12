using System;

namespace NOVR.PatchHelper;

public class PatchAttribute : Attribute
{
    public Type TargetType { get; }
    public string MethodName { get; }

    public PatchAttribute(Type targetType, string methodName)
    {
        TargetType = targetType;
        MethodName = methodName;
    }
}