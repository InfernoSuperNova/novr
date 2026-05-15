using HarmonyLib;

namespace NOVR.VrCamera;

internal static class TurretVrCameraPatch
{
    [HarmonyPatch(typeof(global::Turret), "FixedUpdate")]
    private static class FixedUpdatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::Turret __instance)
        {
            if (__instance == null ||
                !Traverse.Create(__instance).Field("manual").GetValue<bool>() ||
                Traverse.Create(__instance).Field("target").GetValue<global::Unit>() != null)
            {
                return true;
            }

            var aircraft = Traverse.Create(__instance).Field("aircraft").GetValue<global::Aircraft>();
            var attachedUnit = Traverse.Create(__instance).Field("attachedUnit").GetValue<global::Unit>();
            if (aircraft == null ||
                attachedUnit == null ||
                !aircraft.LocalSim ||
                global::SceneSingleton<global::CameraStateManager>.i.currentState != global::SceneSingleton<global::CameraStateManager>.i.cockpitState)
            {
                return true;
            }

            var vrCamera = EventBus.MainCamera;
            if (vrCamera == null)
            {
                return true;
            }

            __instance.SetVector(vrCamera.transform.forward);
            return true;
        }
    }
}
