using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace NOVR.PatchHelper;

public static class PatchLoader
{
    public static void Apply(Harmony harmony)
    {
        NOVRLog.Info("Running and applying PatchLoader");
        var methods = Assembly.GetExecutingAssembly()
            .GetTypes()
            .SelectMany(t => t.GetMethods(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic));

        foreach (var method in methods)
        {
            HandleAttribute(method, harmony);
        }
    }

    private static void HandleAttribute(MethodInfo method, Harmony harmony)
    {
        var attr = method.GetCustomAttribute<PatchAttribute>();
        if (attr == null) return;

        switch (attr)
        {
            case PatchPrefixAttribute prefixAttr:
                HandlePrefixAttribute(prefixAttr, harmony, method);
                break;

            case PatchPostfixAttribute postfixAttr:
                HandlePostfixAttribute(postfixAttr, harmony, method);
                break;
        }
        
    }

    private static void HandlePrefixAttribute(PatchAttribute attr, Harmony harmony, MethodInfo method)
    {
        var original = AccessTools.Method(
            attr.TargetType,
            attr.MethodName);

        harmony.Patch(
            original,
            prefix: new HarmonyMethod(method));
    }

    private static void HandlePostfixAttribute(PatchAttribute attr, Harmony harmony, MethodInfo method)
    {
        var original = AccessTools.Method(
            attr.TargetType,
            attr.MethodName);

        harmony.Patch(
            original,
            postfix: new HarmonyMethod(method));
    }
}