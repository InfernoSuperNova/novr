using System;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class UIRenderedCanvasBehavior : MonoBehaviour
{
    private bool _initialized;
    

    public virtual void Awake() => Initialize();

    public void OnEnable() => Initialize();

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        
        
        ApplyVrUiLayerRecursive(transform);
        transform.localPosition = transform.localPosition with { z = 0 };
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) return;
        
        
        canvas.renderMode = RenderMode.WorldSpace;
        Debug.Log($"{GetType().Name}: Set canvas render mode of {canvas.gameObject.name}. Is currently:  {canvas.renderMode}");
        canvas.worldCamera = EventBus.CockpitHudCamera;
        Debug.Log($"{GetType().Name}: Set canvas world camera of {canvas.gameObject.name}. Is currently:  {canvas.worldCamera}");
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
