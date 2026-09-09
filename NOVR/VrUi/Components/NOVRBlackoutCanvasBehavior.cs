using System;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRBlackoutCanvasBehavior : MonoBehaviour
{
    
    protected virtual void Awake()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) throw new Exception($"{typeof(NOVRBlackoutCanvasBehavior)} attached to {typeof(GameObject)} without {typeof(Canvas)} component.");
        
        ApplyVrUiLayerRecursive(canvas.transform);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = APIBus.CockpitHudCamera;
        canvas.planeDistance = 1f;

        VrCanvasHitTester.Register(canvas);
    }

    protected virtual void OnDestroy()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas != null)
            VrCanvasHitTester.Unregister(canvas);
    }

    private void Update()
    {
        transform.rotation = APIBus.CockpitHudCamera.transform.rotation;
        transform.position = APIBus.CockpitHudCamera.transform.position
                             + APIBus.CockpitHudCamera.transform.forward;
    }

    private static void ApplyVrUiLayerRecursive(Transform root)
    {
        LayerHelper.SetLayerRecursive(root, LayerHelper.GetVrUiLayer());
    }
}