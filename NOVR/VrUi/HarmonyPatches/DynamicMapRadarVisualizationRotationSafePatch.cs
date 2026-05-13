using HarmonyLib;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class DynamicMapRadarVisualizationRotationSafePatch
{
    private static readonly System.Type RadarMapVisType = AccessTools.Inner(typeof(global::DynamicMap), "RadarMapVis");
    private static readonly FieldInfo RadarVisualizationsField = AccessTools.Field(typeof(global::DynamicMap), "radarVisualizations");
    private static readonly FieldInfo VectorImageField = AccessTools.Field(RadarMapVisType, "vectorImage");
    private static readonly FieldInfo EmitterField = AccessTools.Field(RadarMapVisType, "emitter");
    private static readonly FieldInfo PingTimeField = AccessTools.Field(RadarMapVisType, "pingTime");
    private static readonly FieldInfo DelayField = AccessTools.Field(RadarMapVisType, "delay");

    [HarmonyPatch]
    private static class RefreshPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(RadarMapVisType, "Refresh");

        [HarmonyPrefix]
        private static bool Prefix(object __instance)
        {
            var dynamicMap = SceneSingleton<DynamicMap>.i;
            var combatHud = SceneSingleton<CombatHUD>.i;
            var vectorImage = (Image)VectorImageField.GetValue(__instance);
            var emitter = (Unit)EmitterField.GetValue(__instance);
            var pingTime = (float)PingTimeField.GetValue(__instance);
            var delay = (float)DelayField.GetValue(__instance);
            var elapsed = Time.timeSinceLevelLoad - pingTime;

            if (emitter == null ||
                elapsed >= delay ||
                emitter.disabled ||
                combatHud == null ||
                combatHud.aircraft == null ||
                combatHud.aircraft.disabled ||
                vectorImage == null)
            {
                if (vectorImage != null)
                    Object.Destroy(vectorImage.gameObject);
                
                if (dynamicMap != null)
                    ((IList)RadarVisualizationsField.GetValue(dynamicMap)).Remove(__instance);
                
                return false;
            }

            var iconLayer = dynamicMap.iconLayer.transform;
            var emitterMapPosition = emitter.GlobalPosition().AsVector3() * dynamicMap.mapDisplayFactor;
            var emitterLocalPosition = new Vector3(emitterMapPosition.x, emitterMapPosition.z, 0.0f);
            vectorImage.transform.localPosition = emitterLocalPosition;

            if (DynamicMap.TryGetMapIcon(combatHud.aircraft, out var aircraftIcon))
            {
                var aircraftLocalPosition = iconLayer.InverseTransformPoint(aircraftIcon.transform.position);
                var localDelta = aircraftLocalPosition - emitterLocalPosition;
                vectorImage.transform.localEulerAngles = new Vector3(
                    0.0f,
                    0.0f,
                    -Mathf.Atan2(localDelta.x, localDelta.y) * Mathf.Rad2Deg);
                vectorImage.transform.localScale = new Vector3(1.0f, localDelta.magnitude, 1.0f);
            }

            var color = vectorImage.color;
            color.a = Mathf.Lerp(color.a, 0.0f, elapsed * 0.05f);
            vectorImage.color = color;
            return false;
        }
    }
}
