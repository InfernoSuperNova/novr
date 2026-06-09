using NOVR.VrUi.SpecialBehavior;
using System.Collections.Generic;
using NuclearOption.Networking.Lobbies;
using NuclearOption.Workshop;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeVrUiRoot : NOVRBehaviour
{
    private const float MenuDistanceMeters = 3f;
    private const float CanvasScale = 0.0015625f;
    private static readonly Vector2 NativeCanvasSize = new(2000f, 1125f);
    private const float MainMenuScanIntervalSeconds = 0.5f;
    private const float RequestedMenuTransitionSeconds = 0.5f;

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
    private float _nextMainMenuScanTime;

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
        RefreshEnabledState();

        if (!IsNativeMenuUiEnabled)
        {
            return;
        }

        EnsureRoot();
        UpdatePlacement();
        ScanForMainMenuCanvas();

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
        var shouldShowMainMenu = mainCanvasActive &&
                                 !controlMapperOpen &&
                                 topLevelMainMenuAvailable &&
                                 !shouldShowSinglePlayerMissionPicker &&
                                 !shouldShowMultiplayer &&
                                 !shouldShowSettings &&
                                 !waitingForSettingsMenu &&
                                 !shouldShowWorkshop;

        if (shouldShowMainMenu)
        {
            _singlePlayerMissionPickerRequested = false;
            _multiplayerRequested = false;
            _settingsRequested = false;
            _workshopRequested = false;
        }

        _mainMenuShell?.SetVisible(shouldShowMainMenu);
        _multiplayerPanel?.SetVisible(shouldShowMultiplayer);
        _singlePlayerMissionPanel?.SetVisible(shouldShowSinglePlayerMissionPicker);
        _settingsPanel?.SetVisible(shouldShowSettings);
        _workshopPanel?.SetVisible(shouldShowWorkshop);

        var shouldShowNativeUi = shouldShowMainMenu || shouldShowSinglePlayerMissionPicker || shouldShowMultiplayer || shouldShowSettings || shouldShowWorkshop;
        if (_root != null && _root.activeSelf != shouldShowNativeUi)
        {
            _root.SetActive(shouldShowNativeUi);
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
        _mainMenuShell.Initialize(_actions, rectTransform);
        _mainMenuShell.SetOriginalMainCanvas(_mainCanvas);
        _multiplayerPanel = _root.AddComponent<NativeMultiplayerPanel>();
        _multiplayerPanel.Initialize(_actions, rectTransform);
        _multiplayerPanel.SetVisible(false);
        _singlePlayerMissionPanel = _root.AddComponent<NativeSinglePlayerMissionPanel>();
        _singlePlayerMissionPanel.Initialize(_actions, rectTransform);
        _singlePlayerMissionPanel.SetVisible(false);
        _settingsPanel = _root.AddComponent<NativeSettingsPanel>();
        _settingsPanel.Initialize(_actions, rectTransform);
        _settingsPanel.SetVisible(false);
        _workshopPanel = _root.AddComponent<NativeWorkshopPanel>();
        _workshopPanel.Initialize(_actions, rectTransform);
        _workshopPanel.SetVisible(false);
        _actions.ActionInvoked += OnNativeActionInvoked;
        _root.SetActive(false);

        Debug.Log("[NOVR] Native VR UI root created.");
    }

    private void UpdatePlacement()
    {
        if (_root == null) return;

        var reference = APIBus.CockpitHudReference.transform;
        _root.transform.SetParent(reference, false);
        _root.transform.localPosition = new Vector3(0f, 0f, MenuDistanceMeters);
        _root.transform.localRotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one * CanvasScale;

        if (_canvas != null)
        {
            _canvas.worldCamera = APIBus.CockpitHudCamera;
            _canvas.planeDistance = MenuDistanceMeters;
        }
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

    private static bool IsNativeMenuUiEnabled =>
        ModConfiguration.Instance?.EnableNativeMenuUi.Value == true;

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
