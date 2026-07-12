using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRDynamicMapBehavior : MonoBehaviour
{
    private Canvas? _canvas;
    private global::DynamicMap? _map;
    private RectTransform? _headReticleTransform;
    private Image? _headReticleImage;
    private bool _mapWasMaximized;
    private bool _mouseProjectionApplied;

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
        {
            _canvas.worldCamera = APIBus.CockpitHudCamera;
            VrCanvasHitTester.Register(_canvas);
        }

        // The game uses coordinate-math for map interaction instead of EventSystem,
        // so map graphics have raycastTarget=false by design. The VR cursor's
        // HasGraphicAtPoint check needs raycastTarget to find the map surface.
        _map = GetComponent<global::DynamicMap>();
        if (_map != null)
        {
            if (_map.mapBackground != null)
            {
                var bgImg = _map.mapBackground.GetComponent<Image>();
                if (bgImg != null) bgImg.raycastTarget = true;
            }
            if (_map.mapImage != null)
            {
                var mapImg = _map.mapImage.GetComponent<Image>();
                if (mapImg != null) mapImg.raycastTarget = true;
            }
        }
    }

    private void Update()
    {
        var maximized = global::DynamicMap.mapMaximized;
        if (maximized && !_mapWasMaximized)
        {
            ApplyMapMouseProjection();
            EnsureHeadReticle();
        }
        else if (!maximized && _mapWasMaximized)
        {
            ClearMapMouseProjection();
            SetHeadReticleVisible(false);
        }

        if (maximized)
            UpdateHeadReticle();

        _mapWasMaximized = maximized;
    }

    private void OnDisable()
    {
        ClearMapMouseProjection();
        SetHeadReticleVisible(false);
        _mapWasMaximized = false;
    }

    private void OnDestroy()
    {
        ClearMapMouseProjection();
        if (_canvas != null)
            VrCanvasHitTester.Unregister(_canvas);
    }

    private void ApplyMapMouseProjection()
    {
        if (_canvas == null)
            return;

        var cursor = VrUiCursor.I;
        if (cursor == null)
            return;

        cursor.SetProjectionReferenceRotation(_canvas.transform.rotation);
        _mouseProjectionApplied = true;
    }

    private void ClearMapMouseProjection()
    {
        if (!_mouseProjectionApplied)
            return;

        VrUiCursor.I?.ClearProjectionReferenceRotation();
        _mouseProjectionApplied = false;
    }

    private void EnsureHeadReticle()
    {
        if (_headReticleTransform != null || _canvas == null)
            return;

        var source = SceneSingleton<CombatHUD>.i?.targetDesignator;
        if (source == null)
            return;

        var reticleObject = new GameObject("NOVR Map Head Target Designator", typeof(RectTransform), typeof(Image));
        LayerHelper.SetLayerRecursive(reticleObject.transform, LayerHelper.GetVrUiLayer());

        _headReticleTransform = reticleObject.GetComponent<RectTransform>();
        _headReticleTransform.SetParent(_canvas.transform, false);
        _headReticleTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _headReticleTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _headReticleTransform.pivot = source.rectTransform.pivot;
        _headReticleTransform.sizeDelta = source.rectTransform.rect.size;
        _headReticleTransform.localScale = source.rectTransform.localScale;
        _headReticleTransform.SetAsLastSibling();

        _headReticleImage = reticleObject.GetComponent<Image>();
        _headReticleImage.raycastTarget = false;
        CopyHeadReticleAppearance(source);
    }

    private void UpdateHeadReticle()
    {
        if (_map == null || _map.mapImage == null)
        {
            SetHeadReticleVisible(false);
            return;
        }

        EnsureHeadReticle();
        if (_headReticleTransform == null || _headReticleImage == null ||
            !NOVRTargetDesignatorBehavior.TryGetHeadAimRay(out var ray) ||
            !MapSelection.TryGetLocalPoint(_map, ray, out _, out var worldPoint))
        {
            SetHeadReticleVisible(false);
            return;
        }

        var source = SceneSingleton<CombatHUD>.i?.targetDesignator;
        if (source != null)
        {
            _headReticleTransform.sizeDelta = source.rectTransform.rect.size;
            _headReticleTransform.localScale = source.rectTransform.localScale;
            CopyHeadReticleAppearance(source);
        }

        var mapRect = _map.mapImage.GetComponent<RectTransform>();
        _headReticleTransform.position = worldPoint;
        _headReticleTransform.rotation = mapRect.rotation;
        SetHeadReticleVisible(true);
    }

    private void CopyHeadReticleAppearance(Image source)
    {
        if (_headReticleImage == null)
            return;

        _headReticleImage.sprite = source.sprite;
        _headReticleImage.color = source.color;
        _headReticleImage.material = source.material;
        _headReticleImage.type = source.type;
        _headReticleImage.preserveAspect = source.preserveAspect;
    }

    private void SetHeadReticleVisible(bool visible)
    {
        if (_headReticleTransform != null && _headReticleTransform.gameObject.activeSelf != visible)
            _headReticleTransform.gameObject.SetActive(visible);
    }
}
