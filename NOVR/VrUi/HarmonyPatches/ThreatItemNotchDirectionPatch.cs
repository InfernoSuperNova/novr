using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class ThreatItemNotchDirectionPatch
{
    private const float HudDistance = 1000.0f;
    private const float NotchIndicatorDistance = 1000.0f;
    private const float NotchIndicatorLineWidth = 1.0f;
    private const float NotchIndicatorLineHeight = 4096.0f;
    private const float NotchIndicatorGapHeight = 72.0f;
    private const float NotchIndicatorFirstGapOffset = 200.0f;
    private const float NotchIndicatorGapOffsetStep = 25.0f;
    private const float NotchIndicatorGapOffsetLerpSpeed = 8.0f;
    private static readonly FieldInfo MissileField = AccessTools.Field(typeof(global::ThreatItem), "missile");
    private static readonly FieldInfo NotchLineField = AccessTools.Field(typeof(global::ThreatItem), "notchLine");
    private static readonly FieldInfo NotchIndicatorField = AccessTools.Field(typeof(global::ThreatItem), "notchIndicator");
    private static readonly FieldInfo NotchIndicatorBoxField = AccessTools.Field(typeof(global::ThreatItem), "notchIndicatorBox");
    private static readonly FieldInfo NotchIndicatorLabelField = AccessTools.Field(typeof(global::ThreatItem), "notchIndicatorLabel");
    private static readonly FieldInfo PlayerAircraftIconTransformField = AccessTools.Field(typeof(global::ThreatItem), "playerAircraftIconTransform");
    private static readonly Dictionary<global::ThreatItem, float> SmoothedGapOffsets = new();
    private static readonly Dictionary<global::ThreatItem, int> ThreatSlots = new();
    private static readonly Dictionary<Image, LineImages> LineImageCache = new();
    private static int _slotFrame = -1;
    private static int _nextSlot;

    [HarmonyPatch(typeof(global::ThreatItem), "AlignNotchLine")]
    private static class AlignNotchLinePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::ThreatItem __instance)
        {
            var notchLine = (GameObject)NotchLineField.GetValue(__instance);
            var playerAircraftIconTransform = (Transform)PlayerAircraftIconTransformField.GetValue(__instance);
            if (notchLine == null || playerAircraftIconTransform == null || !TryGetNotchDirection(__instance, out var notchDirection))
                return false;

            var flattenedNotchDirection = new Vector3(notchDirection.x, 0.0f, notchDirection.z);
            if (flattenedNotchDirection.sqrMagnitude <= Mathf.Epsilon)
                return false;
            
            var notchYawDegrees = Quaternion.LookRotation(flattenedNotchDirection, Vector3.up).eulerAngles.y;
            notchLine.transform.position = playerAircraftIconTransform.position;
            notchLine.transform.eulerAngles = new Vector3(
                0.0f,
                0.0f,
                SceneSingleton<DynamicMap>.i.mapImage.transform.eulerAngles.z - notchYawDegrees);

            return false;
        }
    }

    [HarmonyPatch(typeof(global::ThreatItem), "AlignNotchIndicator")]
    private static class AlignNotchIndicatorPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::ThreatItem __instance)
        {
            var notchIndicator = (GameObject)NotchIndicatorField.GetValue(__instance);
            if (notchIndicator == null || !TryGetNotchDirection(__instance, out var notchDirection))
                return false;

            var missile = (global::Missile)MissileField.GetValue(__instance);
            var aircraft = SceneSingleton<CombatHUD>.i.aircraft;
            var worldPosition = aircraft.GlobalPosition().ToLocalPosition() + notchDirection * NotchIndicatorDistance;
            var mainCamera = EventBus.MainCamera;
            var cockpitHudCamera = EventBus.CockpitHudCamera;
            var mainCameraLocal = mainCamera.transform.InverseTransformPoint(worldPosition);
            var hudWorldPosition = cockpitHudCamera.transform.TransformPoint(mainCameraLocal);
            var mainCameraLocalNotchDirection = mainCamera.transform.InverseTransformDirection(notchDirection);
            var hudNotchDirection = cockpitHudCamera.transform.TransformDirection(mainCameraLocalNotchDirection);

            notchIndicator.transform.position = hudWorldPosition.normalized * HudDistance;
            var indicatorUp = Vector3.ProjectOnPlane(hudNotchDirection, cockpitHudCamera.transform.forward);
            if (indicatorUp.sqrMagnitude <= Mathf.Epsilon)
                indicatorUp = cockpitHudCamera.transform.up;

            indicatorUp = Vector3.Cross(-cockpitHudCamera.transform.forward, indicatorUp).normalized;
            if (Vector3.Dot(indicatorUp, -cockpitHudCamera.transform.up) > 0.0f)
                indicatorUp *= -1.0f;
            
            notchIndicator.transform.rotation = Quaternion.LookRotation(
                cockpitHudCamera.transform.forward,
                indicatorUp);

            var distance = FastMath.Distance(aircraft.transform.position, missile.transform.position);
            var color = Color.green;
            if (missile.seekerMode == Missile.SeekerMode.activeLock)
                color = Color.Lerp(Color.yellow, Color.red, Mathf.Sin(Time.timeSinceLevelLoad * 20f) + 0.5f);
            else if (missile.seekerMode == Missile.SeekerMode.activeSearch)
                color = Color.Lerp(Color.green, Color.yellow, Mathf.Sin(Time.timeSinceLevelLoad * 10f) + 0.5f);
            
            var gapOffset = GetSmoothedGapOffset(__instance);

            var notchIndicatorBox = (Image)NotchIndicatorBoxField.GetValue(__instance);
            if (notchIndicatorBox != null)
            {
                ConfigureNotchIndicatorLines(notchIndicatorBox, color, gapOffset);
            }

            var notchIndicatorLabel = (Text)NotchIndicatorLabelField.GetValue(__instance);
            if (notchIndicatorLabel != null)
            {
                notchIndicatorLabel.text = $"[{missile.GetSeekerType()}] {UnitConverter.DistanceReading(distance)}";
                notchIndicatorLabel.color = color;
                CenterNotchIndicatorLabel(notchIndicatorLabel, gapOffset);
            }

            return false;
        }
    }

    private static bool TryGetNotchDirection(global::ThreatItem threatItem, out Vector3 notchDirection)
    {
        notchDirection = Vector3.zero;

        var missile = (global::Missile)MissileField.GetValue(threatItem);
        var aircraft = SceneSingleton<CombatHUD>.i.aircraft;
        var mainCamera = EventBus.MainCamera;
        if (missile == null || aircraft == null || mainCamera == null)
            return false;

        var evasionPointToTarget = aircraft.GlobalPosition() - missile.GetEvasionPoint();
        if (evasionPointToTarget.sqrMagnitude <= Mathf.Epsilon)
            return false;

        notchDirection = Vector3.ProjectOnPlane(mainCamera.transform.forward, evasionPointToTarget.normalized);
        if (notchDirection.sqrMagnitude <= Mathf.Epsilon)
            notchDirection = Vector3.ProjectOnPlane(aircraft.transform.forward, evasionPointToTarget.normalized);
        if (notchDirection.sqrMagnitude <= Mathf.Epsilon)
            notchDirection = Vector3.Cross(evasionPointToTarget.normalized, Vector3.up);
        if (notchDirection.sqrMagnitude <= Mathf.Epsilon)
            notchDirection = Vector3.Cross(evasionPointToTarget.normalized, Vector3.right);

        if (notchDirection.sqrMagnitude <= Mathf.Epsilon)
            return false;

        notchDirection.Normalize();
        return true;
    }
    
    private static float GetSmoothedGapOffset(global::ThreatItem threatItem)
    {
        if (_slotFrame != Time.frameCount)
        {
            _slotFrame = Time.frameCount;
            _nextSlot = 0;
            ThreatSlots.Clear();
        }
        
        if (!ThreatSlots.TryGetValue(threatItem, out var slot))
        {
            slot = _nextSlot++;
            ThreatSlots[threatItem] = slot;
        }
        
        var targetOffset = NotchIndicatorFirstGapOffset - slot * NotchIndicatorGapOffsetStep;

        if (!SmoothedGapOffsets.TryGetValue(threatItem, out var currentOffset))
        {
            SmoothedGapOffsets[threatItem] = targetOffset;
            return targetOffset;
        }

        var lerpFactor = 1.0f - Mathf.Exp(-NotchIndicatorGapOffsetLerpSpeed * Time.unscaledDeltaTime);
        currentOffset = Mathf.Lerp(currentOffset, targetOffset, lerpFactor);
        SmoothedGapOffsets[threatItem] = currentOffset;
        return currentOffset;
    }
    
    private static void ConfigureNotchIndicatorLines(Image notchIndicatorBox, Color color, float gapOffset)
    {
        notchIndicatorBox.color = Color.clear;
        notchIndicatorBox.raycastTarget = false;

        var lines = GetOrCreateLineImages(notchIndicatorBox);
        var gapHalfHeight = NotchIndicatorGapHeight * 0.5f;
        var lineSegmentHeight = (NotchIndicatorLineHeight - NotchIndicatorGapHeight) * 0.5f;
        
        ConfigureLineSegment(
            lines.Upper,
            color,
            new Vector2(0.0f, gapOffset + gapHalfHeight + lineSegmentHeight * 0.5f),
            lineSegmentHeight);
        ConfigureLineSegment(
            lines.Lower,
            color,
            new Vector2(0.0f, gapOffset - gapHalfHeight - lineSegmentHeight * 0.5f),
            lineSegmentHeight);
    }
    
    private static void CenterNotchIndicatorLabel(Text notchIndicatorLabel, float gapOffset)
    {
        if (notchIndicatorLabel.transform is not RectTransform rectTransform)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0.0f, gapOffset);
    }
    
    private static LineImages GetOrCreateLineImages(Image notchIndicatorBox)
    {
        if (LineImageCache.TryGetValue(notchIndicatorBox, out var cached) && cached.IsValid)
            return cached;

        cached = new LineImages(
            GetOrCreateLineImage(notchIndicatorBox.transform, "NOVR_NotchIndicatorUpperLine"),
            GetOrCreateLineImage(notchIndicatorBox.transform, "NOVR_NotchIndicatorLowerLine"));
        LineImageCache[notchIndicatorBox] = cached;
        return cached;
    }
    
    private static Image  GetOrCreateLineImage(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent<Image>(out var existingImage))
            return existingImage;
        
        var go = new GameObject(name)
        {
            transform =
            {
                parent = parent,
                localPosition = Vector3.zero,
                localRotation = Quaternion.identity,
                localScale = Vector3.one
            }
        };
        var rectTransform = go.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void ConfigureLineSegment(Image image, Color color, Vector2 anchoredPosition, float height)
    {
        image.color = color;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        
        if (image.transform is RectTransform rectTransform)
        {
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(NotchIndicatorLineWidth, height);
        }
    }
    
    private readonly struct LineImages
    {
        public readonly Image Upper;
        public readonly Image Lower;

        public LineImages(Image upper, Image lower)
        {
            Upper = upper;
            Lower = lower;
        }

        public bool IsValid => Upper != null && Lower != null;
    }
}
