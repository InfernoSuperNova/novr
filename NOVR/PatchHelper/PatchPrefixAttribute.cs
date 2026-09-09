using System;

namespace NOVR.PatchHelper;

[AttributeUsage(AttributeTargets.Method)]
public class PatchPrefixAttribute : PatchAttribute
{
    public PatchPrefixAttribute(Type targetType, string methodName) : base(targetType, methodName) {}
}