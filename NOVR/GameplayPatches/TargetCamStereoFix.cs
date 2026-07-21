using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NOVR.GameplayPatches;

// TargetCam (the in-cockpit "locked target" screen) renders to a plain RenderTexture and was
// never meant to render to the headset, but its prefab leaves Camera.stereoTargetEye = Both.
// Forcing it to None is correct regardless of the workaround below - a texture-target camera
// shouldn't be flagged for stereo/XR rendering once NOVR enables global XR.
[HarmonyPatch(typeof(TargetCam), "Initialize")]
internal static class TargetCamStereoFix
{
      private static readonly AccessTools.FieldRef<TargetCam, Camera> CamField =
                AccessTools.FieldRefAccess<TargetCam, Camera>("cam");

      private static readonly AccessTools.FieldRef<TargetCam, Camera> UiCamField =
                AccessTools.FieldRefAccess<TargetCam, Camera>("UICam");

      [HarmonyPostfix]
      private static void Postfix(TargetCam __instance)
      {
                var cam = CamField(__instance);
                if (cam != null)
                {
                              cam.stereoTargetEye = StereoTargetEyeMask.None;
                }

                var uiCam = UiCamField(__instance);
                if (uiCam != null)
                {
                              uiCam.stereoTargetEye = StereoTargetEyeMask.None;
                }
      }
}

// Workaround for the confirmed-broken Camera.fieldOfView setter on TargetCam's camera (see
// GitHub issue InfernoSuperNova/novr#25): extensive live tracing proved the setter never
// actually commits a new value for this specific camera object, even on a same-line
// force-write, so the game's own zoom-out logic (targetFOV -> cam.fieldOfView lerp) never
// visibly applies. Bypasses fieldOfView entirely by tracking our own lerped FOV value per
// TargetCam instance and applying it directly via Camera.projectionMatrix, which is a distinct
// underlying mechanism from fieldOfView and unaffected by whatever is blocking that setter.
[HarmonyPatch(typeof(TargetCam), "Update")]
internal static class TargetCamZoomWorkaround
{
      private static readonly AccessTools.FieldRef<TargetCam, Camera> CamField =
                AccessTools.FieldRefAccess<TargetCam, Camera>("cam");

      private static readonly AccessTools.FieldRef<TargetCam, float> TargetFovField =
                AccessTools.FieldRefAccess<TargetCam, float>("targetFOV");

      // Tracks our own independent "current FOV" per TargetCam instance, since we can't trust
      // reading cam.fieldOfView back (it never reflects what was actually requested).
      private static readonly Dictionary<TargetCam, float> CurrentFov = new();

      [HarmonyPostfix]
      private static void Postfix(TargetCam __instance)
      {
                var cam = CamField(__instance);
                if (cam == null || !cam.enabled) return;

                var targetFov = TargetFovField(__instance);

                if (!CurrentFov.TryGetValue(__instance, out var current))
                {
                              current = targetFov;
                }

                current = Mathf.Lerp(current, targetFov, Time.deltaTime);
                CurrentFov[__instance] = current;

                cam.projectionMatrix = Matrix4x4.Perspective(current, cam.aspect, cam.nearClipPlane, cam.farClipPlane);
      }
}
