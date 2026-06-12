using System;
using System.Collections;
using HarmonyLib;
using NaturalPoint.TrackIR;
using NOVR.VrUi.HarmonyPatches;
using UnityEngine;

namespace NOVR.HarmonyPatches;

internal static class AircraftEjectFix
{
    [HarmonyPatch(typeof(PilotDismounted), "Setup")]
    private static class SetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PilotDismounted __instance)
        {
            if (!UnitRegistry.TryGetUnit<Aircraft>(__instance.parentUnit, out var unit)) return;
            if (unit != SceneSingleton<CameraStateManager>.i.followingUnit ||
                __instance.pilotNumber != (byte)0) return;
            __instance.StartCoroutine(ApplyCameraPivot(__instance));
        }

        private static IEnumerator ApplyCameraPivot(PilotDismounted pilotDismounted)
        { 
            while (true)
            {
                bool shouldContinue = false;
                try
                {
                    if (pilotDismounted == null) yield break;
                

                    var head = pilotDismounted.transform.Find("pilot/pilot_armature/pelvis/chest/neck/head");
                    if (head == null) throw new Exception("head is null");
                    var helmetCamPoint = head?.Find("helmetCamPoint");
                    if (helmetCamPoint == null) throw new Exception("helmetCamPoint is null");

                    var cameraPivot = pilotDismounted.transform.Find("cameraPivot");
                    if (cameraPivot == null) throw new Exception("cameraPivot is null");

                    cameraPivot.SetPositionAndRotation(helmetCamPoint.position, helmetCamPoint.rotation);
                    head.gameObject.SetActive(false);
                    //LayerHelper.SetLayerRecursive(pilotDismounted.transform, LayerHelper.Layers.CockpitAndExternal); // TODO: Fix this
                }
                catch (Exception e)
                {
                    Debug.Log($"Aircraft ejection: {e}");
                    shouldContinue = true;
                }

                if (!shouldContinue) yield break;
                yield return null;
            }
            
        }
    }
}
