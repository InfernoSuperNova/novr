using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeMainMenuShell : MonoBehaviour
{
    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.88f);
    private static readonly Color PanelColor = new(0.05f, 0.065f, 0.075f, 0.92f);
    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);
    private static readonly Color ButtonHoverColor = new(0.34f, 0.40f, 0.42f, 1f);
    private static readonly Color ButtonPressedColor = new(0.16f, 0.20f, 0.22f, 1f);
    private static readonly Color ExitButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);

    private NativeVrUiRoot? _root;
    private RectTransform? _rectTransform;
    private GameObject? _originalMainCanvas;
    private Font? _font;

    public void Initialize(NativeVrUiRoot root, RectTransform rectTransform)
    {
        _root = root;
        _rectTransform = rectTransform;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildLayout();
    }

    public void SetOriginalMainCanvas(GameObject? originalMainCanvas)
    {
        _originalMainCanvas = originalMainCanvas;
    }

    private void BuildLayout()
    {
        if (_rectTransform == null) return;

        CreateImage("Background", _rectTransform, BackgroundColor, Vector2.zero, _rectTransform.sizeDelta);
        CreateText("Game Title", _rectTransform, "NUCLEAR OPTION", new Vector2(565f, 370f), new Vector2(380f, 54f), 38, TextAnchor.MiddleRight, Color.white);

        var rail = CreatePanel("Main Menu Rail", _rectTransform, PanelColor, new Vector2(-675f, 0f), new Vector2(210f, 860f));
        CreateText("Menu Header", rail, "NOVR", new Vector2(0f, 380f), new Vector2(170f, 34f), 22, TextAnchor.MiddleCenter, Color.white);

        var primaryButtons = new[]
        {
            new MenuAction("SINGLE PLAYER", new[] { "SINGLE PLAYER", "SINGLEPLAYER" }),
            new MenuAction("MULTIPLAYER", new[] { "MULTIPLAYER" }),
            new MenuAction("MISSION EDITOR", new[] { "MISSION EDITOR", "MISSIONEDITOR" }),
            new MenuAction("SETTINGS", new[] { "SETTINGS", "OPTIONS" }),
            new MenuAction("ENCYCLOPEDIA", new[] { "ENCYCLOPEDIA" }),
            new MenuAction("WORKSHOP", new[] { "WORKSHOP" })
        };

        for (var index = 0; index < primaryButtons.Length; index++)
        {
            var action = primaryButtons[index];
            CreateMenuButton(
                action.Label,
                rail,
                new Vector2(0f, 220f - index * 62f),
                new Vector2(170f, 38f),
                ButtonColor,
                () => InvokeOriginalAction(action.CandidateLabels));
        }

        CreateMenuButton(
            "EXIT GAME",
            rail,
            new Vector2(0f, -365f),
            new Vector2(170f, 38f),
            ExitButtonColor,
            () =>
            {
                if (!InvokeOriginalAction(new[] { "EXIT GAME", "EXITGAME", "QUIT" }))
                {
                    Application.Quit();
                }
            });

        var linkPanel = CreatePanel("Secondary Links", _rectTransform, PanelColor, new Vector2(-520f, -335f), new Vector2(230f, 145f));
        var secondaryButtons = new[]
        {
            new MenuAction("Change Log", new[] { "CHANGE LOG", "CHANGELOG" }),
            new MenuAction("Control Changes", new[] { "CONTROL CHANGES" }),
            new MenuAction("Development Roadmap", new[] { "DEVELOPMENT ROADMAP", "ROADMAP" }),
            new MenuAction("Join our Community", new[] { "JOIN OUR COMMUNITY", "COMMUNITY", "DISCORD" })
        };

        for (var index = 0; index < secondaryButtons.Length; index++)
        {
            var action = secondaryButtons[index];
            CreateMenuButton(
                action.Label,
                linkPanel,
                new Vector2(0f, 48f - index * 31f),
                new Vector2(190f, 24f),
                ButtonColor,
                () => InvokeOriginalAction(action.CandidateLabels),
                13);
        }

        var tipPanel = CreatePanel("Menu Tip", _rectTransform, new Color(0.02f, 0.025f, 0.032f, 0.88f), new Vector2(250f, -350f), new Vector2(520f, 86f));
        CreateText("Tip Title", tipPanel, "Did you know?", new Vector2(0f, 22f), new Vector2(480f, 24f), 15, TextAnchor.MiddleCenter, Color.white);
        CreateText("Tip Body", tipPanel, "The SAH-46 Chicane is much better protected against machine gun fire than other aircraft.", new Vector2(0f, -12f), new Vector2(460f, 36f), 14, TextAnchor.MiddleCenter, new Color(0.8f, 0.86f, 0.88f, 1f));
    }

    private bool InvokeOriginalAction(IReadOnlyCollection<string> candidateLabels)
    {
        var originalCanvas = _originalMainCanvas != null ? _originalMainCanvas : _root?.OriginalMainCanvas;
        if (originalCanvas == null)
        {
            Debug.LogWarning("[NOVR] Native main menu action ignored because the original MainCanvas is not available.");
            return false;
        }

        var buttons = originalCanvas.GetComponentsInChildren<Button>(true);
        for (var index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            var normalizedLabel = NormalizeLabel(GetButtonLabel(button));
            foreach (var candidateLabel in candidateLabels)
            {
                if (normalizedLabel != NormalizeLabel(candidateLabel)) continue;

                Debug.Log($"[NOVR] Native main menu delegated action '{candidateLabel}' to original button '{button.name}'.");
                button.onClick.Invoke();
                return true;
            }
        }

        Debug.LogWarning($"[NOVR] Native main menu could not find original button for '{string.Join(", ", candidateLabels)}'.");
        return false;
    }

    private string GetButtonLabel(Button button)
    {
        var legacyText = button.GetComponentInChildren<Text>(true);
        if (legacyText != null && !string.IsNullOrWhiteSpace(legacyText.text))
        {
            return legacyText.text;
        }

        var tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null && !string.IsNullOrWhiteSpace(tmpText.text))
        {
            return tmpText.text;
        }

        return button.name;
    }

    private static string NormalizeLabel(string label)
    {
        return label
            .Replace(" ", string.Empty)
            .Replace("\n", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\t", string.Empty)
            .ToUpperInvariant();
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        return CreateImage(name, parent, color, anchoredPosition, size);
    }

    private RectTransform CreateImage(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var image = gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }

    private void CreateText(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var textComponent = gameObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = _font;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private void CreateMenuButton(string label, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 15)
    {
        var rectTransform = CreateImage(label, parent, color, anchoredPosition, size);
        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = rectTransform.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        colors.selectedColor = ButtonHoverColor;
        colors.disabledColor = new Color(0.16f, 0.18f, 0.19f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        CreateText($"{label} Text", rectTransform, label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white);
    }

    private readonly struct MenuAction
    {
        public MenuAction(string label, string[] candidateLabels)
        {
            Label = label;
            CandidateLabels = candidateLabels;
        }

        public string Label { get; }
        public string[] CandidateLabels { get; }
    }
}
