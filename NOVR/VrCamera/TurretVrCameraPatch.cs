using HarmonyLib;
using UnityEngine;

namespace NOVR.VrCamera;

internal static class TurretVrCameraPatch
{
    [HarmonyPatch(typeof(Turret), "FixedUpdate")]
    private static class FixedUpdatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Turret __instance)
        {
            if (__instance == null)
            {
                return true;
            }

            var turret = Traverse.Create(__instance);
            if (!turret.Field("manual").GetValue<bool>() ||
                turret.Field("target").GetValue<Unit>() != null)
            {
                return true;
            }

            var aircraft = turret.Field("aircraft").GetValue<Aircraft>();
            var attachedUnit = turret.Field("attachedUnit").GetValue<Unit>();
            if (aircraft == null ||
                attachedUnit == null ||
                !aircraft.LocalSim ||
                SceneSingleton<CameraStateManager>.i.currentState != SceneSingleton<CameraStateManager>.i.cockpitState)
            {
                return true;
            }

            var vrCamera = APIBus.MainCamera;
            if (vrCamera == null)
            {
                return true;
            }

            __instance.SetVector(vrCamera.transform.forward);

            var lastVectorSent = turret.Field("lastVectorSent").GetValue<float>();
            if (Time.timeSinceLevelLoad - lastVectorSent > 0.20000000298023224)
            {
                var currentWeaponStation = turret.Field("currentWeaponStation").GetValue<WeaponStation>();
                var manualVector = turret.Field("manualVector").GetValue<Vector3>();
                aircraft.SetTurretVector(currentWeaponStation.Number, manualVector);
                turret.Field("lastVectorSent").SetValue(Time.timeSinceLevelLoad);
            }

            turret.Method("AimTurret", turret.Field("manualVector").GetValue<Vector3>()).GetValue();
            return false;
        }
    }
}
