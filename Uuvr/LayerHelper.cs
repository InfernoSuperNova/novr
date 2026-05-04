using UnityEngine;

namespace Uuvr;

public static class LayerHelper
{
    private static int _freeLayer = -1;
    private static int _captureLayer = -1;
    
    // Unity only lets you define 32 layers.
    // This is annoying because it's useful for us to create layers for some VR-specific stuff.
    // We try to find a free layer (one without a name), but some games use all 32 layers.
    // In that case, we need to fall back to something else.
    private static int FindFreeLayer(params int[] excludedLayers)
    {
        for (var layer = 31; layer >= 0; layer--)
        {
            if (LayerMask.LayerToName(layer).Length != 0) continue;
            if (IsExcludedLayer(layer, excludedLayers)) continue;
            if (LayerHasSceneObjects(layer)) continue;

            Debug.Log($"Found free layer: {layer}");
            return layer;
        }

        Debug.LogWarning("Failed to find a free layer to use for VR UI. Falling back to last layer.");
        return 31;
    }

    private static bool IsExcludedLayer(int layer, int[] excludedLayers)
    {
        for (var index = 0; index < excludedLayers.Length; index++)
        {
            if (excludedLayers[index] == layer)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LayerHasSceneObjects(int layer)
    {
        var gameObjects = Object.FindObjectsOfType<GameObject>();
        for (var index = 0; index < gameObjects.Length; index++)
        {
            if (gameObjects[index].layer == layer)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetFreeLayerCached()
    {
        if (_freeLayer == -1)
        {
            _freeLayer = FindFreeLayer();
        }

        return _freeLayer;
    }

    public static int GetVrUiLayer()
    {
        return 25;
        return GetFreeLayerCached();
    }

    public static int GetVrUiCaptureLayer()
    {
        if (_captureLayer == -1)
        {
            _captureLayer = FindFreeLayer(GetVrUiLayer());
        }

        return _captureLayer;
    }

    public static void SetLayerRecursive(Transform transform, int layer)
    {
        transform.gameObject.layer = layer;

        // Not using the usual foreach Transform etc because it fails in silly il2cpp.
        for (var index = 0; index < transform.childCount; index++)
        {
            var child = transform.GetChild(index);
            SetLayerRecursive(child, layer);
        }
    }
}
