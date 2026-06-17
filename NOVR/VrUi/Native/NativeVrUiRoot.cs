using NOVR.VrUi.SpecialBehavior;
using System.Collections.Generic;
using NuclearOption.Networking;
using NuclearOption.Networking.Lobbies;
using NuclearOption.Workshop;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeVrUiRoot : NOVRBehaviour
{
    private const float BaseCanvasScale = 0.0015625f;
    private const float DefaultMenuScale = 1.25f;
    private const float DefaultMenuDistanceMeters = 3f;
    private const float DefaultMenuHeightOffsetMeters = 0f;
    private static readonly Vector2 NativeCanvasSize = new(2000f, 1125f);
    private const float MainMenuScanIntervalSeconds = 0.5f;
    private const float RequestedMenuTransitionSeconds = 0.5f;
    private const float MissionLaunchEnvironmentSuppressionSeconds = 8f;
    private const float RecenterDelaySeconds = 2.0f;
    private const float AnchorResetAfterHiddenSeconds = 1.5f;
    private const float MinimumMenuCenterHeightBelowHeadMeters = -0.25f;
    private const float RecenterWidgetDistanceMeters = 1.35f;
    private const float RecenterWidgetVerticalOffsetMeters = -0.42f;
    private const float RecenterWidgetCanvasScale = 0.0018f;
    private const float NativeMenuEnvironmentHeightOffsetMeters = 0.0f;
    private const float NativeMenuEnvironmentMinimumMenuCenterHeightBelowHeadMeters = -5.25f;
    private static readonly Vector2 RecenterWidgetCanvasSize = new(320f, 96f);

    private readonly NativeGameActionAdapter _actions = new();
    private readonly VrPointerState _pointerState = new();
    private readonly List<SuppressedCanvasState> _suppressedCanvasStates = new();
    private GameObject? _root;
    private Canvas? _canvas;
    private NativeMainMenuShell? _mainMenuShell;
    private NativeMultiplayerPanel? _multiplayerPanel;
    private NativeSinglePlayerMissionPanel? _singlePlayerMissionPanel;
    private NativeSettingsPanel? _settingsPanel;
    private NativeWorkshopPanel? _workshopPanel;
    private NativeVrUiSettingsPanel? _vrUiSettingsPanel;
    private NativeMenuEnvironment? _menuEnvironment;
    private GameObject? _recenterWidgetRoot;
    private Canvas? _recenterWidgetCanvas;
    private Button? _recenterWidgetButton;
    private Text? _recenterButtonText;
    private GameObject? _mainCanvas;
    private CanvasGroup? _suppressedMainCanvasGroup;
    private bool _mainCanvasHadCanvasGroup;
    private float _mainCanvasOriginalAlpha;
    private bool _mainCanvasOriginalInteractable;
    private bool _mainCanvasOriginalBlocksRaycasts;
    private bool _singlePlayerMissionPickerRequested;
    private float _singlePlayerMissionPickerRequestTime;
    private bool _multiplayerRequested;
    private float _multiplayerRequestTime;
    private bool _settingsRequested;
    private float _settingsRequestTime;
    private bool _workshopRequested;
    private float _workshopRequestTime;
    private bool _vrUiSettingsOpen;
    private bool _missionLaunchPending;
    private float _missionLaunchRequestTime;
    private float _nextMainMenuScanTime;
    private float _pendingRecenterTime;
    private bool _recenterPending;
    private float _lastNativeUiVisibleTime = -100f;
    private Vector3 _menuAnchorPosition;
    private Quaternion _menuAnchorRotation = Quaternion.identity;
    private UtilityWidgetMode _utilityWidgetMode = UtilityWidgetMode.Hidden;

    public VrPointerState PointerState => _pointerState;
    public GameObject? OriginalMainCanvas => _mainCanvas;
    public NativeGameActionAdapter Actions => _actions;

    private void Start()
    {
        RefreshEnabledState();
    }

    private void Update()
    {
        _pointerState.Update(VrUiCursor.I);
        EnsureRoot();
        ScanForMainMenuCanvas();

        if (GameManager.gameState != GameState.Menu)
        {
            RestoreOriginalMainCanvas();
            if (_root != null)
            {
                _root.SetActive(false);
            }
            _menuEnvironment?.Hide();
            ClearNativeCursorProjectionReference();
            SetUtilityWidgetMode(UtilityWidgetMode.Hidden);
            return;
        }

        if (_missionLaunchPending &&
            Time.unscaledTime - _missionLaunchRequestTime > MissionLaunchEnvironmentSuppressionSeconds)
        {
            _missionLaunchPending = false;
        }

        if (!IsNativeMenuUiEnabled)
        {
            RestoreOriginalMainCanvas();
            if (_root != null)
            {
                _root.SetActive(false);
            }
            _menuEnvironment?.Hide();
            ClearNativeCursorProjectionReference();
            SetUtilityWidgetMode(ShouldShowStockNativeUiToggle()
                ? UtilityWidgetMode.EnableNativeUi
                : UtilityWidgetMode.Hidden);
            return;
        }

        var mainCanvasActive = _mainCanvas != null && _mainCanvas.activeInHierarchy;
        var controlMapperOpen = IsControlMapperOpen();
        var topLevelMainMenuAvailable = _actions.IsTopLevelMainMenuAvailable;
        var waitingForSinglePlayerMissionPicker = _singlePlayerMissionPickerRequested &&
                                                  Time.unscaledTime - _singlePlayerMissionPickerRequestTime < RequestedMenuTransitionSeconds;
        var waitingForMultiplayer = _multiplayerRequested &&
                                    Time.unscaledTime - _multiplayerRequestTime < RequestedMenuTransitionSeconds;
        var shouldShowSinglePlayerMissionPicker = _singlePlayerMissionPickerRequested &&
                                                  !controlMapperOpen &&
                                                  mainCanvasActive &&
                                                  (waitingForSinglePlayerMissionPicker ||
                                                   !topLevelMainMenuAvailable ||
                                                   IsMissionPickerAvailable());
        var shouldShowMultiplayer = _multiplayerRequested &&
                                    !controlMapperOpen &&
                                    mainCanvasActive &&
                                    (waitingForMultiplayer ||
                                     !topLevelMainMenuAvailable ||
                                     IsMultiplayerMenuAvailable());
        var shouldShowSettings = _settingsRequested &&
                                 !controlMapperOpen &&
                                 mainCanvasActive &&
                                 IsSettingsMenuAvailable();
        var waitingForSettingsMenu = _settingsRequested &&
                                     !shouldShowSettings &&
                                     Time.unscaledTime - _settingsRequestTime < RequestedMenuTransitionSeconds;
        var waitingForWorkshop = _workshopRequested &&
                                 Time.unscaledTime - _workshopRequestTime < RequestedMenuTransitionSeconds;
        var shouldShowWorkshop = _workshopRequested &&
                                 !controlMapperOpen &&
                                 mainCanvasActive &&
                                 (waitingForWorkshop ||
                                  !topLevelMainMenuAvailable ||
                                  IsWorkshopMenuAvailable());
        var shouldShowVrUiSettings = _vrUiSettingsOpen &&
                                     !controlMapperOpen &&
                                     mainCanvasActive &&
                                     topLevelMainMenuAvailable;
        var shouldShowMainMenu = mainCanvasActive &&
                                 !controlMapperOpen &&
                                 topLevelMainMenuAvailable &&
                                 !shouldShowSinglePlayerMissionPicker &&
                                 !shouldShowMultiplayer &&
                                 !shouldShowSettings &&
                                 !waitingForSettingsMenu &&
                                 !shouldShowWorkshop &&
                                 !shouldShowVrUiSettings;

        if (shouldShowMainMenu)
        {
            _singlePlayerMissionPickerRequested = false;
            _multiplayerRequested = false;
            _settingsRequested = false;
            _workshopRequested = false;
            _vrUiSettingsOpen = false;
        }

        _mainMenuShell?.SetVisible(shouldShowMainMenu);
        _multiplayerPanel?.SetVisible(shouldShowMultiplayer);
        _singlePlayerMissionPanel?.SetVisible(shouldShowSinglePlayerMissionPicker);
        _settingsPanel?.SetVisible(shouldShowSettings);
        _workshopPanel?.SetVisible(shouldShowWorkshop);
        _vrUiSettingsPanel?.SetVisible(shouldShowVrUiSettings);

        var shouldShowNativeUi = shouldShowMainMenu || shouldShowSinglePlayerMissionPicker || shouldShowMultiplayer || shouldShowSettings || shouldShowWorkshop || shouldShowVrUiSettings;
        var shouldKeepEnvironmentForMenuTransition = !controlMapperOpen &&
                                                     (waitingForSinglePlayerMissionPicker ||
                                                      waitingForMultiplayer ||
                                                      waitingForSettingsMenu ||
                                                      waitingForWorkshop ||
                                                      _singlePlayerMissionPickerRequested ||
                                                      _multiplayerRequested ||
                                                      _settingsRequested ||
                                                      _workshopRequested ||
                                                      _vrUiSettingsOpen);
        var shouldShowNativeEnvironment = !_missionLaunchPending &&
                                          (shouldShowNativeUi || shouldKeepEnvironmentForMenuTransition);
        HandleRecenterShortcut(shouldShowNativeUi);
        UpdatePlacement(shouldShowNativeUi);
        UpdatePendingRecenter(shouldShowNativeUi);
        if (_root != null && _root.activeSelf != shouldShowNativeUi)
        {
            _root.SetActive(shouldShowNativeUi);
        }

        if (_root != null)
        {
            _menuEnvironment?.UpdateEnvironment(_root.transform, shouldShowNativeEnvironment);
        }

        SuppressOriginalMainCanvas(shouldShowNativeUi);
    }

    protected override void OnSettingChanged()
    {
        base.OnSettingChanged();
        RefreshEnabledState();
    }

    private void RefreshEnabledState()
    {
        if (IsNativeMenuUiEnabled)
        {
            EnsureRoot();
            return;
        }

        RestoreOriginalMainCanvas();
        if (_root != null)
        {
            _root.SetActive(false);
        }
        _menuEnvironment?.Hide();
        ClearNativeCursorProjectionReference();
        SetUtilityWidgetMode(UtilityWidgetMode.Hidden);
    }

    private void EnsureRoot()
    {
        if (_root != null) return;

        _root = new GameObject("NOVR Native VR UI");
        DontDestroyOnLoad(_root);

        var rectTransform = _root.AddComponent<RectTransform>();
        rectTransform.sizeDelta = NativeCanvasSize;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 5000;

        _root.AddComponent<GraphicRaycaster>();
        LayerHelper.SetLayerRecursive(_root.transform, LayerHelper.GetVrUiLayer());

        _mainMenuShell = _root.AddComponent<NativeMainMenuShell>();
        _mainMenuShell.Initialize(_actions, rectTransform, OpenVrUiSettingsPanel);
        _mainMenuShell.SetOriginalMainCanvas(_mainCanvas);
        _multiplayerPanel = _root.AddComponent<NativeMultiplayerPanel>();
        _multiplayerPanel.Initialize(_actions, rectTransform, OnMissionLaunchRequested);
        _multiplayerPanel.SetVisible(false);
        _singlePlayerMissionPanel = _root.AddComponent<NativeSinglePlayerMissionPanel>();
        _singlePlayerMissionPanel.Initialize(_actions, rectTransform, OnMissionLaunchRequested);
        _singlePlayerMissionPanel.SetVisible(false);
        _settingsPanel = _root.AddComponent<NativeSettingsPanel>();
        _settingsPanel.Initialize(_actions, rectTransform);
        _settingsPanel.SetVisible(false);
        _workshopPanel = _root.AddComponent<NativeWorkshopPanel>();
        _workshopPanel.Initialize(_actions, rectTransform);
        _workshopPanel.SetVisible(false);
        _vrUiSettingsPanel = _root.AddComponent<NativeVrUiSettingsPanel>();
        _vrUiSettingsPanel.Initialize(rectTransform, CloseVrUiSettingsPanel, RecenterMenu);
        _vrUiSettingsPanel.SetVisible(false);
        _menuEnvironment = gameObject.AddComponent<NativeMenuEnvironment>();
        CreateRecenterWidget();
        _actions.ActionInvoked += OnNativeActionInvoked;
        _root.SetActive(false);

        Debug.Log("[NOVR] Native VR UI root created.");
    }

    private void UpdatePlacement(bool shouldShowNativeUi)
    {
        if (_root == null) return;

        var menuDistance = GetNativeMenuDistance();
        var menuHeightOffset = GetNativeMenuHeightOffset();
        var environmentEnabled = ModConfiguration.Instance?.EnableNativeMenuEnvironment.Value == true;
        if (environmentEnabled)
        {
            menuHeightOffset += NativeMenuEnvironmentHeightOffsetMeters;
        }

        var menuScale = BaseCanvasScale * GetNativeMenuScale();
        _root.transform.localScale = Vector3.one * menuScale;

        if (_canvas != null)
        {
            _canvas.worldCamera = APIBus.CockpitHudCamera;
            _canvas.planeDistance = menuDistance;
        }

        if (!shouldShowNativeUi)
        {
            ClearNativeCursorProjectionReference();
            SetUtilityWidgetMode(UtilityWidgetMode.Hidden);
            return;
        }

        _lastNativeUiVisibleTime = Time.unscaledTime;
        SetUtilityWidgetMode(UtilityWidgetMode.Recenter);
        

        _root.transform.SetParent(null, true);
        ApplyMenuAnchor(
            menuDistance,
            menuHeightOffset,
            environmentEnabled
                ? NativeMenuEnvironmentMinimumMenuCenterHeightBelowHeadMeters
                : MinimumMenuCenterHeightBelowHeadMeters);
        ApplyNativeCursorProjectionReference();
    }

    private void ApplyMenuAnchor(float menuDistance, float menuHeightOffset, float minimumMenuCenterHeightBelowHeadMeters)
    {
        if (_root == null) return;

        var position = _menuAnchorPosition + (_menuAnchorRotation * Vector3.forward) * menuDistance + Vector3.up * menuHeightOffset;
        var minimumHeight = _menuAnchorPosition.y + minimumMenuCenterHeightBelowHeadMeters;
        if (position.y < minimumHeight)
        {
            position.y = minimumHeight;
        }

        _root.transform.position = position;
        _root.transform.rotation = _menuAnchorRotation;
    }

    private void ApplyNativeCursorProjectionReference()
    {
        VrUiCursor.I?.SetProjectionReferenceRotation(_menuAnchorRotation);
    }

    private static void ClearNativeCursorProjectionReference()
    {
        VrUiCursor.I?.ClearProjectionReferenceRotation();
    }

    private void HandleRecenterShortcut(bool shouldShowNativeUi)
    {
        if (!shouldShowNativeUi) return;

        var keyboard = Keyboard.current;
        if (keyboard?.homeKey.wasPressedThisFrame == true)
        {
            RecenterMenu();
        }
    }

    private void RecenterMenu()
    {
        _recenterPending = true;
        _pendingRecenterTime = Time.unscaledTime + RecenterDelaySeconds;
        UpdateRecenterButtonText();
        Debug.Log($"[NOVR] Native VR UI recenter queued for {RecenterDelaySeconds:0.0} seconds from now.");
    }

    private void RecenterMenuImmediately()
    {
        // CaptureMenuAnchor();
        // UpdatePlacement(true);
        NOVRHeadsetData.CalibrateTranslation();
        NOVRHeadsetData.CalibrateRotation();
        _recenterPending = false;
        UpdateRecenterButtonText();
        Debug.Log("[NOVR] Native VR UI recentered.");
    }

    private void UpdatePendingRecenter(bool shouldShowNativeUi)
    {
        if (!_recenterPending)
        {
            UpdateRecenterButtonText();
            return;
        }

        if (!shouldShowNativeUi)
        {
            _recenterPending = false;
            UpdateRecenterButtonText();
            return;
        }

        if (Time.unscaledTime >= _pendingRecenterTime)
        {
            RecenterMenuImmediately();
            return;
        }

        UpdateRecenterButtonText();
    }

    private void UpdateRecenterButtonText()
    {
        if (_recenterButtonText == null) return;

        if (_utilityWidgetMode == UtilityWidgetMode.EnableNativeUi)
        {
            _recenterButtonText.text = "VR UI ON";
            return;
        }

        if (!_recenterPending)
        {
            _recenterButtonText.text = "VR CENTER";
            return;
        }

        var remaining = Mathf.Max(0f, _pendingRecenterTime - Time.unscaledTime);
        _recenterButtonText.text = $"CENTER {remaining:0.0}";
    }

    private void CreateRecenterWidget()
    {
        if (_recenterWidgetRoot != null) return;

        _recenterWidgetRoot = new GameObject("NOVR Native VR UI Recenter Widget");
        DontDestroyOnLoad(_recenterWidgetRoot);

        var rectTransform = _recenterWidgetRoot.AddComponent<RectTransform>();
        rectTransform.sizeDelta = RecenterWidgetCanvasSize;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        _recenterWidgetCanvas = _recenterWidgetRoot.AddComponent<Canvas>();
        _recenterWidgetCanvas.renderMode = RenderMode.WorldSpace;
        _recenterWidgetCanvas.overrideSorting = true;
        _recenterWidgetCanvas.sortingOrder = 6000;
        _recenterWidgetCanvas.planeDistance = RecenterWidgetDistanceMeters;

        _recenterWidgetRoot.AddComponent<GraphicRaycaster>();
        LayerHelper.SetLayerRecursive(_recenterWidgetRoot.transform, LayerHelper.GetVrUiLayer());

        var button = CreateRecenterButton(
            "VR CENTER",
            rectTransform,
            Vector2.zero,
            new Vector2(260f, 54f),
            new Color(0.18f, 0.23f, 0.26f, 0.96f),
            OnUtilityWidgetClicked,
            14);

        _recenterWidgetButton = button;
        _recenterButtonText = button.GetComponentInChildren<Text>();
        SetUtilityWidgetMode(UtilityWidgetMode.Hidden);
    }

    private void UpdateRecenterWidgetPlacement()
    {
        if (_recenterWidgetRoot == null) return;

        var reference = APIBus.CockpitHudReference.transform;
        _recenterWidgetRoot.transform.SetParent(reference, false);
        _recenterWidgetRoot.transform.localPosition = new Vector3(0f, RecenterWidgetVerticalOffsetMeters, RecenterWidgetDistanceMeters);
        _recenterWidgetRoot.transform.localRotation = Quaternion.identity;
        _recenterWidgetRoot.transform.localScale = Vector3.one * RecenterWidgetCanvasScale;

        if (_recenterWidgetCanvas != null)
        {
            _recenterWidgetCanvas.worldCamera = APIBus.CockpitHudCamera;
        }
    }

    private void SetUtilityWidgetMode(UtilityWidgetMode mode)
    {
        var modeChanged = _utilityWidgetMode != mode;
        _utilityWidgetMode = mode;

        if (_recenterWidgetRoot == null) return;

        var visible = mode != UtilityWidgetMode.Hidden;
        if (_recenterWidgetRoot.activeSelf != visible)
        {
            _recenterWidgetRoot.SetActive(visible);
        }

        if (!visible) return;

        UpdateRecenterWidgetPlacement();
        UpdateRecenterButtonText();

        if (modeChanged && _recenterWidgetButton != null)
        {
            var color = mode == UtilityWidgetMode.EnableNativeUi
                ? new Color(0.12f, 0.34f, 0.20f, 0.96f)
                : new Color(0.18f, 0.23f, 0.26f, 0.96f);
            NativeButtonFeedback.SetNormalColor(_recenterWidgetButton, color);
        }
    }

    private void OnUtilityWidgetClicked()
    {
        if (_utilityWidgetMode == UtilityWidgetMode.EnableNativeUi)
        {
            EnableNativeMenuUiFromStock();
            return;
        }

        RecenterMenu();
    }

    private void EnableNativeMenuUiFromStock()
    {
        var config = ModConfiguration.Instance;
        config.EnableNativeMenuUi.Value = true;
        config.Config.Save();
        SetNativeMenuRequestFromCurrentStockMenu();
        SetUtilityWidgetMode(UtilityWidgetMode.Hidden);
        Debug.Log("[NOVR] Native VR UI enabled from stock menu fallback button.");
    }

    private void SetNativeMenuRequestFromCurrentStockMenu()
    {
        _singlePlayerMissionPickerRequested = false;
        _multiplayerRequested = false;
        _settingsRequested = false;
        _workshopRequested = false;
        _vrUiSettingsOpen = false;

        if (IsMissionPickerAvailable())
        {
            _singlePlayerMissionPickerRequested = true;
            _singlePlayerMissionPickerRequestTime = Time.unscaledTime;
        }
        else if (IsMultiplayerMenuAvailable())
        {
            _multiplayerRequested = true;
            _multiplayerRequestTime = Time.unscaledTime;
        }
        else if (IsSettingsMenuAvailable())
        {
            _settingsRequested = true;
            _settingsRequestTime = Time.unscaledTime;
        }
        else if (IsWorkshopMenuAvailable())
        {
            _workshopRequested = true;
            _workshopRequestTime = Time.unscaledTime;
        }
    }

    private bool ShouldShowStockNativeUiToggle()
    {
        if (_mainCanvas == null || !_mainCanvas.activeInHierarchy || IsControlMapperOpen())
        {
            return false;
        }

        return _actions.IsTopLevelMainMenuAvailable ||
               IsMissionPickerAvailable() ||
               IsMultiplayerMenuAvailable() ||
               IsSettingsMenuAvailable() ||
               IsWorkshopMenuAvailable();
    }

    private static Button CreateRecenterButton(
        string label,
        RectTransform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        UnityEngine.Events.UnityAction onClick,
        int fontSize)
    {
        var rectTransform = CreateImage(label, parent, color, anchoredPosition, size);
        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = rectTransform.GetComponent<Image>();
        button.onClick.AddListener(onClick);
        NativeButtonFeedback.Configure(button, color);
        CreateText($"{label} Text", rectTransform, label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white);
        return button;
    }

    private static RectTransform CreateImage(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
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

    private static Text CreateText(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var textComponent = gameObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private void ScanForMainMenuCanvas()
    {
        if (Time.unscaledTime < _nextMainMenuScanTime) return;

        _nextMainMenuScanTime = Time.unscaledTime + MainMenuScanIntervalSeconds;
        var mainCanvas = FindMainCanvas();
        if (mainCanvas == _mainCanvas) return;

        RestoreOriginalMainCanvas();
        _mainCanvas = mainCanvas;
        _actions.SetOriginalMainCanvas(_mainCanvas);
        _mainMenuShell?.SetOriginalMainCanvas(_mainCanvas);
    }

    private static GameObject? FindMainCanvas()
    {
        var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (var index = 0; index < gameObjects.Length; index++)
        {
            var gameObject = gameObjects[index];
            if (gameObject.name == "MainCanvas" && gameObject.GetComponent<Canvas>() != null)
            {
                return gameObject;
            }
        }

        return null;
    }

    private bool IsMissionPickerAvailable()
    {
        if (_mainCanvas == null) return false;

        var pickers = _mainCanvas.GetComponentsInChildren<global::MissionsPicker>(true);
        for (var index = 0; index < pickers.Length; index++)
        {
            if (pickers[index].gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMultiplayerMenuAvailable()
    {
        if (_mainCanvas == null) return false;

        var lobbyLists = _mainCanvas.GetComponentsInChildren<LobbyList>(true);
        for (var index = 0; index < lobbyLists.Length; index++)
        {
            if (lobbyLists[index].gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSettingsMenuAvailable()
    {
        if (_mainCanvas == null) return false;

        var settingsMenus = _mainCanvas.GetComponentsInChildren<global::SettingsMenu>(true);
        for (var index = 0; index < settingsMenus.Length; index++)
        {
            if (settingsMenus[index].gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWorkshopMenuAvailable()
    {
        if (_mainCanvas == null) return false;

        var workshopMenus = _mainCanvas.GetComponentsInChildren<WorkshopMenu>(true);
        for (var index = 0; index < workshopMenus.Length; index++)
        {
            if (workshopMenus[index].gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsControlMapperOpen()
    {
        return GameManager.controlMapper != null && GameManager.controlMapper.isOpen;
    }

    private void OnNativeActionInvoked(NativeGameAction action)
    {
        _vrUiSettingsOpen = false;
        _singlePlayerMissionPickerRequested = action == NativeGameAction.SinglePlayer;
        if (_singlePlayerMissionPickerRequested)
        {
            _singlePlayerMissionPickerRequestTime = Time.unscaledTime;
            ShowSinglePlayerMissionPanelImmediately();
        }

        _multiplayerRequested = action == NativeGameAction.Multiplayer;
        if (_multiplayerRequested)
        {
            _multiplayerRequestTime = Time.unscaledTime;
            ShowMultiplayerPanelImmediately();
        }

        _settingsRequested = action == NativeGameAction.Settings;
        if (_settingsRequested)
        {
            _settingsRequestTime = Time.unscaledTime;
            SuppressOriginalMainCanvas(true);
        }

        _workshopRequested = action == NativeGameAction.Workshop;
        if (_workshopRequested)
        {
            _workshopRequestTime = Time.unscaledTime;
            ShowWorkshopPanelImmediately();
        }
    }

    private void OpenVrUiSettingsPanel()
    {
        _singlePlayerMissionPickerRequested = false;
        _multiplayerRequested = false;
        _settingsRequested = false;
        _workshopRequested = false;
        _vrUiSettingsOpen = true;

        if (_root != null && !_root.activeSelf)
        {
            _root.SetActive(true);
        }

        _mainMenuShell?.SetVisible(false);
        _multiplayerPanel?.SetVisible(false);
        _singlePlayerMissionPanel?.SetVisible(false);
        _settingsPanel?.SetVisible(false);
        _workshopPanel?.SetVisible(false);
        _vrUiSettingsPanel?.SetVisible(true);
        SuppressOriginalMainCanvas(true);
    }

    private void CloseVrUiSettingsPanel()
    {
        _vrUiSettingsOpen = false;
        _vrUiSettingsPanel?.SetVisible(false);
    }

    private void OnMissionLaunchRequested()
    {
        _missionLaunchPending = true;
        _missionLaunchRequestTime = Time.unscaledTime;
        _menuEnvironment?.Hide();
    }

    private void ShowSinglePlayerMissionPanelImmediately()
    {
        if (_root != null && !_root.activeSelf)
        {
            _root.SetActive(true);
        }

        _mainMenuShell?.SetVisible(false);
        _multiplayerPanel?.SetVisible(false);
        _singlePlayerMissionPanel?.SetVisible(true);
        _settingsPanel?.SetVisible(false);
        _workshopPanel?.SetVisible(false);
        _vrUiSettingsPanel?.SetVisible(false);
        SuppressOriginalMainCanvas(true);
    }

    private void ShowMultiplayerPanelImmediately()
    {
        if (_root != null && !_root.activeSelf)
        {
            _root.SetActive(true);
        }

        _mainMenuShell?.SetVisible(false);
        _multiplayerPanel?.SetVisible(true);
        _singlePlayerMissionPanel?.SetVisible(false);
        _settingsPanel?.SetVisible(false);
        _workshopPanel?.SetVisible(false);
        _vrUiSettingsPanel?.SetVisible(false);
        SuppressOriginalMainCanvas(true);
    }

    private void ShowWorkshopPanelImmediately()
    {
        if (_root != null && !_root.activeSelf)
        {
            _root.SetActive(true);
        }

        _mainMenuShell?.SetVisible(false);
        _multiplayerPanel?.SetVisible(false);
        _singlePlayerMissionPanel?.SetVisible(false);
        _settingsPanel?.SetVisible(false);
        _workshopPanel?.SetVisible(true);
        _vrUiSettingsPanel?.SetVisible(false);
        SuppressOriginalMainCanvas(true);
    }

    private void SuppressOriginalMainCanvas(bool suppress)
    {
        if (!suppress)
        {
            RestoreOriginalMainCanvas();
            return;
        }

        if (_mainCanvas == null) return;

        SuppressOriginalCanvases();

        if (_suppressedMainCanvasGroup != null) return;

        _mainCanvasHadCanvasGroup = _mainCanvas.TryGetComponent(out _suppressedMainCanvasGroup);
        if (_suppressedMainCanvasGroup == null)
        {
            _suppressedMainCanvasGroup = _mainCanvas.AddComponent<CanvasGroup>();
        }

        _mainCanvasOriginalAlpha = _suppressedMainCanvasGroup.alpha;
        _mainCanvasOriginalInteractable = _suppressedMainCanvasGroup.interactable;
        _mainCanvasOriginalBlocksRaycasts = _suppressedMainCanvasGroup.blocksRaycasts;

        _suppressedMainCanvasGroup.alpha = 0f;
        _suppressedMainCanvasGroup.interactable = false;
        _suppressedMainCanvasGroup.blocksRaycasts = false;
    }

    private void SuppressOriginalCanvases()
    {
        if (_mainCanvas == null) return;

        var canvases = _mainCanvas.GetComponentsInChildren<Canvas>(true);
        for (var index = 0; index < canvases.Length; index++)
        {
            var canvas = canvases[index];
            if (canvas == null) continue;

            if (!HasSuppressedCanvasState(canvas))
            {
                _suppressedCanvasStates.Add(new SuppressedCanvasState(canvas, canvas.enabled));
            }

            canvas.enabled = false;
        }
    }

    private bool HasSuppressedCanvasState(Canvas canvas)
    {
        for (var index = 0; index < _suppressedCanvasStates.Count; index++)
        {
            if (_suppressedCanvasStates[index].Canvas == canvas)
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreOriginalMainCanvas()
    {
        RestoreOriginalCanvases();

        if (_suppressedMainCanvasGroup == null) return;

        _suppressedMainCanvasGroup.alpha = _mainCanvasOriginalAlpha;
        _suppressedMainCanvasGroup.interactable = _mainCanvasOriginalInteractable;
        _suppressedMainCanvasGroup.blocksRaycasts = _mainCanvasOriginalBlocksRaycasts;

        if (!_mainCanvasHadCanvasGroup)
        {
            Destroy(_suppressedMainCanvasGroup);
        }

        _suppressedMainCanvasGroup = null;
    }

    private void RestoreOriginalCanvases()
    {
        for (var index = 0; index < _suppressedCanvasStates.Count; index++)
        {
            var state = _suppressedCanvasStates[index];
            if (state.Canvas != null)
            {
                state.Canvas.enabled = state.WasEnabled;
            }
        }

        _suppressedCanvasStates.Clear();
    }

    private static float GetNativeMenuScale()
    {
        return Mathf.Clamp(ModConfiguration.Instance?.NativeMenuScale.Value ?? DefaultMenuScale, 0.75f, 2.0f);
    }

    private static float GetNativeMenuDistance()
    {
        return Mathf.Clamp(ModConfiguration.Instance?.NativeMenuDistance.Value ?? DefaultMenuDistanceMeters, 1.5f, 6.0f);
    }

    private static float GetNativeMenuHeightOffset()
    {
        return Mathf.Clamp(ModConfiguration.Instance?.NativeMenuHeightOffset.Value ?? DefaultMenuHeightOffsetMeters, MinimumMenuCenterHeightBelowHeadMeters, 1.0f);
    }

    private static bool IsNativeMenuUiEnabled =>
        ModConfiguration.Instance?.EnableNativeMenuUi.Value == true;

    private enum UtilityWidgetMode
    {
        Hidden,
        Recenter,
        EnableNativeUi
    }

    private readonly struct SuppressedCanvasState
    {
        public SuppressedCanvasState(Canvas canvas, bool wasEnabled)
        {
            Canvas = canvas;
            WasEnabled = wasEnabled;
        }

        public Canvas Canvas { get; }
        public bool WasEnabled { get; }
    }
}
