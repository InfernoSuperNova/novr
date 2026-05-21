using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class AirbaseOverlayViewPositionPatch
{
    private static readonly FieldInfo AirbaseMarkerField = AccessTools.Field(typeof(global::AirbaseOverlay), "airbaseMarker");
    private static readonly FieldInfo AirbaseLabelField = AccessTools.Field(typeof(global::AirbaseOverlay), "airbaseLabel");
    private static readonly FieldInfo NearestAirbaseField = AccessTools.Field(typeof(global::AirbaseOverlay), "nearestAirbase");
    private static readonly FieldInfo RunwayUsageField = AccessTools.Field(typeof(global::AirbaseOverlay), "runwayUsage");
    private static readonly FieldInfo LandingField = AccessTools.Field(typeof(global::AirbaseOverlay), "landing");
    private static readonly FieldInfo TaxiingToRunwayField = AccessTools.Field(typeof(global::AirbaseOverlay), "taxiingToRunway");
    private static readonly FieldInfo ReachedRunwayField = AccessTools.Field(typeof(global::AirbaseOverlay), "reachedRunway");
    private static readonly FieldInfo RunwayBordersField = AccessTools.Field(typeof(global::AirbaseOverlay), "runwayBorders");
    private static readonly FieldInfo GlideslopeField = AccessTools.Field(typeof(global::AirbaseOverlay), "glideslope");
    private static readonly FieldInfo GlideslopeAimPointField = AccessTools.Field(typeof(global::AirbaseOverlay), "glideslopeAimPoint");

    [HarmonyPatch(typeof(global::AirbaseOverlay), "LateUpdate")]
    private static class LateUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::AirbaseOverlay __instance)
        {
            var aircraft = SceneSingleton<CombatHUD>.i.aircraft;
            var mainCamera = EventBus.MainCamera;
            var cockpitHudCamera = EventBus.CockpitHudCamera;
            if (aircraft == null || mainCamera == null || cockpitHudCamera == null)
                return;

            __instance.transform.rotation = cockpitHudCamera.transform.rotation;

            UpdateAirbaseMarker(__instance, aircraft);
            UpdateRunwayBorders(__instance);
            UpdateGlideslope(__instance, aircraft);
        }
    }

    private static void UpdateAirbaseMarker(global::AirbaseOverlay overlay, Aircraft aircraft)
    {
        var airbaseMarker = (Image)AirbaseMarkerField.GetValue(overlay);
        var airbaseLabel = (Text)AirbaseLabelField.GetValue(overlay);
        var nearestAirbase = (Airbase)NearestAirbaseField.GetValue(overlay);
        var runwayUsage = GetRunwayUsage(overlay);
        var landing = (bool)LandingField.GetValue(overlay);
        var taxiingToRunway = (bool)TaxiingToRunwayField.GetValue(overlay);
        var reachedRunway = (bool)ReachedRunwayField.GetValue(overlay);
        var cockpitHudCamera = EventBus.CockpitHudCamera;
        if (airbaseMarker == null || airbaseLabel == null || nearestAirbase == null || cockpitHudCamera == null || !airbaseMarker.enabled)
            return;

        if (landing || aircraft.radarAlt < 1.0f && !taxiingToRunway)
            return;

        var markerWorldPosition = nearestAirbase.center.position;
        if (!aircraft.pilots[0].flightInfo.HasTakenOff)
        {
            if (!taxiingToRunway || reachedRunway || !runwayUsage.HasValue)
                return;

            markerWorldPosition = runwayUsage.Value.GetEnd().position;
        }

        if (VrHudProjection.PinToScreenEdge(markerWorldPosition, out var markerHudPosition))
        {
            airbaseMarker.transform.position = markerHudPosition;
            airbaseLabel.transform.position = markerHudPosition - markerHudPosition.normalized * 50.0f;
        }
        else
        {
            airbaseMarker.transform.position = markerHudPosition;
            airbaseLabel.transform.position = markerHudPosition - Vector3.up * 20.0f;
        }

        var rotation = Quaternion.LookRotation(airbaseMarker.transform.position - cockpitHudCamera.transform.position);
        
        airbaseMarker.transform.rotation = rotation;
        airbaseLabel.transform.rotation = rotation;
    }

    private static void UpdateRunwayBorders(global::AirbaseOverlay overlay)
    {
        var runwayUsage = GetRunwayUsage(overlay);
        var landing = (bool)LandingField.GetValue(overlay);
        var runwayBorders = (Image[])RunwayBordersField.GetValue(overlay);
        var cockpitHudCamera = EventBus.CockpitHudCamera;
        if (!landing || !runwayUsage.HasValue || runwayBorders == null || runwayBorders.Length < 4 || cockpitHudCamera == null)
            return;

        var runway = runwayUsage.Value.Runway;
        if (runway == null)
            return;

        var width = runway.GetWidth();
        var start = runway.Start;
        var end = runway.End;
        var corners = new[]
        {
            start.position - 0.5f * width * start.right,
            start.position + 0.5f * width * start.right,
            end.position + 0.5f * width * end.right,
            end.position - 0.5f * width * end.right
        };

        var hudCorners = new Vector3[4];
        for (var i = 0; i < corners.Length; i++)
        {
            if (!VrHudProjection.TryProjectToCockpitHud(corners[i], out hudCorners[i]))
            {
                SetRunwayBordersEnabled(runwayBorders, false);
                return;
            }
        }

        SetRunwayBordersEnabled(runwayBorders, true);
        for (var i = 0; i < runwayBorders.Length && i < 4; i++)
        {
            var nextIndex = (i + 1) % 4;
            if (runwayBorders[i] != null)
                VrHudProjection.SetVerticalLine(runwayBorders[i].transform, hudCorners[i], hudCorners[nextIndex], cockpitHudCamera);
        }
    }

    private static void UpdateGlideslope(global::AirbaseOverlay overlay, Aircraft aircraft)
    {
        var runwayUsage = GetRunwayUsage(overlay);
        var glideslope = (Image)GlideslopeField.GetValue(overlay);
        var glideslopeAimPoint = (Image)GlideslopeAimPointField.GetValue(overlay);
        var cockpitHudCamera = EventBus.CockpitHudCamera;
        if (!runwayUsage.HasValue || runwayUsage.Value.Runway == null || glideslope == null || glideslopeAimPoint == null ||
            cockpitHudCamera == null || !glideslope.enabled)
            return;

        var runwayEndPosition = runwayUsage.Value.GetEnd().position;
        var distanceToRunwayEnd = FastMath.Distance(aircraft.transform.position, runwayEndPosition);
        var runwayVelocity = runwayUsage.Value.Runway.GetVelocity();
        var closingSpeed = Vector3.Dot(aircraft.rb.velocity - runwayVelocity, (runwayEndPosition - aircraft.transform.position).normalized);
        var timeToRunwayEnd = distanceToRunwayEnd / closingSpeed;
        var aimPointWorldPosition = runwayUsage.Value.Runway.GetGlideslopeAimpoint(
            aircraft,
            distanceToRunwayEnd * 0.9f,
            runwayUsage.Value.Reverse,
            timeToRunwayEnd * 0.9f);

        if (!VrHudProjection.TryProjectToCockpitHud(runwayEndPosition, out var runwayEndHudPosition) ||
            !VrHudProjection.TryProjectToCockpitHud(aimPointWorldPosition, out var aimPointHudPosition))
        {
            glideslope.enabled = false;
            glideslopeAimPoint.enabled = false;
            return;
        }

        VrHudProjection.SetVerticalLine(glideslope.transform, runwayEndHudPosition, aimPointHudPosition, cockpitHudCamera, -8.0f);
        glideslopeAimPoint.transform.position = aimPointHudPosition;
        glideslopeAimPoint.transform.rotation = cockpitHudCamera.transform.rotation;
    }

    private static Airbase.Runway.RunwayUsage? GetRunwayUsage(global::AirbaseOverlay overlay) =>
        (Airbase.Runway.RunwayUsage?)RunwayUsageField.GetValue(overlay);

    private static void SetRunwayBordersEnabled(Image[] runwayBorders, bool enabled)
    {
        foreach (var runwayBorder in runwayBorders)
        {
            if (runwayBorder != null)
                runwayBorder.enabled = enabled;
        }
    }

}
