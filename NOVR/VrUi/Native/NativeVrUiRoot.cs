using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeVrUiRoot : NOVRBehaviour
{
    private const float MenuDistanceMeters = 3f;
    private const float CanvasScale = 0.0015625f;
    private const float MainMenuScanIntervalSeconds = 0.5f;
    private const float RequestedMenuTransitionSeconds = 0.5f;

    private readonly NativeGameActionAdapter _actions = new();
    private readonly VrPointerState _pointerState = new();
    private GameObject? _root;
    private Canvas? _canvas;
    private NativeMainMenuShell? _mainMenuShell;
    private NativeSinglePlayerMissionPanel? _singlePlayerMissionPanel;
    private NativeSettingsPanel? _settingsPanel;
    private GameObject? _mainCanvas;
    private CanvasGroup? _suppressedMainCanvasGroup;
    private bool _mainCanvasHadCanvasGroup;
    private float _mainCanvasOriginalAlpha;
    private bool _mainCanvasOriginalInteractable;
    private bool _mainCanvasOriginalBlocksRaycasts;
    private bool _singlePlayerMissionPickerRequested;
    private bool _settingsRequested;
    private float _settingsRequestTime;
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
        var shouldShowSinglePlayerMissionPicker = _singlePlayerMissionPickerRequested &&
                                                  !controlMapperOpen &&
                                                  mainCanvasActive &&
                                                  IsMissionPickerAvailable();
        var shouldShowSettings = _settingsRequested &&
                                 !controlMapperOpen &&
                                 mainCanvasActive &&
                                 IsSettingsMenuAvailable();
        var waitingForSettingsMenu = _settingsRequested &&
                                     !shouldShowSettings &&
                                     Time.unscaledTime - _settingsRequestTime < RequestedMenuTransitionSeconds;
        var shouldShowMainMenu = mainCanvasActive &&
                                 !controlMapperOpen &&
                                 _actions.IsTopLevelMainMenuAvailable &&
                                 !shouldShowSinglePlayerMissionPicker &&
                                 !shouldShowSettings &&
                                 !waitingForSettingsMenu;

        if (shouldShowMainMenu)
        {
            _singlePlayerMissionPickerRequested = false;
            _settingsRequested = false;
        }

        _mainMenuShell?.SetVisible(shouldShowMainMenu);
        _singlePlayerMissionPanel?.SetVisible(shouldShowSinglePlayerMissionPicker);
        _settingsPanel?.SetVisible(shouldShowSettings);

        var shouldShowNativeUi = shouldShowMainMenu || shouldShowSinglePlayerMissionPicker || shouldShowSettings;
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
        rectTransform.sizeDelta = new Vector2(1600f, 900f);
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
        _singlePlayerMissionPanel = _root.AddComponent<NativeSinglePlayerMissionPanel>();
        _singlePlayerMissionPanel.Initialize(_actions, rectTransform);
        _singlePlayerMissionPanel.SetVisible(false);
        _settingsPanel = _root.AddComponent<NativeSettingsPanel>();
        _settingsPanel.Initialize(_actions, rectTransform);
        _settingsPanel.SetVisible(false);
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

    private static bool IsControlMapperOpen()
    {
        return GameManager.controlMapper != null && GameManager.controlMapper.isOpen;
    }

    private void OnNativeActionInvoked(NativeGameAction action)
    {
        _singlePlayerMissionPickerRequested = action == NativeGameAction.SinglePlayer;
        _settingsRequested = action == NativeGameAction.Settings;
        if (_settingsRequested)
        {
            _settingsRequestTime = Time.unscaledTime;
        }
    }

    private void SuppressOriginalMainCanvas(bool suppress)
    {
        if (!suppress)
        {
            RestoreOriginalMainCanvas();
            return;
        }

        if (_mainCanvas == null || _suppressedMainCanvasGroup != null) return;

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

    private void RestoreOriginalMainCanvas()
    {
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

    private static bool IsNativeMenuUiEnabled =>
        ModConfiguration.Instance?.EnableNativeMenuUi.Value == true;
}
