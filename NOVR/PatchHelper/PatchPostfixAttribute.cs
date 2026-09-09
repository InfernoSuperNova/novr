using System;

namespace NOVR.PatchHelper;

[AttributeUsage(AttributeTargets.Method)]
public class PatchPostfixAttribute : PatchAttribute
{
    public PatchPostfixAttribute(Type targetType, string methodName) : base(targetType, methodName) {}
}