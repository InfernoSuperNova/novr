using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.HUD.Map;

internal static class TargetTelemetryConsistencyPatch
{
    private static readonly FieldInfo InfoSpeedField = AccessTools.Field(typeof(global::TargetMarker), "infoSpeed");
    private static readonly FieldInfo InfoAltitudeField = AccessTools.Field(typeof(global::TargetMarker), "infoAlt");
    private static readonly FieldInfo InfoHeadingField = AccessTools.Field(typeof(global::TargetMarker), "infoHeading");
    private static readonly FieldInfo InfoRangeField = AccessTools.Field(typeof(global::TargetMarker), "infoRange");
    private static readonly FieldInfo TooltipItemsField = AccessTools.Field(typeof(global::MapToolTip), "listToolTips");

    [HarmonyPatch(typeof(global::TargetMarker), "ExtraSetup")]
    private static class TargetMarkerSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::TargetMarker __instance)
        {
            RefreshTargetMarker(__instance);
        }
    }

    [HarmonyPatch(typeof(global::TargetMarker), "Update")]
    private static class TargetMarkerUpdatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(global::TargetMarker __instance, out float __state)
        {
            __state = __instance.lastRefresh;
        }

        [HarmonyPostfix]
        private static void Postfix(global::TargetMarker __instance, float __state)
        {
            if (!Mathf.Approximately(__instance.lastRefresh, __state))
                RefreshTargetMarker(__instance);
        }
    }

    [HarmonyPatch(typeof(global::MapToolTip), nameof(global::MapToolTip.RefreshAirUnitTooltip))]
    private static class AirTooltipPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::MapToolTip __instance, Unit unit)
        {
            if (!ShouldRefreshInfoTooltip())
                return;

            var items = GetTooltipItems(__instance);
            if (items == null || items.Count < 3)
                return;

            var showExact = ShouldShowExactTelemetry(unit);
            items[0].Setup(null, Color.white, showExact ? SpeedText(unit) : "SPD -", null);
            items[1].Setup(null, Color.white, showExact ? AircraftAltitudeText(unit) : "ALT -", null);
            items[2].Setup(null, Color.white, showExact ? HeadingText(unit) : "HDG -", null);
        }
    }

    [HarmonyPatch(typeof(global::MapToolTip), nameof(global::MapToolTip.RefreshMissileUnitTooltip))]
    private static class MissileTooltipPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::MapToolTip __instance, Unit unit)
        {
            if (!ShouldRefreshInfoTooltip())
                return;

            var items = GetTooltipItems(__instance);
            if (items == null || items.Count < 3)
                return;

            var showExact = ShouldShowExactTelemetry(unit);
            items[0].Setup(null, Color.white, showExact ? SpeedText(unit) : "SPD -", null);
            items[1].Setup(null, Color.white, showExact ? MissileAltitudeText(unit) : "ALT -", null);
            items[2].Setup(null, Color.white, showExact ? HeadingText(unit) : "HDG -", null);
        }
    }

    [HarmonyPatch(typeof(global::MapToolTip), nameof(global::MapToolTip.RefreshGroundUnitTooltip))]
    private static class GroundTooltipPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::MapToolTip __instance, Unit unit)
        {
            if (!ShouldRefreshInfoTooltip())
                return;

            var items = GetTooltipItems(__instance);
            if (items == null || items.Count < 3)
                return;

            var showExact = ShouldShowExactTelemetry(unit);
            items[0].Setup(null, Color.white, showExact ? SpeedText(unit) : "SPD -", null);
            items[2].Setup(null, Color.white, showExact ? HeadingText(unit) : "HDG -", null);
        }
    }

    private static void RefreshTargetMarker(global::TargetMarker marker)
    {
        var unit = marker.GetUnit();
        if (unit == null)
            return;

        var infoSpeed = (Text)InfoSpeedField.GetValue(marker);
        var infoAltitude = (Text)InfoAltitudeField.GetValue(marker);
        var infoHeading = (Text)InfoHeadingField.GetValue(marker);
        var infoRange = (Text)InfoRangeField.GetValue(marker);
        if (infoSpeed == null || infoAltitude == null || infoHeading == null || infoRange == null)
            return;

        if (ShouldShowExactTelemetry(unit))
        {
            SetText(infoSpeed, SpeedText(unit));
            SetText(infoAltitude, TargetMarkerAltitudeText(unit));
            SetText(infoHeading, HeadingText(unit));

            var aircraft = SceneSingleton<CombatHUD>.i?.aircraft;
            SetText(
                infoRange,
                aircraft != null && !aircraft.disabled
                    ? "RNG " + UnitConverter.DistanceReading(FastMath.Distance(aircraft.GlobalPosition(), unit.GlobalPosition()))
                    : string.Empty);
            return;
        }

        SetText(infoSpeed, "SPD -");
        SetText(infoAltitude, "ALT -");
        SetText(infoHeading, "HDG -");
        SetText(infoRange, "RNG -");
    }

    private static bool ShouldShowExactTelemetry(Unit unit)
    {
        if (unit == null)
            return false;

        var combatHud = SceneSingleton<CombatHUD>.i;
        if (combatHud == null || combatHud.aircraft == null || combatHud.aircraft.disabled)
            return true;

        var map = SceneSingleton<global::DynamicMap>.i;
        var localHeadquarters = map?.HQ;
        return unit.NetworkHQ == null ||
               localHeadquarters == null ||
               unit.NetworkHQ == localHeadquarters ||
               localHeadquarters.IsTargetPositionAccurate(unit, 20.0f);
    }

    private static bool ShouldRefreshInfoTooltip()
    {
        var mapOptions = SceneSingleton<MapOptions>.i;
        return mapOptions != null && mapOptions.tooltipType == MapOptions.TooltipType.Info;
    }

    private static List<TooltipItem>? GetTooltipItems(global::MapToolTip tooltip)
    {
        return TooltipItemsField.GetValue(tooltip) as List<TooltipItem>;
    }

    private static string SpeedText(Unit unit)
    {
        return "SPD " + UnitConverter.SpeedReading(unit.speed);
    }

    private static string AircraftAltitudeText(Unit unit)
    {
        return "ALT " + UnitConverter.AltitudeReading(unit.radarAlt);
    }

    private static string MissileAltitudeText(Unit unit)
    {
        return "ALT " + UnitConverter.AltitudeReading(unit.GlobalPosition().y);
    }

    private static string TargetMarkerAltitudeText(Unit unit)
    {
        if (unit is Aircraft)
            return AircraftAltitudeText(unit);
        if (unit is Missile)
            return MissileAltitudeText(unit);
        return "ALT -";
    }

    private static string HeadingText(Unit unit)
    {
        return $"HDG {Mathf.RoundToInt(unit.transform.eulerAngles.y)}°";
    }

    private static void SetText(Text text, string value)
    {
        if (text.text != value)
            text.text = value;
    }
}
