using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRDynamicMapBehavior : MonoBehaviour
{
    private Canvas? _canvas;

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
        var map = GetComponent<global::DynamicMap>();
        if (map != null)
        {
            if (map.mapBackground != null)
            {
                var bgImg = map.mapBackground.GetComponent<Image>();
                if (bgImg != null) bgImg.raycastTarget = true;
            }
            if (map.mapImage != null)
            {
                var mapImg = map.mapImage.GetComponent<Image>();
                if (mapImg != null) mapImg.raycastTarget = true;
            }
        }
    }

    private void OnDestroy()
    {
        if (_canvas != null)
            VrCanvasHitTester.Unregister(_canvas);
    }
}
