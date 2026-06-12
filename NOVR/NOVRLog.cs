using UnityEngine;

namespace NOVR;

public class NOVRLog
{
    public static void Info(object log) => Debug.Log($"NOVR: {log}");
    public static void Warning(object log) => Debug.LogWarning($"NOVR: {log}");
    public static void Error(object log) => Debug.LogError($"NOVR: {log}");
}