using System;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public sealed class NativeVrUiSettingsPanel : MonoBehaviour
{
    private const float DefaultScale = 1.25f;
    private const float DefaultDistance = 3.0f;
    private const float DefaultHeightOffset = 0.0f;
    private const float MinScale = 0.75f;
    private const float MaxScale = 2.0f;
    private const float MinDistance = 1.5f;
    private const float MaxDistance = 6.0f;
    private const float MinHeightOffset = -0.25f;
    private const float MaxHeightOffset = 1.0f;

    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.93f);
    private static readonly Color PanelColor = new(0.05f, 0.06f, 0.065f, 0.94f);
    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);
    private static readonly Color BackButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);
    private static readonly Color ActionButtonColor = new(0.12f, 0.34f, 0.20f, 0.96f);
    private static readonly Color ToggleOnColor = new(0.12f, 0.34f, 0.20f, 0.96f);
    private static readonly Color ToggleOffColor = new(0.54f, 0.16f, 0.14f, 0.96f);

    private RectTransform? _container;
    private Font? _font;
    private Text? _nativeUiValueText;
    private Button? _nativeUiToggleButton;
    private Text? _environmentValueText;
    private Button? _environmentToggleButton;
    private Text? _scaleValueText;
    private Text? _distanceValueText;
    private Text? _heightValueText;
    private Text? _statusText;
    private Action? _close;
    private Action? _recenter;

    public void Initialize(RectTransform root, Action close, Action recenter)
    {
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _close = close;
        _recenter = recenter;
        BuildLayout(root);
    }

    public void SetVisible(bool visible)
    {
        if (_container == null) return;

        if (visible)
        {
            RefreshValues();
        }

        NativePanelTransition.SetVisible(_container, visible);
    }

    private void BuildLayout(RectTransform root)
    {
        _container = CreateContainer("Native VR UI Settings", root, root.sizeDelta);
        CreateImage("Background", _container, BackgroundColor, Vector2.zero, _container.sizeDelta);
        CreateText("Header", _container, "VR UI SETTINGS", new Vector2(0f, NativeUiLayout.HeaderY), NativeUiLayout.HeaderSize, 22, TextAnchor.MiddleCenter, Color.white);

        var panel = CreatePanel("VR UI Settings Panel", _container, PanelColor, Vector2.zero, new Vector2(980f, 720f));
        CreateText("Panel Header", panel, "MENU MODE", new Vector2(0f, 310f), new Vector2(860f, 34f), 19, TextAnchor.MiddleCenter, Color.white);

        CreateToggleRow(
            panel,
            "NATIVE VR UI",
            "Use NOVR's native menu instead of the stock rendered UI.",
            new Vector2(0f, 230f),
            ToggleNativeUi,
            out _nativeUiToggleButton,
            out _nativeUiValueText);

        CreateToggleRow(
            panel,
            "3D MENU ENVIRONMENT",
            "Show an experimental scene behind the native menu.",
            new Vector2(0f, 140f),
            ToggleEnvironment,
            out _environmentToggleButton,
            out _environmentValueText);

        CreateText("Placement Header", panel, "PLACEMENT", new Vector2(0f, 50f), new Vector2(860f, 30f), 17, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));

        CreateSettingRow(
            panel,
            "SCALE",
            "Overall native menu size.",
            new Vector2(0f, -30f),
            () => ChangeScale(-0.05f),
            () => ChangeScale(0.05f),
            out _scaleValueText);

        CreateSettingRow(
            panel,
            "DISTANCE",
            "Meters from your headset when opened or recentered.",
            new Vector2(0f, -125f),
            () => ChangeDistance(-0.1f),
            () => ChangeDistance(0.1f),
            out _distanceValueText);

        CreateSettingRow(
            panel,
            "HEIGHT OFFSET",
            "Vertical offset in meters relative to your headset.",
            new Vector2(0f, -220f),
            () => ChangeHeightOffset(-0.05f),
            () => ChangeHeightOffset(0.05f),
            out _heightValueText);

        CreateMenuButton("RESET DEFAULTS", panel, new Vector2(-160f, -310f), new Vector2(220f, 42f), ButtonColor, ResetDefaults, 13);
        CreateMenuButton("RECENTER", panel, new Vector2(160f, -310f), new Vector2(180f, 42f), ActionButtonColor, Recenter, 14);
        _statusText = CreateText("Status", panel, "", new Vector2(0f, -340f), new Vector2(860f, 34f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));

        CreateMenuButton("BACK", _container, new Vector2(NativeUiLayout.FooterLeftX, NativeUiLayout.FooterY), NativeUiLayout.FooterButtonSize, BackButtonColor, Close, 15);
        NativePanelTransition.SetVisible(_container, false, instant: true);
    }

    private void CreateToggleRow(
        RectTransform parent,
        string label,
        string description,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction onClick,
        out Button toggleButton,
        out Text valueText)
    {
        var row = CreatePanel($"{label} Row", parent, new Color(0.08f, 0.095f, 0.105f, 0.72f), anchoredPosition, new Vector2(860f, 82f));
        CreateText($"{label} Label", row, label, new Vector2(-280f, 16f), new Vector2(250f, 28f), 17, TextAnchor.MiddleLeft, Color.white);
        CreateText($"{label} Description", row, description, new Vector2(-280f, -18f), new Vector2(430f, 28f), 12, TextAnchor.MiddleLeft, new Color(0.75f, 0.82f, 0.84f, 1f));
        toggleButton = CreateMenuButton("ON", row, new Vector2(290f, 0f), new Vector2(150f, 40f), ToggleOnColor, onClick, 15);
        valueText = toggleButton.GetComponentInChildren<Text>();
    }

    private void CreateSettingRow(
        RectTransform parent,
        string label,
        string description,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction decrease,
        UnityEngine.Events.UnityAction increase,
        out Text valueText)
    {
        var row = CreatePanel($"{label} Row", parent, new Color(0.08f, 0.095f, 0.105f, 0.72f), anchoredPosition, new Vector2(860f, 82f));
        CreateText($"{label} Label", row, label, new Vector2(-280f, 16f), new Vector2(220f, 28f), 17, TextAnchor.MiddleLeft, Color.white);
        CreateText($"{label} Description", row, description, new Vector2(-280f, -18f), new Vector2(360f, 28f), 12, TextAnchor.MiddleLeft, new Color(0.75f, 0.82f, 0.84f, 1f));
        CreateMenuButton("-", row, new Vector2(120f, 0f), new Vector2(54f, 38f), ButtonColor, decrease, 18);
        valueText = CreateText($"{label} Value", row, "", new Vector2(230f, 0f), new Vector2(130f, 38f), 16, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("+", row, new Vector2(340f, 0f), new Vector2(54f, 38f), ButtonColor, increase, 18);
    }

    private void ChangeScale(float delta)
    {
        var config = ModConfiguration.Instance;
        var value = RoundToStep(Mathf.Clamp(config.NativeMenuScale.Value + delta, MinScale, MaxScale), 0.05f);
        config.NativeMenuScale.Value = value;
        SaveAndRefresh("Scale updated.");
    }

    private void ChangeDistance(float delta)
    {
        var config = ModConfiguration.Instance;
        var value = RoundToStep(Mathf.Clamp(config.NativeMenuDistance.Value + delta, MinDistance, MaxDistance), 0.1f);
        config.NativeMenuDistance.Value = value;
        SaveAndRefresh("Distance updated.");
    }

    private void ChangeHeightOffset(float delta)
    {
        var config = ModConfiguration.Instance;
        var value = RoundToStep(Mathf.Clamp(config.NativeMenuHeightOffset.Value + delta, MinHeightOffset, MaxHeightOffset), 0.05f);
        config.NativeMenuHeightOffset.Value = value;
        SaveAndRefresh("Height offset updated.");
    }

    private void ResetDefaults()
    {
        var config = ModConfiguration.Instance;
        config.NativeMenuScale.Value = DefaultScale;
        config.NativeMenuDistance.Value = DefaultDistance;
        config.NativeMenuHeightOffset.Value = DefaultHeightOffset;
        SaveAndRefresh("VR UI settings reset.");
    }

    private void ToggleNativeUi()
    {
        var config = ModConfiguration.Instance;
        var nextValue = !config.EnableNativeMenuUi.Value;
        config.EnableNativeMenuUi.Value = nextValue;
        config.Config.Save();
        RefreshValues();
        SetStatus(nextValue
            ? "Native VR UI enabled."
            : "Native VR UI disabled. Use the headset VR UI ON button to return.");
    }

    private void ToggleEnvironment()
    {
        var config = ModConfiguration.Instance;
        var nextValue = !config.EnableNativeMenuEnvironment.Value;
        config.EnableNativeMenuEnvironment.Value = nextValue;
        config.Config.Save();
        RefreshValues();
        SetStatus(nextValue
            ? "3D menu environment enabled."
            : "3D menu environment disabled.");
    }

    private void Recenter()
    {
        _recenter?.Invoke();
        SetStatus("Recenter queued. Look where you want the menu.");
    }

    private void Close()
    {
        _close?.Invoke();
    }

    private void SaveAndRefresh(string status)
    {
        ModConfiguration.Instance.Config.Save();
        RefreshValues();
        SetStatus(status);
    }

    private void RefreshValues()
    {
        var config = ModConfiguration.Instance;
        RefreshNativeUiToggle(config.EnableNativeMenuUi.Value);
        RefreshEnvironmentToggle(config.EnableNativeMenuEnvironment.Value);
        if (_scaleValueText != null) _scaleValueText.text = $"{config.NativeMenuScale.Value:0.00}x";
        if (_distanceValueText != null) _distanceValueText.text = $"{config.NativeMenuDistance.Value:0.0} m";
        if (_heightValueText != null) _heightValueText.text = $"{config.NativeMenuHeightOffset.Value:+0.00;-0.00;0.00} m";
    }

    private void RefreshNativeUiToggle(bool enabled)
    {
        if (_nativeUiValueText != null)
        {
            _nativeUiValueText.text = enabled ? "ON" : "OFF";
        }

        if (_nativeUiToggleButton != null)
        {
            NativeButtonFeedback.SetNormalColor(_nativeUiToggleButton, enabled ? ToggleOnColor : ToggleOffColor);
        }
    }

    private void RefreshEnvironmentToggle(bool enabled)
    {
        if (_environmentValueText != null)
        {
            _environmentValueText.text = enabled ? "ON" : "OFF";
        }

        if (_environmentToggleButton != null)
        {
            NativeButtonFeedback.SetNormalColor(_environmentToggleButton, enabled ? ToggleOnColor : ToggleOffColor);
        }
    }

    private void SetStatus(string status)
    {
        if (_statusText != null)
        {
            _statusText.text = status;
        }
    }

    private static float RoundToStep(float value, float step)
    {
        return Mathf.Round(value / step) * step;
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

    private Text CreateText(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color)
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
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private Button CreateMenuButton(string label, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 15)
    {
        var rectTransform = CreateImage(label, parent, color, anchoredPosition, size);
        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = rectTransform.GetComponent<Image>();
        button.onClick.AddListener(onClick);
        NativeButtonFeedback.Configure(button, color);
        CreateText($"{label} Text", rectTransform, label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white);
        return button;
    }
}
