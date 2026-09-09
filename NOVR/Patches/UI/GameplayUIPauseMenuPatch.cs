using NOVR.PatchHelper;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.Patches.UI;

internal static class GameplayUIPauseMenuPatch
{
    private static readonly Vector2 ButtonSize = new(260f, 54f);
    private static readonly Color ButtonColor = new(0.18f, 0.23f, 0.26f, 0.96f);

    private static GameObject? _recenterButtonRoot;
    private static Transform? _cachedContainer;

    [PatchPostfix(typeof(GameplayUI), "PauseGame")]
    private static void PauseGame_Postfix(GameplayUI __instance)
    {
        if (!ModConfiguration.Instance.ShowRecenterInPauseMenu.Value) return;
        if (_recenterButtonRoot != null) return;

        var canvas = __instance.menuCanvas;
        if (canvas == null) return;

        var container = LocateContainer(canvas.transform);
        if (container == null)
        {
            NOVRPlugin.LogSource?.LogMessage("[NOVR Recenter] PauseGame: could not locate pause menu container (no 'Resume' button found)");
            return;
        }

        BuildButton(container);
    }

    [PatchPostfix(typeof(GameplayUI), "ResumeGame")]
    private static void ResumeGame_Postfix()
    {
        DestroyButton();
    }

    private static Transform? LocateContainer(Transform menuCanvasTransform)
    {
        if (_cachedContainer != null && _cachedContainer.gameObject != null) return _cachedContainer;

        // The game's pause menu panel contains a button labeled "Resume Mission"; its parent
        // is the buttons container we want to attach to.
        foreach (var btn in menuCanvasTransform.GetComponentsInChildren<Button>(includeInactive: true))
        {
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp != null && tmp.text.IndexOf("Resume", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _cachedContainer = btn.transform.parent;
                return _cachedContainer;
            }
            var txt = btn.GetComponentInChildren<Text>(true);
            if (txt != null && txt.text.IndexOf("Resume", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _cachedContainer = btn.transform.parent;
                return _cachedContainer;
            }
        }

        return null;
    }

    private static void BuildButton(Transform container)
    {
        var go = new GameObject("NOVR Recenter View Button");
        go.transform.SetParent(container, false);
        LayerHelper.SetLayerRecursive(go.transform, LayerHelper.GetVrUiLayer());

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = ButtonSize;

        var image = go.AddComponent<Image>();
        image.color = ButtonColor;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(OnRecenterClicked);
        NativeButtonFeedback.Configure(button, ButtonColor);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        LayerHelper.SetLayerRecursive(textGo.transform, LayerHelper.GetVrUiLayer());

        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var text = textGo.AddComponent<Text>();
        text.text = "RECENTER VIEW";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.minHeight = ButtonSize.y;
        layoutElement.preferredHeight = ButtonSize.y;

        go.transform.SetAsLastSibling();

        _recenterButtonRoot = go;
    }

    private static void DestroyButton()
    {
        if (_recenterButtonRoot == null) return;
        Object.Destroy(_recenterButtonRoot);
        _recenterButtonRoot = null;
    }

    private static void OnRecenterClicked()
    {
        NOVRHeadsetData.CalibrateTranslation();
        NOVRHeadsetData.CalibrateRotation();
    }
}