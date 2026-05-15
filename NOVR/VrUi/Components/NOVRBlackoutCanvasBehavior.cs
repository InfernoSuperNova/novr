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
        canvas.transform.localPosition = canvas.transform.localPosition with { z = 0 };
        canvas.transform.localScale = Vector3.one * 10;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = EventBus.CockpitHudCamera;
        canvas.planeDistance = 1f;
    }
    
    private static void ApplyVrUiLayerRecursive(Transform root)
    {
        LayerHelper.SetLayerRecursive(root, LayerHelper.GetVrUiLayer());
    }
}