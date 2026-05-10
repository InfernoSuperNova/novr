using System;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class UIRenderedCanvasBehavior : MonoBehaviour
{
    protected virtual void Awake()
    {
        ApplyVrUiLayerRecursive(transform);
        transform.localPosition = transform.localPosition with { z = 0 };
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) return;
        
        
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = EventBus.CockpitHudCamera;
        canvas.planeDistance = 1f;
    }
    
    private static void ApplyVrUiLayerRecursive(Transform root)
    {
        LayerHelper.SetLayerRecursive(root, LayerHelper.GetVrUiLayer());
    }


    protected static Transform FindChildStartingWith(Transform parent, string childNamePrefix)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith(childNamePrefix))
            {
                return child;
            }

            var nestedChild = FindChildStartingWith(child, childNamePrefix);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }
}