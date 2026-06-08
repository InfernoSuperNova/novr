using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeVrUiRoot : NOVRBehaviour
{
    private const float MenuDistanceMeters = 3f;
    private const float CanvasScale = 0.00125f;
    private const float MainMenuScanIntervalSeconds = 0.5f;

    private readonly NativeGameActionAdapter _actions = new();
    private readonly VrPointerState _pointerState = new();
    private GameObject? _root;
    private Canvas? _canvas;
    private NativeMainMenuShell? _mainMenuShell;
    private GameObject? _mainCanvas;
    private CanvasGroup? _suppressedMainCanvasGroup;
    private bool _mainCanvasHadCanvasGroup;
    private float _mainCanvasOriginalAlpha;
    private bool _mainCanvasOriginalInteractable;
    private bool _mainCanvasOriginalBlocksRaycasts;
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

        var shouldShowMainMenu = _mainCanvas != null && _mainCanvas.activeInHierarchy;
        if (_root != null && _root.activeSelf != shouldShowMainMenu)
        {
            _root.SetActive(shouldShowMainMenu);
        }

        SuppressOriginalMainCanvas(shouldShowMainMenu);
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
