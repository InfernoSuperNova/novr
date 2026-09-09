using NOVR.PatchHelper;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOVR.Patches.HUD;

internal static class FloatingOriginPatch
{
    private const float OriginShiftEpsilonSqr = 0.001f;

    
    [PatchPrefix(typeof(FloatingOrigin), nameof(FloatingOrigin.OriginShift))]
    private static void OriginShiftPre(out Vector3 __state)
    {
        __state = Datum.originPosition;
    }
    [PatchPostfix(typeof(FloatingOrigin), nameof(FloatingOrigin.OriginShift))]
    private static void OriginShiftPost(Vector3 __state)
    {
        if ((Datum.originPosition - __state).sqrMagnitude <= OriginShiftEpsilonSqr)
        {
            return;
        }

        StabilizePositionZeroUiRoots();
    }
    
    private static void StabilizePositionZeroUiRoots()
    {
        var activeScene = SceneManager.GetActiveScene();
        foreach (var root in activeScene.GetRootGameObjects())
        {
            StabilizePositionZeroUiRoots(root.transform);
        }
    }

    private static void StabilizePositionZeroUiRoots(Transform root)
    {
        if (root == null)
        {
            return;
        }

        if (root.GetComponent<PositionZeroBehavior>() != null)
        {
            root.position = Vector3.zero;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            StabilizePositionZeroUiRoots(root.GetChild(i));
        }
    }
}
