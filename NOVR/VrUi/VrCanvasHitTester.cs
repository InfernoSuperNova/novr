using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace NOVR.VrUi
{
    internal readonly struct CanvasHit
    {
        public readonly Canvas Canvas;
        public readonly Vector3 WorldPoint;
        public readonly Vector2 LocalPoint;
        public readonly float Distance;
        public readonly bool HasGraphic;

        public CanvasHit(Canvas canvas, Vector3 worldPoint, Vector2 localPoint, float distance, bool hasGraphic)
        {
            Canvas = canvas;
            WorldPoint = worldPoint;
            LocalPoint = localPoint;
            Distance = distance;
            HasGraphic = hasGraphic;
        }
    }

    internal static class VrCanvasHitTester
    {
        private static readonly List<Canvas> _registeredCanvases = new();
        private static readonly List<RaycastResult> _graphicResults = new();

        /// <summary>
        /// Set by the cursor after each successful raycast to enable sticky-canvas fallback:
        /// if no candidate has a graphic at the hit point, prefer the previous frame's active
        /// canvas over the unconditionally closest plane (which a head-locked near-field canvas
        /// would win by distance).
        /// </summary>
        internal static Canvas? LastActiveCanvas { get; set; }

        public static void Register(Canvas canvas)
        {
            if (canvas != null && !_registeredCanvases.Contains(canvas))
                _registeredCanvases.Add(canvas);
        }

        public static void Unregister(Canvas canvas)
        {
            _registeredCanvases.Remove(canvas);
        }

        /// <summary>
        /// Intersect ray against registered canvases. Returns closest valid hit.
        /// If the closest canvas has no graphic at the hit point, falls through to
        /// the next-closest canvas to avoid empty regions occluding interactive content.
        /// </summary>
        /// <param name="acceptBackFace">If true, accept hits from behind the canvas plane.</param>
        private static bool IsCanvasRaycastBlocked(Canvas canvas)
        {
            var t = canvas.transform;
            while (t != null)
            {
                if (t.TryGetComponent<CanvasGroup>(out var cg) && !cg.blocksRaycasts)
                    return true;
                t = t.parent;
            }
            return false;
        }

        public static string DebugRaycast(Canvas canvas, Ray ray, bool acceptBackFace = false)
        {
            if (canvas == null) return "null";
            if (!canvas.gameObject.activeInHierarchy) return "inactive";
            if (IsCanvasRaycastBlocked(canvas)) return "blocksRaycasts=false";
            var uiCamera = APIBus.CockpitHudCamera;
            if (canvas.worldCamera != uiCamera)
                return $"worldCam!=cockpitHud (worldCam={canvas.worldCamera?.name ?? "null"})";
            var rt = canvas.GetComponent<RectTransform>();
            if (rt == null) return "noRectTransform";
            Vector3 planeNormal = rt.forward;
            Vector3 planePoint = rt.position;
            float denom = Vector3.Dot(planeNormal, ray.direction);
            if (Mathf.Abs(denom) < 0.0001f) return $"denom~0({denom:E2})";
            if (!acceptBackFace && denom < 0f) return $"backFace(denom={denom:F3})";
            float t = Vector3.Dot(planeNormal, planePoint - ray.origin) / denom;
            if (t < 0f) return $"t<0({t:F3})";
            Vector3 worldPoint = ray.GetPoint(t);
            Vector3 localPos = rt.InverseTransformPoint(worldPoint);
            Vector2 localPoint = new Vector2(localPos.x, localPos.y);
            if (!rt.rect.Contains(localPoint)) return $"outsideRect(local={localPoint:F3},rect={rt.rect})";
            return "HIT";
        }

        /// <summary>
        /// Test a single canvas against a ray. Returns true if the ray hits the canvas plane within its rect.
        /// </summary>
        public static bool RaycastSingle(Canvas canvas, Ray ray, out CanvasHit hit, bool acceptBackFace = false)
        {
            hit = default;
            string diag = DebugRaycast(canvas, ray, acceptBackFace);
            if (diag != "HIT") return false;

            var rt = canvas.GetComponent<RectTransform>();
            Vector3 planeNormal = rt.forward;
            Vector3 planePoint = rt.position;
            float denom = Vector3.Dot(planeNormal, ray.direction);
            float t = Vector3.Dot(planeNormal, planePoint - ray.origin) / denom;
            Vector3 worldPoint = ray.GetPoint(t);
            Vector3 localPos = rt.InverseTransformPoint(worldPoint);
            Vector2 localPoint = new Vector2(localPos.x, localPos.y);

            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            bool hasGraphic = false;
            if (raycaster != null)
            {
                var camera = canvas.worldCamera;
                if (camera != null)
                {
                    Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
                    var ped = new PointerEventData(EventSystem.current)
                    {
                        position = new Vector2(screenPoint.x, screenPoint.y)
                    };
                    _graphicResults.Clear();
                    raycaster.Raycast(ped, _graphicResults);
                    hasGraphic = _graphicResults.Count > 0;
                }
            }

            hit = new CanvasHit(canvas, worldPoint, localPoint, t, hasGraphic);
            return true;
        }

        public static bool RaycastCanvases(Ray ray, out CanvasHit hit, bool acceptBackFace = false)
        {
            hit = default;
            List<(float distance, Canvas canvas, Vector3 worldPoint, Vector2 localPoint)> candidates =
                new(_registeredCanvases.Count);

            var uiCamera = APIBus.CockpitHudCamera;

            foreach (var canvas in _registeredCanvases)
            {
                if (canvas == null || !canvas.gameObject.activeInHierarchy) continue;
                if (!CameraMatches(canvas, uiCamera)) continue;
                if (IsCanvasRaycastBlocked(canvas)) continue;

                var rectTransform = canvas.GetComponent<RectTransform>();
                if (rectTransform == null) continue;

                Vector3 planeNormal = rectTransform.forward;
                Vector3 planePoint = rectTransform.position;

                float denominator = Vector3.Dot(planeNormal, ray.direction);
                if (Mathf.Abs(denominator) < 0.0001f) continue;

                // uGUI convention: canvas content is readable from -Z side, so +Z (rectTransform.forward)
                // points AWAY from the viewer. A front-face hit means the ray travels WITH canvas.forward
                // (denominator > 0). Reject when the ray approaches from the opposite side (denominator < 0),
                // which means the viewer is behind the canvas looking at the non-readable side.
                if (!acceptBackFace && denominator < 0f) continue;

                float t = Vector3.Dot(planeNormal, planePoint - ray.origin) / denominator;
                if (t < 0f) continue;

                Vector3 worldPoint = ray.GetPoint(t);
                Vector3 localPos = rectTransform.InverseTransformPoint(worldPoint);
                Vector2 localPoint = new Vector2(localPos.x, localPos.y);

                if (!rectTransform.rect.Contains(localPoint)) continue;

                candidates.Add((t, canvas, worldPoint, localPoint));
            }

            if (candidates.Count == 0) return false;

            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            foreach (var (distance, canvas, worldPoint, localPoint) in candidates)
            {
                bool hasGraphic = HasGraphicAtPoint(canvas, localPoint);
                hit = new CanvasHit(canvas, worldPoint, localPoint, distance, hasGraphic);

                if (hasGraphic || candidates.Count == 1)
                    return true;
            }

            var last = candidates[0];
            hit = new CanvasHit(last.canvas, last.worldPoint, last.localPoint, last.distance, false);
            return true;
        }

        /// <summary>
        /// Intersect ray against registered canvas infinite planes (ignoring rect bounds).
        /// Returns the closest front-face plane hit that has a graphic at the intersection
        /// point (interactive content). If no plane has a graphic, returns the closest canvas
        /// as a fallback so the cursor still tracks the ray. This allows the cursor to pass
        /// through transparent canvas areas to reach interactive content on canvases behind.
        /// </summary>
        private static bool CameraMatches(Canvas canvas, Camera uiCamera)
        {
            if (canvas.worldCamera == uiCamera) return true;
            // Nested canvases may have worldCamera = null (inherits from root).
            if (canvas.worldCamera == null)
            {
                var root = canvas.rootCanvas;
                if (root != null && root != canvas && root.worldCamera == uiCamera)
                    return true;
            }
            return false;
        }

        public static bool RaycastCanvasPlanes(Ray ray, out CanvasHit hit, bool acceptBackFace = false)
        {
            hit = default;

            var uiCamera = APIBus.CockpitHudCamera;

            List<(float distance, Canvas canvas, Vector3 worldPoint, Vector2 localPoint)> candidates =
                new(_registeredCanvases.Count);

            foreach (var canvas in _registeredCanvases)
            {
                if (canvas == null || !canvas.gameObject.activeInHierarchy) continue;
                if (!CameraMatches(canvas, uiCamera)) continue;
                if (IsCanvasRaycastBlocked(canvas)) continue;

                var rt = canvas.GetComponent<RectTransform>();
                if (rt == null) continue;

                Vector3 planeNormal = rt.forward;
                Vector3 planePoint = rt.position;

                float denom = Vector3.Dot(planeNormal, ray.direction);
                if (Mathf.Abs(denom) < 0.0001f) continue;
                if (!acceptBackFace && denom < 0f) continue;

                float t = Vector3.Dot(planeNormal, planePoint - ray.origin) / denom;
                if (t < 0f) continue;

                Vector3 worldPoint = ray.GetPoint(t);
                Vector2 localPoint = rt.InverseTransformPoint(worldPoint);

                candidates.Add((t, canvas, worldPoint, localPoint));
            }

            if (candidates.Count == 0) return false;

            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            // 1. Walk closest-first; skip planes with no graphic so the cursor passes
            //    through to interactive content behind.
            foreach (var (dist, canvas, worldPt, localPt) in candidates)
            {
                if (HasGraphicAtPoint(canvas, localPt))
                {
                    hit = new CanvasHit(canvas, worldPt, localPt, dist, true);
                    return true;
                }
            }

            // 2. Sticky fallback: prefer the previous frame's active canvas if still
            //    among the plane hits. Prevents mid-drag teleport to head-locked canvases.
            if (LastActiveCanvas != null)
            {
                foreach (var (dist, canvas, worldPt, localPt) in candidates)
                {
                    if (canvas == LastActiveCanvas)
                    {
                        hit = new CanvasHit(canvas, worldPt, localPt, dist, false);
                        return true;
                    }
                }
            }

            // 3. Fallback to closest candidate whose rect actually contains the hit point.
            //    RaycastCanvasPlanes uses infinite planes, so "closest plane" without rect
            //    checking can snap to the off-panel extension of a head-locked quad.
            foreach (var (dist, canvas, worldPt, localPt) in candidates)
            {
                var rt = canvas.GetComponent<RectTransform>();
                if (rt != null && rt.rect.Contains(localPt))
                {
                    hit = new CanvasHit(canvas, worldPt, localPt, dist, false);
                    return true;
                }
            }

            // 4. Last resort: return the closest plane anyway (cursor still visible).
            var first = candidates[0];
            hit = new CanvasHit(first.canvas, first.worldPoint, first.localPoint, first.distance, false);
            return true;
        }

        private static bool HasGraphicAtPoint(Canvas canvas, Vector2 localPoint)
        {
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                // Nested canvases may not have their own GraphicRaycaster —
                // the root canvas's raycaster covers the entire hierarchy.
                var root = canvas.rootCanvas;
                if (root != null && root != canvas)
                    raycaster = root.GetComponent<GraphicRaycaster>();
            }
            if (raycaster == null) return false;

            var camera = canvas.worldCamera;
            if (camera == null)
            {
                // Nested canvas inherits worldCamera from root.
                var root = canvas.rootCanvas;
                if (root != null)
                    camera = root.worldCamera;
            }
            if (camera == null) return false;

            Vector3 worldPoint = canvas.transform.TransformPoint(localPoint);
            Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);

            var pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(screenPoint.x, screenPoint.y)
            };

            _graphicResults.Clear();
            raycaster.Raycast(pointerEventData, _graphicResults);
            return _graphicResults.Count > 0;
        }

        public static int GetRegisteredCanvasCount()
        {
            return _registeredCanvases.Count;
        }

        public static IReadOnlyList<Canvas> GetRegisteredCanvases()
        {
            return _registeredCanvases;
        }

        public static void Clear()
        {
            _registeredCanvases.Clear();
        }

        public static void DrawCanvasBounds(Canvas canvas, Color color)
        {
            var rt = canvas.GetComponent<RectTransform>();
            if (rt == null) return;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Debug.DrawLine(corners[0], corners[1], color);
            Debug.DrawLine(corners[1], corners[2], color);
            Debug.DrawLine(corners[2], corners[3], color);
            Debug.DrawLine(corners[3], corners[0], color);

            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            Debug.DrawRay(center, rt.forward * 0.3f, color);
        }

        public static void DrawAllCanvasBounds(Color color)
        {
            foreach (var canvas in _registeredCanvases)
            {
                if (canvas != null && canvas.gameObject.activeInHierarchy)
                    DrawCanvasBounds(canvas, color);
            }
        }
    }
}
