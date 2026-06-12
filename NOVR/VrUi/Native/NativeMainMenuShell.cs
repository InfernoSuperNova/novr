using System;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeMainMenuShell : MonoBehaviour
{
    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.24f);
    private static readonly Color PanelColor = new(0.05f, 0.065f, 0.075f, 0.92f);
    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);
    private static readonly Color ButtonHoverColor = new(0.34f, 0.40f, 0.42f, 1f);
    private static readonly Color ButtonPressedColor = new(0.16f, 0.20f, 0.22f, 1f);
    private static readonly Color ExitButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);
    private const float PrimaryButtonStartY = 250f;
    private const float PrimaryButtonSpacingY = 68f;

    private NativeGameActionAdapter? _actions;
    private RectTransform? _rectTransform;
    private RectTransform? _containerTransform;
    private GameObject? _container;
    private GameObject? _sourceMainCanvas;
    private GameObject? _sourceBackgroundObject;
    private Font? _font;
    private Action? _openVrUiSettings;
    private int _sourceBackgroundGraphicId;
    private BackgroundGraphicKind _sourceBackgroundKind;
    private bool _loggedMissingBackground;

    public void Initialize(NativeGameActionAdapter actions, RectTransform rectTransform, Action openVrUiSettings)
    {
        _actions = actions;
        _rectTransform = rectTransform;
        _openVrUiSettings = openVrUiSettings;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildLayout();
    }

    private void BuildLayout()
    {
        if (_rectTransform == null) return;

        var container = CreateContainer("Native Main Menu Shell", _rectTransform, _rectTransform.sizeDelta);
        _containerTransform = container;
        _container = container.gameObject;

        CreateImage("Background", container, BackgroundColor, Vector2.zero, container.sizeDelta);
        CreateText("Header", container, "MAIN MENU", new Vector2(0f, NativeUiLayout.HeaderY), NativeUiLayout.HeaderSize, 22, TextAnchor.MiddleCenter, Color.white);
        CreateText("Game Title", container, "NUCLEAR OPTION", new Vector2(760f, 470f), new Vector2(440f, 60f), 40, TextAnchor.MiddleRight, Color.white);

        var rail = CreatePanel("Main Menu Rail", container, PanelColor, new Vector2(-850f, -15f), new Vector2(250f, 950f));
        CreateLogo("Menu Header Logo", rail, new Vector2(0f, 405f), new Vector2(118f, 118f));
        CreateText("Menu Header", rail, "NOVR", new Vector2(0f, 325f), new Vector2(210f, 30f), 20, TextAnchor.MiddleCenter, Color.white);

        var primaryButtons = new[]
        {
            new MenuButton("SINGLE PLAYER", () => InvokeAction(NativeGameAction.SinglePlayer)),
            new MenuButton("MULTIPLAYER", () => InvokeAction(NativeGameAction.Multiplayer)),
            new MenuButton("SETTINGS", () => InvokeAction(NativeGameAction.Settings)),
            new MenuButton("VR UI SETTINGS", OpenVrUiSettings),
            new MenuButton("ENCYCLOPEDIA", () => InvokeAction(NativeGameAction.Encyclopedia)),
            new MenuButton("WORKSHOP", () => InvokeAction(NativeGameAction.Workshop))
        };

        for (var index = 0; index < primaryButtons.Length; index++)
        {
            var button = primaryButtons[index];
            CreateMenuButton(
                button.Label,
                rail,
                new Vector2(0f, PrimaryButtonStartY - index * PrimaryButtonSpacingY),
                new Vector2(205f, 44f),
                ButtonColor,
                () => button.Action.Invoke(),
                button.Label.Length > 12 ? 14 : 16);
        }

        CreateMenuButton(
            "EXIT GAME",
            rail,
            new Vector2(0f, -420f),
            new Vector2(205f, 44f),
            ExitButtonColor,
            () => _actions?.QuitGame(),
            16);

        var linkPanel = CreatePanel("Secondary Links", container, PanelColor, new Vector2(-610f, -365f), new Vector2(280f, 165f));
        var secondaryButtons = new[]
        {
            new MenuAction("Change Log", NativeGameAction.ChangeLog),
            new MenuAction("Control Changes", NativeGameAction.ControlChanges),
            new MenuAction("Development Roadmap", NativeGameAction.DevelopmentRoadmap),
            new MenuAction("Join our Community", NativeGameAction.Community)
        };

        for (var index = 0; index < secondaryButtons.Length; index++)
        {
            var action = secondaryButtons[index];
            CreateMenuButton(
                action.Label,
                linkPanel,
                new Vector2(0f, 55f - index * 35f),
                new Vector2(235f, 28f),
                ButtonColor,
                () => InvokeAction(action.Action),
                13);
        }

        var tipPanel = CreatePanel("Menu Tip", container, new Color(0.02f, 0.025f, 0.032f, 0.88f), new Vector2(320f, -430f), new Vector2(650f, 92f));
        CreateText("Tip Title", tipPanel, "Did you know?", new Vector2(0f, 24f), new Vector2(600f, 24f), 15, TextAnchor.MiddleCenter, Color.white);
        CreateText("Tip Body", tipPanel, "The SAH-46 Chicane is much better protected against machine gun fire than other aircraft.", new Vector2(0f, -12f), new Vector2(590f, 40f), 14, TextAnchor.MiddleCenter, new Color(0.8f, 0.86f, 0.88f, 1f));
        NativePanelTransition.SetVisible(container, false, instant: true);
    }

    public void SetOriginalMainCanvas(GameObject? sourceMainCanvas)
    {
        if (_sourceMainCanvas == sourceMainCanvas) return;

        _sourceMainCanvas = sourceMainCanvas;
        _sourceBackgroundGraphicId = 0;
        _sourceBackgroundKind = BackgroundGraphicKind.None;
        _loggedMissingBackground = false;

        if (_sourceBackgroundObject != null)
        {
            Destroy(_sourceBackgroundObject);
            _sourceBackgroundObject = null;
        }
    }

    public void SetVisible(bool visible)
    {
        if (visible && ShouldUseSourceBackground())
        {
            SyncSourceBackground();
        }
        else
        {
            ClearSourceBackground();
        }

        if (_containerTransform != null)
        {
            NativePanelTransition.SetVisible(_containerTransform, visible);
        }
    }

    private void InvokeAction(NativeGameAction action)
    {
        _actions?.TryInvoke(action);
    }

    private void OpenVrUiSettings()
    {
        _openVrUiSettings?.Invoke();
    }

    private static bool ShouldUseSourceBackground()
    {
        return !ModConfiguration.Instance.EnableNativeMenuEnvironment.Value;
    }

    private void ClearSourceBackground()
    {
        _sourceBackgroundGraphicId = 0;
        _sourceBackgroundKind = BackgroundGraphicKind.None;

        if (_sourceBackgroundObject == null) return;

        Destroy(_sourceBackgroundObject);
        _sourceBackgroundObject = null;
    }

    private void SyncSourceBackground()
    {
        if (_containerTransform == null || _sourceMainCanvas == null) return;

        var source = FindBestSourceBackground(_sourceMainCanvas);
        if (source == null)
        {
            if (!_loggedMissingBackground)
            {
                _loggedMissingBackground = true;
                Debug.LogWarning("[NOVR] Native main menu could not find a source background graphic under the original MainCanvas.");
            }

            return;
        }

        var kind = source is RawImage ? BackgroundGraphicKind.RawImage : BackgroundGraphicKind.Image;
        var sourceId = source.GetInstanceID();
        if (_sourceBackgroundObject == null || _sourceBackgroundGraphicId != sourceId || _sourceBackgroundKind != kind)
        {
            if (_sourceBackgroundObject != null)
            {
                Destroy(_sourceBackgroundObject);
            }

            _sourceBackgroundObject = CreateSourceBackgroundObject(kind);
            _sourceBackgroundGraphicId = sourceId;
            _sourceBackgroundKind = kind;
            Debug.Log($"[NOVR] Native main menu using original background graphic '{GetGameObjectPath(source.gameObject)}'.");
        }

        CopySourceBackground(source);
    }

    private GameObject CreateSourceBackgroundObject(BackgroundGraphicKind kind)
    {
        var gameObject = new GameObject("Original Main Menu Background");
        gameObject.transform.SetParent(_containerTransform, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = _containerTransform != null ? _containerTransform.sizeDelta : new Vector2(2000f, 1125f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.SetAsFirstSibling();

        if (kind == BackgroundGraphicKind.RawImage)
        {
            gameObject.AddComponent<RawImage>().raycastTarget = false;
        }
        else
        {
            var image = gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
        }

        return gameObject;
    }

    private void CopySourceBackground(Graphic source)
    {
        if (_sourceBackgroundObject == null) return;

        if (source is RawImage sourceRawImage &&
            _sourceBackgroundObject.TryGetComponent<RawImage>(out var targetRawImage))
        {
            targetRawImage.texture = sourceRawImage.texture;
            targetRawImage.uvRect = sourceRawImage.uvRect;
            targetRawImage.color = sourceRawImage.color;
            targetRawImage.material = sourceRawImage.material;
            targetRawImage.raycastTarget = false;
            return;
        }

        if (source is Image sourceImage &&
            _sourceBackgroundObject.TryGetComponent<Image>(out var targetImage))
        {
            targetImage.sprite = sourceImage.sprite;
            targetImage.type = sourceImage.type;
            targetImage.preserveAspect = true;
            targetImage.fillCenter = sourceImage.fillCenter;
            targetImage.fillMethod = sourceImage.fillMethod;
            targetImage.fillOrigin = sourceImage.fillOrigin;
            targetImage.fillAmount = sourceImage.fillAmount;
            targetImage.color = sourceImage.color;
            targetImage.material = sourceImage.material;
            targetImage.raycastTarget = false;
        }
    }

    private static Graphic? FindBestSourceBackground(GameObject sourceMainCanvas)
    {
        Graphic? bestGraphic = null;
        var bestScore = 0f;

        var rawImages = sourceMainCanvas.GetComponentsInChildren<RawImage>(true);
        for (var index = 0; index < rawImages.Length; index++)
        {
            var rawImage = rawImages[index];
            if (rawImage.texture == null || !TryScoreSourceBackground(rawImage, out var score)) continue;
            if (score <= bestScore) continue;

            bestGraphic = rawImage;
            bestScore = score;
        }

        var images = sourceMainCanvas.GetComponentsInChildren<Image>(true);
        for (var index = 0; index < images.Length; index++)
        {
            var image = images[index];
            if (image.sprite == null || !TryScoreSourceBackground(image, out var score)) continue;
            if (score <= bestScore) continue;

            bestGraphic = image;
            bestScore = score;
        }

        return bestGraphic;
    }

    private static bool TryScoreSourceBackground(Graphic graphic, out float score)
    {
        score = 0f;
        if (!graphic.enabled || !graphic.gameObject.activeInHierarchy || graphic.color.a <= 0.01f)
        {
            return false;
        }

        if (graphic.GetComponent<Button>() != null)
        {
            return false;
        }

        var rect = graphic.rectTransform.rect;
        var area = Mathf.Abs(rect.width * rect.height);
        if (area < 100000f)
        {
            return false;
        }

        score = area;

        var name = graphic.gameObject.name.ToLowerInvariant();
        if (name.Contains("background") || name.Contains("backdrop") || name.Contains("image"))
        {
            score *= 2f;
        }

        if (graphic is RawImage)
        {
            score *= 1.25f;
        }

        return true;
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        return CreateImage(name, parent, color, anchoredPosition, size);
    }

    private RectTransform CreateContainer(string name, RectTransform parent, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;
        return rectTransform;
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

    private void CreateLogo(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        var texture = NativeMainMenuLogo.GetTexture();
        if (texture == null) return;

        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var image = gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
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

        NativeButtonFeedback.Configure(button, color);

        CreateText($"{label} Text", rectTransform, label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white);
    }

    private static string GetGameObjectPath(GameObject gameObject)
    {
        var path = gameObject.name;
        var parent = gameObject.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private enum BackgroundGraphicKind
    {
        None,
        Image,
        RawImage
    }

    private readonly struct MenuAction
    {
        public MenuAction(string label, NativeGameAction action)
        {
            Label = label;
            Action = action;
        }

        public string Label { get; }
        public NativeGameAction Action { get; }
    }

    private readonly struct MenuButton
    {
        public MenuButton(string label, Action action)
        {
            Label = label;
            Action = action;
        }

        public string Label { get; }
        public Action Action { get; }
    }
}
