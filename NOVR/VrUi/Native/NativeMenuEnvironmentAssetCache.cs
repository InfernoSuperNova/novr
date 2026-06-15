using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NOVR.VrUi.Native;

public sealed class NativeMenuEnvironmentAssetCache : MonoBehaviour
{
    private const string EncyclopediaSceneName = "Encyclopedia";
    private const string EncyclopediaScenePath = "Assets/Scenes/Encyclopedia/Encyclopedia.unity";
    private const string MainMenuSceneName = "MainMenu";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu/MainMenu.unity";
    private const float ScenePollIntervalSeconds = 0.5f;
    private const float MenuWarmupDelayTimeoutSeconds = 8f;

    private static readonly BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private Material? _waterMaterialTemplate;
    private Texture2D? _menuOceanBasecolor;
    private Texture2D? _oceanBasecolor;
    private Texture2D? _oceanDepthmap;
    private Texture2D? _terrainColorMap;
    private Vector2 _mapSize;

    private GameObject? _cloudPlaneTemplate;
    private WeatherSet[] _weatherSets = Array.Empty<WeatherSet>();
    private Material? _cloudLayerMaterialTemplate;
    private Material? _cloudMaterialTemplate;
    private Material? _distantCloudMaterialTemplate;
    private Material? _flyThroughCloudMaterialTemplate;
    private float _cloudDensityMapScale;
    private float _cloudSizeMin;
    private float _cloudSizeMax;
    private float _cloudDrawDistance;
    private int _cloudGenerationRate;
    private int _cloudMaxParticles;
    private int _cachedSceneHandle;
    private int _pendingCacheSceneHandle;
    private float _nextScenePollTime;
    private float _nextMissingObjectDiagnosticTime;
    private float _menuWarmupStartTime;
    private bool _gameStateListenerRegistered;
    private bool _menuWarmupStarted;
    private bool _menuWarmupInProgress;
    private bool _menuWarmupComplete;
    private bool _menuWarmupFailed;
    private bool _menuWarmupSceneLoadedByCache;
    private Scene _menuWarmupScene;
    private GameState _preWarmupGameState;

    public static NativeMenuEnvironmentAssetCache? Instance { get; private set; }

    public bool HasWaterMaterial => IsAlive(_waterMaterialTemplate);
    public bool HasCloudPlane => IsAlive(_cloudPlaneTemplate);
    public bool HasCloudMaterials => IsAlive(_cloudLayerMaterialTemplate) && IsAlive(_cloudMaterialTemplate);
    public int CachedWeatherSetCount => _weatherSets.Length;
    public bool ShouldDelayMenuEnvironment =>
        _menuWarmupStarted &&
        !_menuWarmupComplete &&
        !_menuWarmupFailed &&
        !HasCloudPlane &&
        Time.unscaledTime - _menuWarmupStartTime < MenuWarmupDelayTimeoutSeconds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[NOVR] Native menu environment asset cache ready.");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        RegisterGameStateListener();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        UnregisterGameStateListener();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        DestroyCachedMaterial(ref _waterMaterialTemplate);
        DestroyCachedTexture(ref _menuOceanBasecolor);
        DestroyCachedMaterial(ref _cloudLayerMaterialTemplate);
        DestroyCachedMaterial(ref _cloudMaterialTemplate);
        DestroyCachedMaterial(ref _distantCloudMaterialTemplate);
        DestroyCachedMaterial(ref _flyThroughCloudMaterialTemplate);
        DestroyCachedGameObject(ref _cloudPlaneTemplate);
    }

    public bool TryCreateWaterMaterial(out Material? material)
    {
        material = null;
        if (!IsAlive(_waterMaterialTemplate))
        {
            return false;
        }

        material = new Material(_waterMaterialTemplate)
        {
            name = "NOVR Menu Encyclopedia WaterMat",
            hideFlags = HideFlags.DontSave
        };

        ApplyCachedWaterTextures(material);
        return true;
    }

    public bool TryCreateCloudPlane(out GameObject? cloudPlane)
    {
        cloudPlane = null;
        if (!IsAlive(_cloudPlaneTemplate))
        {
            return false;
        }

        var template = _cloudPlaneTemplate;
        if (template == null)
        {
            return false;
        }

        cloudPlane = Object.Instantiate(template);
        cloudPlane.hideFlags = HideFlags.DontSave;
        return true;
    }

    public void EnsureMenuWarmupStarted()
    {
        if (_menuWarmupStarted || _menuWarmupComplete || _menuWarmupFailed || HasCloudPlane)
        {
            return;
        }

        if (!IsMainMenuLoaded())
        {
            return;
        }

        if (GameManager.gameState == GameState.SinglePlayer ||
            GameManager.gameState == GameState.Multiplayer ||
            GameManager.gameState == GameState.Editor ||
            GameManager.gameState == GameState.Encyclopedia ||
            GameManager.gameState == GameState.ServerWaiting)
        {
            return;
        }

        _menuWarmupStarted = true;
        _menuWarmupInProgress = true;
        _menuWarmupStartTime = Time.unscaledTime;
        _preWarmupGameState = GameManager.gameState;
        StartCoroutine(WarmMenuAssetsFromEncyclopediaScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsMainMenuScene(scene))
        {
            EnsureMenuWarmupStarted();
        }

        if (_menuWarmupInProgress && IsEncyclopediaScene(scene))
        {
            _menuWarmupScene = scene;
            DisableWarmupSceneRoots(scene);
        }

        TryScheduleEncyclopediaCache(scene, "sceneLoaded");
    }

    private void RegisterGameStateListener()
    {
        if (_gameStateListenerRegistered) return;

        GameManager.OnGameStateChanged.AddListener(OnGameStateChanged);
        _gameStateListenerRegistered = true;
    }

    private void UnregisterGameStateListener()
    {
        if (!_gameStateListenerRegistered) return;

        GameManager.OnGameStateChanged.RemoveListener(OnGameStateChanged);
        _gameStateListenerRegistered = false;
    }

    private void OnGameStateChanged()
    {
        if (GameManager.gameState != GameState.Encyclopedia)
        {
            return;
        }

        Debug.Log($"[NOVR] Native menu environment asset cache observed Encyclopedia state; scenes={DescribeLoadedScenes()}.");
        StartCoroutine(CacheAfterGameStateSettles());
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (!IsEncyclopediaScene(scene))
        {
            return;
        }

        if (_menuWarmupScene.handle == scene.handle)
        {
            _menuWarmupScene = default;
            _menuWarmupSceneLoadedByCache = false;
        }

        if (_cachedSceneHandle == scene.handle)
        {
            _cachedSceneHandle = 0;
        }

        if (_pendingCacheSceneHandle == scene.handle)
        {
            _pendingCacheSceneHandle = 0;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextScenePollTime)
        {
            return;
        }

        _nextScenePollTime = Time.unscaledTime + ScenePollIntervalSeconds;
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            TryScheduleEncyclopediaCache(SceneManager.GetSceneAt(index), "poll");
        }

        EnsureMenuWarmupStarted();

        if (GameManager.gameState == GameState.Encyclopedia)
        {
            TryCacheFromLiveObjects("live-object-poll");
        }
    }

    private IEnumerator WarmMenuAssetsFromEncyclopediaScene()
    {
        Debug.Log("[NOVR] Native menu environment starting hidden Encyclopedia asset warmup.");

        yield return null;

        var scene = FindLoadedScene(EncyclopediaScenePath, EncyclopediaSceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            AsyncOperation? loadOperation;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(EncyclopediaScenePath, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                MarkMenuWarmupFailed($"could not start hidden Encyclopedia load: {exception.GetType().Name}: {exception.Message}");
                yield break;
            }

            if (loadOperation == null)
            {
                MarkMenuWarmupFailed("SceneManager returned null for hidden Encyclopedia load.");
                yield break;
            }

            _menuWarmupSceneLoadedByCache = true;
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            scene = FindLoadedScene(EncyclopediaScenePath, EncyclopediaSceneName);
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            MarkMenuWarmupFailed("hidden Encyclopedia scene load finished without a valid scene.");
            yield break;
        }

        _menuWarmupScene = scene;
        DisableWarmupSceneRoots(scene);
        CacheFromEncyclopediaScene(scene, "hidden-menu-warmup");

        if (!HasCloudPlane)
        {
            TryCacheFromLiveObjects("hidden-menu-warmup-live");
        }

        if (_menuWarmupSceneLoadedByCache && scene.IsValid() && scene.isLoaded)
        {
            var unloadOperation = SceneManager.UnloadSceneAsync(scene);
            if (unloadOperation != null)
            {
                while (!unloadOperation.isDone)
                {
                    yield return null;
                }
            }
        }

        RestoreGameStateAfterWarmup();

        _menuWarmupInProgress = false;
        _menuWarmupComplete = HasCloudPlane;
        _menuWarmupFailed = !_menuWarmupComplete;
        Debug.Log(
            "[NOVR] Native menu environment hidden Encyclopedia asset warmup finished: " +
            $"cloudPlane={HasCloudPlane}, waterMaterial={HasWaterMaterial}, weatherSets={_weatherSets.Length}, failed={_menuWarmupFailed}.");
    }

    private void MarkMenuWarmupFailed(string reason)
    {
        RestoreGameStateAfterWarmup();
        _menuWarmupInProgress = false;
        _menuWarmupComplete = false;
        _menuWarmupFailed = true;
        Debug.LogWarning($"[NOVR] Native menu environment hidden Encyclopedia asset warmup failed: {reason}");
    }

    private void RestoreGameStateAfterWarmup()
    {
        if (GameManager.gameState != GameState.Encyclopedia)
        {
            return;
        }

        var restoreState = _preWarmupGameState == GameState.Uninitialized
            ? GameState.Menu
            : _preWarmupGameState;
        GameManager.SetGameState(restoreState);
        Debug.Log($"[NOVR] Native menu environment restored game state to {restoreState} after hidden asset warmup.");
    }

    private IEnumerator CacheAfterGameStateSettles()
    {
        yield return null;
        yield return null;
        yield return null;

        TryCacheFromLiveObjects("game-state");
    }

    private void TryScheduleEncyclopediaCache(Scene scene, string trigger)
    {
        if (!IsEncyclopediaScene(scene) || !scene.isLoaded)
        {
            return;
        }

        if (_cachedSceneHandle == scene.handle || _pendingCacheSceneHandle == scene.handle)
        {
            return;
        }

        _pendingCacheSceneHandle = scene.handle;
        StartCoroutine(CacheAfterSceneSettles(scene, trigger));
    }

    private IEnumerator CacheAfterSceneSettles(Scene scene, string trigger)
    {
        yield return null;
        yield return null;
        yield return null;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (_pendingCacheSceneHandle == scene.handle)
            {
                _pendingCacheSceneHandle = 0;
            }

            yield break;
        }

        CacheFromEncyclopediaScene(scene, trigger);
        _cachedSceneHandle = scene.handle;
        if (_pendingCacheSceneHandle == scene.handle)
        {
            _pendingCacheSceneHandle = 0;
        }
    }

    private void CacheFromEncyclopediaScene(Scene scene, string trigger)
    {
        CacheFromObjects(
            FindSceneComponent<MapSettings>(scene),
            FindSceneComponent<LevelInfo>(scene),
            FindSceneComponent<CloudLayer>(scene),
            trigger);
    }

    private void TryCacheFromLiveObjects(string trigger)
    {
        if (HasWaterMaterial && HasCloudMaterials && _weatherSets.Length > 0)
        {
            return;
        }

        var mapSettings = FindBestLoadedMapSettings();
        var levelInfo = FindBestLoadedObject<LevelInfo>("LevelInfo");
        var cloudLayer = FindBestLoadedObject<CloudLayer>("cloudPlane");
        if (mapSettings == null && levelInfo == null && cloudLayer == null)
        {
            LogMissingObjectDiagnostic(trigger);
            return;
        }

        CacheFromObjects(mapSettings, levelInfo, cloudLayer, trigger);
    }

    private void CacheFromObjects(MapSettings? mapSettings, LevelInfo? levelInfo, CloudLayer? cloudLayer, string trigger)
    {
        if (mapSettings != null)
        {
            _mapSize = mapSettings.MapSize;
            _oceanBasecolor = mapSettings.OceanBasecolor;
            _oceanDepthmap = mapSettings.OceanDepthmap;
            _terrainColorMap = mapSettings.TerrainColorMap;
        }

        var waterMaterial = GetFieldValue<Material>(levelInfo, "waterMaterial");
        ReplaceMaterialTemplate(ref _waterMaterialTemplate, waterMaterial, "NOVR Cached Encyclopedia WaterMat");
        if (_waterMaterialTemplate != null)
        {
            ApplyCachedWaterTextures(_waterMaterialTemplate);
        }

        CacheCloudLayer(cloudLayer);

        Debug.Log(
            $"[NOVR] Native menu environment cached Encyclopedia assets via {trigger}: " +
            $"waterMaterial={Describe(_waterMaterialTemplate)}, " +
            $"oceanBasecolor={Describe(_oceanBasecolor)}, " +
            $"oceanDepthmap={Describe(_oceanDepthmap)}, " +
            $"terrainColorMap={Describe(_terrainColorMap)}, " +
            $"weatherSets={_weatherSets.Length}, " +
            $"cloudLayerMaterial={Describe(_cloudLayerMaterialTemplate)}, " +
            $"cloudMaterial={Describe(_cloudMaterialTemplate)}, " +
            $"distantCloudMaterial={Describe(_distantCloudMaterialTemplate)}, " +
            $"flyThroughCloudMaterial={Describe(_flyThroughCloudMaterialTemplate)}, " +
            $"cloudDensityScale={_cloudDensityMapScale}, " +
            $"cloudSize={_cloudSizeMin}-{_cloudSizeMax}, " +
            $"cloudDrawDist={_cloudDrawDistance}, " +
            $"cloudGenerationRate={_cloudGenerationRate}, " +
            $"cloudMaxParticles={_cloudMaxParticles}.");
    }

    private static bool IsEncyclopediaScene(Scene scene)
    {
        return scene.IsValid() &&
               (string.Equals(scene.name, EncyclopediaSceneName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scene.path, EncyclopediaScenePath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMainMenuScene(Scene scene)
    {
        return scene.IsValid() &&
               (string.Equals(scene.name, MainMenuSceneName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scene.path, MainMenuScenePath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMainMenuLoaded()
    {
        return FindLoadedScene(MainMenuScenePath, MainMenuSceneName).IsValid();
    }

    private static Scene FindLoadedScene(string scenePath, string sceneName)
    {
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            if (string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return scene;
            }
        }

        return default;
    }

    private static void DisableWarmupSceneRoots(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        var roots = scene.GetRootGameObjects();
        for (var index = 0; index < roots.Length; index++)
        {
            var root = roots[index];
            if (root != null && root.activeSelf)
            {
                root.SetActive(false);
            }
        }
    }

    private void LogMissingObjectDiagnostic(string trigger)
    {
        if (Time.unscaledTime < _nextMissingObjectDiagnosticTime)
        {
            return;
        }

        _nextMissingObjectDiagnosticTime = Time.unscaledTime + 2f;
        Debug.Log(
            $"[NOVR] Native menu environment asset cache waiting for Encyclopedia objects via {trigger}: " +
            $"mapSettings={Resources.FindObjectsOfTypeAll<MapSettings>().Length}, " +
            $"levelInfo={Resources.FindObjectsOfTypeAll<LevelInfo>().Length}, " +
            $"cloudLayer={Resources.FindObjectsOfTypeAll<CloudLayer>().Length}, " +
            $"scenes={DescribeLoadedScenes()}.");
    }

    private static string DescribeLoadedScenes()
    {
        var descriptions = new string[SceneManager.sceneCount];
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            descriptions[index] = $"'{scene.name}' path='{scene.path}' loaded={scene.isLoaded}";
        }

        return string.Join("; ", descriptions);
    }

    private void CacheCloudLayer(CloudLayer? cloudLayer)
    {
        if (cloudLayer == null)
        {
            _weatherSets = Array.Empty<WeatherSet>();
            return;
        }

        ReplaceCloudPlaneTemplate(cloudLayer.gameObject);
        _weatherSets = GetFieldValue<WeatherSet[]>(cloudLayer, "weatherSets") ?? Array.Empty<WeatherSet>();
        ReplaceMaterialTemplate(ref _cloudLayerMaterialTemplate, GetFieldValue<Material>(cloudLayer, "layerMaterial"), "NOVR Cached Cloudlayer");
        ReplaceMaterialTemplate(ref _cloudMaterialTemplate, GetFieldValue<Material>(cloudLayer, "cloudMaterial"), "NOVR Cached MeshCloud");
        ReplaceMaterialTemplate(ref _distantCloudMaterialTemplate, GetFieldValue<Material>(cloudLayer, "distantCloudMaterial"), "NOVR Cached MeshCloud Distant");
        ReplaceMaterialTemplate(ref _flyThroughCloudMaterialTemplate, GetFieldValue<Material>(cloudLayer, "flyThroughMaterial"), "NOVR Cached MeshCloud Close");

        _cloudDensityMapScale = GetFieldValue<float>(cloudLayer, "densityMapScale");
        _cloudSizeMin = GetFieldValue<float>(cloudLayer, "cloudSizeMin");
        _cloudSizeMax = GetFieldValue<float>(cloudLayer, "cloudSizeMax");
        _cloudDrawDistance = GetFieldValue<float>(cloudLayer, "cloudDrawDist");
        _cloudGenerationRate = GetFieldValue<int>(cloudLayer, "generationRate");
        _cloudMaxParticles = GetFieldValue<int>(cloudLayer, "maxParticles");
    }

    private void ApplyCachedWaterTextures(Material material)
    {
        var basecolor = GetMenuOceanBasecolorTexture();
        SetMaterialTexture(material, "_macro_basecolor", basecolor);
        SetMaterialTexture(material, "_macro_depth", _oceanDepthmap);
        SetMaterialTexture(material, "_BaseMap", basecolor);
        SetMaterialTexture(material, "_MainTex", basecolor);

        if (material.HasProperty("_size"))
        {
            material.SetVector("_size", new Vector4(_mapSize.x, _mapSize.y, 0f, 0f));
        }

        if (material.HasProperty("_OriginOffset"))
        {
            material.SetVector("_OriginOffset", Vector4.zero);
        }
    }

    private Texture2D GetMenuOceanBasecolorTexture()
    {
        if (_menuOceanBasecolor != null)
        {
            return _menuOceanBasecolor;
        }

        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "NOVR Menu Deep Ocean Basecolor",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            hideFlags = HideFlags.HideAndDontSave
        };

        var deep = new Color(0.025f, 0.15f, 0.28f, 1f);
        var mid = new Color(0.04f, 0.22f, 0.36f, 1f);
        var highlight = new Color(0.065f, 0.31f, 0.46f, 1f);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var broad = Mathf.PerlinNoise(x * 0.035f, y * 0.035f);
                var fine = Mathf.PerlinNoise(x * 0.12f + 17.3f, y * 0.12f + 42.1f);
                var wave = Mathf.Sin((x * 0.12f) + (y * 0.05f)) * 0.08f;
                var t = Mathf.Clamp01(0.18f + broad * 0.52f + fine * 0.16f + wave);
                var color = t < 0.58f
                    ? Color.Lerp(deep, mid, t / 0.58f)
                    : Color.Lerp(mid, highlight, (t - 0.58f) / 0.42f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(true, true);
        _menuOceanBasecolor = texture;
        return texture;
    }

    private static void SetMaterialTexture(Material material, string propertyName, Texture? texture)
    {
        if (texture != null && material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private void ReplaceCloudPlaneTemplate(GameObject? source)
    {
        if (source == null)
        {
            return;
        }

        DestroyCachedGameObject(ref _cloudPlaneTemplate);
        _cloudPlaneTemplate = InstantiateInactive(source);
        _cloudPlaneTemplate.name = "NOVR Cached Encyclopedia cloudPlane";
        _cloudPlaneTemplate.hideFlags = HideFlags.HideAndDontSave;
        _cloudPlaneTemplate.transform.SetParent(transform, false);
        _cloudPlaneTemplate.SetActive(false);
        StripCloudLayerComponents(_cloudPlaneTemplate);
    }

    private static GameObject InstantiateInactive(GameObject source)
    {
        var wasActive = source.activeSelf;
        source.SetActive(false);
        try
        {
            return Object.Instantiate(source);
        }
        finally
        {
            source.SetActive(wasActive);
        }
    }

    private static void StripCloudLayerComponents(GameObject root)
    {
        var components = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (var index = 0; index < components.Length; index++)
        {
            var component = components[index];
            if (component == null ||
                !string.Equals(component.GetType().Name, "CloudLayer", StringComparison.Ordinal))
            {
                continue;
            }

            Object.DestroyImmediate(component);
        }
    }

    private static void ReplaceMaterialTemplate(ref Material? target, Material? source, string name)
    {
        if (source == null)
        {
            return;
        }

        DestroyCachedMaterial(ref target);
        target = new Material(source)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static void DestroyCachedMaterial(ref Material? material)
    {
        if (material == null)
        {
            return;
        }

        Object.Destroy(material);
        material = null;
    }

    private static void DestroyCachedTexture(ref Texture2D? texture)
    {
        if (texture == null)
        {
            return;
        }

        Object.Destroy(texture);
        texture = null;
    }

    private static void DestroyCachedGameObject(ref GameObject? gameObject)
    {
        if (gameObject == null)
        {
            return;
        }

        Object.Destroy(gameObject);
        gameObject = null;
    }

    private static T? FindSceneComponent<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            if (root == null) continue;

            var components = root.GetComponentsInChildren<T>(true);
            for (var index = 0; index < components.Length; index++)
            {
                if (components[index] != null)
                {
                    return components[index];
                }
            }
        }

        return null;
    }

    private static MapSettings? FindBestLoadedMapSettings()
    {
        var mapSettings = Resources.FindObjectsOfTypeAll<MapSettings>();
        MapSettings? fallback = null;
        for (var index = 0; index < mapSettings.Length; index++)
        {
            var candidate = mapSettings[index];
            if (candidate == null) continue;

            fallback ??= candidate;
            if (candidate.OceanBasecolor != null || candidate.OceanDepthmap != null)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private static T? FindBestLoadedObject<T>(string preferredName) where T : Component
    {
        var objects = Resources.FindObjectsOfTypeAll<T>();
        T? fallback = null;
        for (var index = 0; index < objects.Length; index++)
        {
            var candidate = objects[index];
            if (candidate == null) continue;

            fallback ??= candidate;
            if (string.Equals(candidate.name, preferredName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return fallback;
    }

    private static T? GetFieldValue<T>(object? source, string fieldName)
    {
        if (source == null)
        {
            return default;
        }

        var field = source.GetType().GetField(fieldName, InstanceFields);
        if (field == null)
        {
            return default;
        }

        var value = field.GetValue(source);
        return value is T typed ? typed : default;
    }

    private static bool IsAlive(Object? obj)
    {
        return obj != null;
    }

    private static string Describe(Object? obj)
    {
        if (obj == null)
        {
            return "null";
        }

        return $"{obj.GetType().Name} '{obj.name}'";
    }
}
