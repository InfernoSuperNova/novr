using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOVR.VrUi.HarmonyPatches;

internal static class FloatingOriginUiRootPatch
{
    private const float OriginShiftEpsilonSqr = 0.001f;

    [HarmonyPatch(typeof(global::FloatingOrigin), nameof(global::FloatingOrigin.OriginShift))]
    private static class OriginShiftPatch
    {
        [HarmonyPrefix]
        private static void Prefix(out Vector3 __state)
        {
            __state = global::Datum.originPosition;
        }

        [HarmonyPostfix]
        private static void Postfix(Vector3 __state)
        {
            if ((global::Datum.originPosition - __state).sqrMagnitude <= OriginShiftEpsilonSqr)
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
}
