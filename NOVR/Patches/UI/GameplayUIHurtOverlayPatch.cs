using NOVR.PatchHelper;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.UI;

internal static class GameplayUIHurtOverlayPatch
{
    [PatchPostfix(typeof(GameplayUI), "Awake")]
    private static void Awake_Postfix(GameplayUI __instance)
    {
        var hurt = __instance.hurt;
        if (hurt == null) return;

        var hurtGO = hurt.gameObject;

        var canvasGO = new GameObject("NOVR_HurtOverlayCanvas");

        var canvas = canvasGO.AddComponent<Canvas>();
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<NOVRHurtOverlayBehavior>();

        hurtGO.transform.SetParent(canvasGO.transform, false);

        var rect = hurtGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
    }
}
