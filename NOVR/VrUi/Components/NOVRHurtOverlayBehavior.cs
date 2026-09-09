using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRHurtOverlayBehavior : MonoBehaviour
{
    private Canvas? _canvas;

    private void Awake()
    {
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null) return;

        LayerHelper.SetLayerRecursive(transform, LayerHelper.GetVrUiLayer());
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = APIBus.CockpitHudCamera;
    }

    private void Update()
    {
        var hudCam = APIBus.CockpitHudCamera;
        if (hudCam == null) return;

        var hudCamTransform = hudCam.transform;
        transform.rotation = hudCamTransform.rotation;
        transform.position = hudCamTransform.position + hudCamTransform.forward;

        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(5f, 5f);
    }
}
