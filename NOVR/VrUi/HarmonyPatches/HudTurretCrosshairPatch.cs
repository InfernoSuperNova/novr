using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrCamera;

internal static class HudTurretCrosshairPatch
{
    [HarmonyPatch(typeof(HUDTurretCrosshair), "Refresh")]
    private static class Refresh
    {
        [HarmonyPrefix]
        private static bool Prefix(HUDTurretCrosshair __instance, ref Camera mainCamera, out Vector3 crosshairPosition)
        {
            Traverse traverse = Traverse.Create(__instance);
            var turret  = traverse.Field("turret").GetValue<Turret>();
            var circle = traverse.Field("circle").GetValue<Image>();
            var readinessCircle = traverse.Field("readinessCircle").GetValue<Image>();
            var crosshair = traverse.Field("crosshair").GetValue<Image>();
            var gun = traverse.Field("gun").GetValue<Gun>();
            
            
            var direction = turret.GetDirection().normalized;
            var onTarget = turret.IsOnTarget();
            

            var crosshairDirection = EventBus.MainCamera.transform.InverseTransformDirection(direction);
            crosshairDirection = EventBus.CockpitHudCamera.transform.TransformDirection(crosshairDirection);
            
            crosshairPosition = crosshairDirection * 1000f;
            crosshair.gameObject.transform.position = crosshairPosition;
            crosshair.gameObject.transform.rotation = Quaternion.LookRotation(crosshairDirection);
            crosshair.enabled = true;

            if (gun != null)
            {
                float reloadProgress = gun.GetReloadProgress();
                if ((double) reloadProgress > 0.0)
                {
                    if (!readinessCircle.enabled)
                    {
                        readinessCircle.enabled = true;
                        crosshair.color = Color.red + Color.green * 0.5f;
                    }
                    readinessCircle.fillAmount = reloadProgress;
                }
                else if (readinessCircle.enabled)
                {
                    readinessCircle.enabled = false;
                    crosshair.color = Color.green;
                }
                circle.enabled = onTarget && (double) reloadProgress <= 0.0;
            }
            else
                circle.enabled = onTarget;
            
            return false;
        }
    }
    
}