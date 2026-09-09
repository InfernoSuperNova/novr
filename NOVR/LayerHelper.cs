using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NOVR;

public static class LayerHelper
{
    
    
    // Check me on game updates!!
    [Flags]
    public enum Layers
    {
        
        // Base game
        Default = 0,
        TransparentFX = 1,
        IgnoreRaycast = 2,
        Cockpit = 3,
        Water = 4,
        UI = 5,
        Statics = 6,
        PP = 7,
        TargetCamPP =8,
        HUD = 9,
        Effects = 10,
        Ship = 11,
        Sun = 12,
        ExclusionZones = 13,
        CockpitAndExternal = 14,
        IgnoreCollision = 15,
        PreviewRender = 16,
        EditorSelectOnly = 17,
        GrassBlockerProxy = 18,
        
        
        
        // Ours
        VrUiClippedHud = 29,
        VrUi = 30,
        VrUiCapture = 31,
    }

    public static Layers GetVrUiLayer() => Layers.VrUi;

    public static Layers GetVrUiClippedHudLayer() => Layers.VrUiClippedHud;

    public static Layers GetVrUiCaptureLayer() => Layers.VrUiCapture;

    public static void SetLayerRecursive(Transform transform, Layers layer)
    {
        transform.gameObject.layer = (int)layer;

        // Not using the usual foreach Transform etc because it fails in silly il2cpp.
        for (var index = 0; index < transform.childCount; index++)
        {
            var child = transform.GetChild(index);
            SetLayerRecursive(child, layer);
        }
    }
}
