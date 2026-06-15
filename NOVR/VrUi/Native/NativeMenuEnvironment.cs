using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NOVR.VrUi.Native;

public sealed class NativeMenuEnvironment : MonoBehaviour
{
    private const string EnvironmentSceneName = "NOVR Menu Environment Scene";
    private const string EnvironmentRootName = "NOVR Native Menu Environment";
    private const string VisualRootName = "Visuals";
    private const string PreviewUnitName = "AttackHelo1";
    private const string PreviewShipName = "Frigate1";
    private const string SimpleWaterPlaneName = "NOVR Menu Environment Water Plane";
    private const string CloudPlaneName = "cloudPlane";
    private const string MenuCloudPlaneName = "NOVR Menu Environment Cloud Plane";
    private const string ShipWakeRootName = "wakefoam";
    private const string ShipHangarDoorName = "frigate1_hangarDoor";
    private const string MenuCameraName = "Menu Camera";
    private const string DefaultSkyboxMaterialName = "Default-Skybox";
    private const string EncyclopediaSceneName = "Encyclopedia";
    private const float EnvironmentCameraFarClipPlane = 80000f;
    private const float MenuEnvironmentFogDensity = 0.00105f;
    private const float ShipWakeFlowSpeed = 40f;
    private const float ShipHangarDoorOpenDelaySeconds = 3.0f;
    private const float ShipHangarDoorOpenDurationSeconds = 4.0f;
    private static readonly Vector3 MenuEnvironmentWorldAnchor = new(-23.8808f, 2.774f, -821.3446f);
    private static readonly Vector3 SimpleWaterPlaneLocalPosition = new(0f, -10f, -9.66f);
    private static readonly Vector3 SimpleWaterPlaneLocalScale = new(9000f, 1f, 9000f);
    private static readonly Vector3 CloudPlaneLocalPosition = new(0f, 500f, 0f);
    private static readonly Vector3 CloudPlaneLocalScale = new(1f, 10f, 1f);
    private static readonly Vector3 ShipHangarDoorOpenLocalOffset = new(0f, 5f, 2f);
    private static readonly Color MenuEnvironmentFogColor = new(0.52f, 0.68f, 0.80f, 1f);
    private static readonly Color CloudMaterialColor = new(3.75f, 3.78f, 1.72f, 0.98f);
    private static readonly Color CloudLayerParticleColor = new(1f, 1f, 1f, 0.5f);
    private static readonly Color DistantCloudParticleColor = Color.white;
    private static readonly Color SimpleWaterBaseColor = new(0.12f, 0.32f, 0.42f, 1f);
    private static readonly Color SimpleWaterHighlightColor = new(0.28f, 0.58f, 0.70f, 1f);
    private static readonly HashSet<string> HiddenShipPreviewChildNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "frigate1_hull_port",
        "frigate1_hull_starboard",
        "frigate1_hull_F",
    };
    private static readonly Vector3 ShipHangarSourceAnchor = new(0f, 3.8098f, -22.1115f);
    private static readonly Vector3 ShipHangarTargetAnchor = new(0f, 1.25f, 0.45f);
    private static readonly Vector3 PreviewLocalOffset = new(23.8808f, -2.774f, 821.3446f);
    private static readonly Vector3 ShipEuler = new(0f, 180f, 0f);
    private const float ShipScale = 1f;
    private static readonly Vector3 AircraftDeckSourceAnchor = new(0f, 3.55f, -37.5f);
    private static readonly Vector3 AircraftDeckOffset = new(0f, -0.6f, 6f);
    private static readonly Vector3 AircraftEuler = new(0f, 180f, 0f);
    private const float AircraftScale = 1f;
    private static readonly HashSet<string> PreviewGameplayComponentNames = new(StringComparer.Ordinal)
    {
        "AeroPart",
        "Aircraft",
        "AircraftAI",
        "AircraftNetworkTransform",
        "Airbase",
        "Capture",
        "ControlSurface",
        "FireControl",
        "FlareEjector",
        "FuelTank",
        "Hangar",
        "NavLights",
        "NetworkIdentity",
        "Pilot",
        "PowerSupply",
        "RadarJammer",
        "SetGlobalParticles",
        "Ship",
        "ShipAI",
        "ShipNetworkTransform",
        "ShipPart",
        "ShipPropulsion",
        "TargetCam",
        "TargetDetector",
        "Transmission",
        "Turret",
        "UnitCommand",
        "UnitPart",
    };

    private GameObject? _environmentRoot;
    private Transform? _visualRoot;
    private GameObject? _shipRoot;
    private GameObject? _previewUnit;
    private GameObject? _waterPlane;
    private GameObject? _cloudPlane;
    private Material? _waterMaterial;
    private Texture2D? _waterTexture;
    private Camera? _environmentCamera;
    private CameraClearFlags _originalEnvironmentCameraClearFlags;
    private Color _originalEnvironmentCameraBackgroundColor;
    private float _originalEnvironmentCameraFarClipPlane;
    private int _originalEnvironmentCameraCullingMask;
    private bool _originalEnvironmentCameraRenderPostProcessing;
    private Volume? _postProcessingVolume;
    private bool _environmentRenderSettingsApplied;
    private bool _environmentCameraSettingsCaptured;
    private bool _gameStateListenerRegistered;
    private bool _sceneUnloadedListenerRegistered;
    private Scene _environmentScene;
    private RenderSettingsSnapshot _originalRenderSettings;
    private bool _renderSettingsCaptured;
    private bool _spawnAttempted;
    private bool _loggedWarmupDelay;

    private struct RenderSettingsSnapshot
    {
        public Material? Skybox;
        public AmbientMode AmbientMode;
        public Color AmbientLight;
        public Color AmbientSkyColor;
        public Color AmbientEquatorColor;
        public Color AmbientGroundColor;
        public float AmbientIntensity;
        public bool Fog;
        public FogMode FogMode;
        public Color FogColor;
        public float FogDensity;
        public float FogStartDistance;
        public float FogEndDistance;
        public float ReflectionIntensity;
    }

    public void UpdateEnvironment(Transform menuTransform, bool shouldShow)
    {
        var enabled = shouldShow && ModConfiguration.Instance.EnableNativeMenuEnvironment.Value;
        if (!enabled)
        {
            Hide();
            return;
        }

        var assetCache = NativeMenuEnvironmentAssetCache.Instance;
        assetCache?.EnsureMenuWarmupStarted();
        if (assetCache?.ShouldDelayMenuEnvironment == true)
        {
            if (!_loggedWarmupDelay)
            {
                Debug.Log("[NOVR] Native menu environment waiting for hidden Encyclopedia asset warmup.");
                _loggedWarmupDelay = true;
            }

            return;
        }

        _loggedWarmupDelay = false;

        if (!EnsureEnvironmentScene())
        {
            return;
        }

        EnsureRoot();
        PlaceRoot(menuTransform);
        EnsureSimpleWaterPlane();
        EnsureCloudPlane();
        ApplyEnvironmentRenderSettings();
        ApplyEnvironmentCameraSettings();
        EnsurePreviewScene();
        SetVisible(true);
        ApplyShipVisualOverrides();
        ApplyAircraftVisualOverrides();
    }

    private void OnEnable()
    {
        RegisterGameStateListener();
        RegisterSceneUnloadedListener();
    }

    private void OnDisable()
    {
        UnregisterGameStateListener();
        UnregisterSceneUnloadedListener();
        Hide();
    }

    public void Hide()
    {
        SetVisible(false);
        RestoreEnvironmentCameraSettings();
        RestoreEnvironmentRenderSettings();
    }

    private void OnDestroy()
    {
        UnregisterGameStateListener();
        UnregisterSceneUnloadedListener();
        DestroyEnvironmentScene();
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
        if (GameManager.gameState == GameState.Menu) return;

        DestroyEnvironmentScene();
    }

    private void RegisterSceneUnloadedListener()
    {
        if (_sceneUnloadedListenerRegistered) return;

        SceneManager.sceneUnloaded += OnSceneUnloaded;
        _sceneUnloadedListenerRegistered = true;
    }

    private void UnregisterSceneUnloadedListener()
    {
        if (!_sceneUnloadedListenerRegistered) return;

        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        _sceneUnloadedListenerRegistered = false;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (!string.Equals(scene.name, EnvironmentSceneName, StringComparison.OrdinalIgnoreCase)) return;

        if (_environmentRoot != null)
        {
            _environmentScene = default;
            Debug.Log($"[NOVR] Native menu environment preserved root after scene '{EnvironmentSceneName}' unloaded during menu scene transition.");
            return;
        }

        ClearEnvironmentSceneObjectReferences();
        ResetSceneScopedRenderingState();
        _environmentScene = default;
        Debug.Log($"[NOVR] Native menu environment cleared cached objects after scene '{EnvironmentSceneName}' unloaded.");
    }

    private bool EnsureEnvironmentScene()
    {
        if (_environmentRoot != null)
        {
            return true;
        }

        if (_environmentScene.IsValid() && _environmentScene.isLoaded)
        {
            ResetSceneObjectReferencesIfDestroyed();
            return true;
        }

        ClearEnvironmentSceneObjectReferences();

        var existingScene = FindLoadedSceneByName(EnvironmentSceneName);
        if (existingScene.IsValid() && existingScene.isLoaded)
        {
            _environmentScene = existingScene;
            return true;
        }

        _environmentScene = SceneManager.CreateScene(EnvironmentSceneName);
        if (!_environmentScene.IsValid() || !_environmentScene.isLoaded)
        {
            Debug.LogWarning("[NOVR] Native menu environment could not create its scene.");
            _environmentScene = default;
            return false;
        }

        Debug.Log($"[NOVR] Native menu environment created scene '{_environmentScene.name}'.");
        return true;
    }

    private void DestroyEnvironmentScene()
    {
        SetVisible(false);
        StopAllCoroutines();
        RestoreEnvironmentCameraSettings();
        RestoreEnvironmentRenderSettings();
        ClearEnvironmentSceneObjectReferences(destroyRoot: true);

        if (_environmentScene.IsValid() && _environmentScene.isLoaded)
        {
            SceneManager.UnloadSceneAsync(_environmentScene);
            Debug.Log($"[NOVR] Native menu environment unloaded scene '{EnvironmentSceneName}'.");
        }

        _environmentScene = default;
    }

    private void ResetSceneObjectReferencesIfDestroyed()
    {
        if (_environmentRoot != null) return;

        ClearEnvironmentSceneObjectReferences();
    }

    private void ClearEnvironmentSceneObjectReferences(bool destroyRoot = false)
    {
        if (destroyRoot && _environmentRoot != null)
        {
            Object.Destroy(_environmentRoot);
        }

        DestroySimpleWaterResources();

        _environmentRoot = null;
        _visualRoot = null;
        _shipRoot = null;
        _previewUnit = null;
        _waterPlane = null;
        _cloudPlane = null;
        _spawnAttempted = false;
    }

    private void ResetSceneScopedRenderingState()
    {
        _postProcessingVolume = null;
        _environmentCamera = null;
        _environmentCameraSettingsCaptured = false;
        _environmentRenderSettingsApplied = false;
        _renderSettingsCaptured = false;
    }

    private static Scene FindLoadedSceneByName(string sceneName)
    {
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            if (string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return scene;
            }
        }

        return default;
    }

    private void EnsureRoot()
    {
        if (_environmentRoot != null) return;

        _environmentRoot = new GameObject(EnvironmentRootName);
        if (_environmentScene.IsValid() && _environmentScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(_environmentRoot, _environmentScene);
        }

        DontDestroyOnLoad(_environmentRoot);

        var visualRoot = new GameObject(VisualRootName);
        visualRoot.transform.SetParent(_environmentRoot.transform, false);
        _visualRoot = visualRoot.transform;

        Debug.Log("[NOVR] Native menu environment root created.");
    }

    private void EnsureSimpleWaterPlane()
    {
        if (_visualRoot == null || _waterPlane != null) return;

        _waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        _waterPlane.name = SimpleWaterPlaneName;
        _waterPlane.transform.SetParent(_visualRoot, false);
        _waterPlane.transform.localPosition = SimpleWaterPlaneLocalPosition;
        _waterPlane.transform.localRotation = Quaternion.identity;
        _waterPlane.transform.localScale = SimpleWaterPlaneLocalScale;
        LayerHelper.SetLayerRecursive(_waterPlane.transform, LayerHelper.GetVrUiLayer());

        var collider = _waterPlane.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }

        var renderer = _waterPlane.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var waterMaterial = CreateSimpleWaterMaterial();
            if (waterMaterial != null)
            {
                renderer.sharedMaterial = waterMaterial;
            }
            else
            {
                renderer.enabled = false;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        Debug.Log("[NOVR] Native menu environment created simple water plane.");
    }

    private void EnsureCloudPlane()
    {
        if (_visualRoot == null || _cloudPlane != null) return;

        if (NativeMenuEnvironmentAssetCache.Instance?.TryCreateCloudPlane(out var cloudPlane) != true ||
            cloudPlane == null)
        {
            return;
        }

        _cloudPlane = cloudPlane;
        _cloudPlane.name = MenuCloudPlaneName;
        _cloudPlane.SetActive(false);
        _cloudPlane.transform.SetParent(_visualRoot, false);
        _cloudPlane.transform.localPosition = CloudPlaneLocalPosition;
        _cloudPlane.transform.localRotation = Quaternion.identity;
        _cloudPlane.transform.localScale = CloudPlaneLocalScale;

        PrepareCloudPlane(_cloudPlane);
        if (_cloudPlane.GetComponent<MenuEnvironmentCloudWind>() == null)
        {
            _cloudPlane.AddComponent<MenuEnvironmentCloudWind>();
        }

        _cloudPlane.SetActive(true);
        PrepareCloudPlane(_cloudPlane);

        Debug.Log("[NOVR] Native menu environment cloned cached Encyclopedia cloud plane.");
    }

    private Material? CreateSimpleWaterMaterial()
    {
        if (_waterMaterial != null) return _waterMaterial;

        if (NativeMenuEnvironmentAssetCache.Instance?.TryCreateWaterMaterial(out var cachedWaterMaterial) == true &&
            cachedWaterMaterial != null)
        {
            _waterMaterial = cachedWaterMaterial;
            Debug.Log("[NOVR] Native menu environment using cached Encyclopedia water material.");
            return cachedWaterMaterial;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                     Shader.Find("Universal Render Pipeline/Unlit") ??
                     Shader.Find("Standard") ??
                     Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[NOVR] Native menu environment could not find a shader for the simple water plane.");
            return null;
        }

        var material = new Material(shader)
        {
            name = "NOVR Menu Environment Water"
        };

        var texture = CreateSimpleWaterTexture();
        SetMaterialTexture(material, texture);
        SetMaterialColor(material, SimpleWaterBaseColor);
        SetMaterialFloat(material, "_Smoothness", 0.72f);
        SetMaterialFloat(material, "_Metallic", 0f);
        SetMaterialTextureScale(material, new Vector2(280f, 280f));

        _waterMaterial = material;
        _waterTexture = texture;
        return material;
    }

    private static Texture2D CreateSimpleWaterTexture()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "NOVR Menu Environment Water Texture",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4
        };

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var waveA = Mathf.Sin((x + y * 0.55f) * 0.22f);
                var waveB = Mathf.Sin((x * -0.35f + y) * 0.15f);
                var ripple = Mathf.PerlinNoise(x * 0.075f, y * 0.075f);
                var t = Mathf.Clamp01(0.48f + waveA * 0.10f + waveB * 0.06f + (ripple - 0.5f) * 0.16f);
                texture.SetPixel(x, y, Color.Lerp(SimpleWaterBaseColor, SimpleWaterHighlightColor, t));
            }
        }

        texture.Apply(true, true);
        return texture;
    }

    private static void SetMaterialTexture(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private static void SetMaterialTextureScale(Material material, Vector2 scale)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", scale);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", scale);
        }
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private void DestroySimpleWaterResources()
    {
        if (_waterMaterial != null)
        {
            Object.Destroy(_waterMaterial);
            _waterMaterial = null;
        }

        if (_waterTexture != null)
        {
            Object.Destroy(_waterTexture);
            _waterTexture = null;
        }
    }

    private static void PrepareCloudPlane(GameObject root)
    {
        StripCloudPlaneGameplayComponents(root);
        LayerHelper.SetLayerRecursive(root.transform, LayerHelper.GetVrUiLayer());
        SetMatchingChildrenActive(root.transform, static transform => string.Equals(transform.name, "lightning", StringComparison.OrdinalIgnoreCase), false);
        ApplyCloudMaterialOverrides(root);
        ApplyCloudParticleOverrides(root);

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (renderer == null) continue;

            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (var index = 0; index < particleSystems.Length; index++)
        {
            var particleSystem = particleSystems[index];
            if (particleSystem == null ||
                !particleSystem.gameObject.activeInHierarchy ||
                ShouldKeepCloudParticleSystemStopped(particleSystem))
            {
                continue;
            }

            particleSystem.Play(true);
        }
    }

    private static void ApplyCloudParticleOverrides(GameObject root)
    {
        var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (var index = 0; index < particleSystems.Length; index++)
        {
            var particleSystem = particleSystems[index];
            if (particleSystem == null) continue;

            if (IsCloudPlaneParticleSystem(root, particleSystem))
            {
                ConfigureCloudLayerParticles(particleSystem);
                continue;
            }

            if (string.Equals(particleSystem.gameObject.name, "distantClouds", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureDistantCloudParticles(particleSystem);
                continue;
            }

            if (ShouldKeepCloudParticleSystemStopped(particleSystem))
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.gameObject.SetActive(false);
            }
        }
    }

    private static bool IsCloudPlaneParticleSystem(GameObject root, ParticleSystem particleSystem)
    {
        return particleSystem.gameObject == root ||
               string.Equals(particleSystem.gameObject.name, CloudPlaneName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(particleSystem.gameObject.name, MenuCloudPlaneName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldKeepCloudParticleSystemStopped(ParticleSystem particleSystem)
    {
        var name = particleSystem.gameObject.name;
        return name.IndexOf("flyThrough", StringComparison.OrdinalIgnoreCase) >= 0 ||
               string.Equals(name, "lightning", StringComparison.OrdinalIgnoreCase);
    }

    private static void ConfigureCloudLayerParticles(ParticleSystem particleSystem)
    {
        var main = particleSystem.main;
        main.duration = 1f;
        main.loop = false;
        main.prewarm = false;
        main.startDelay = new ParticleSystem.MinMaxCurve(0f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(15.00058f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startSize = new ParticleSystem.MinMaxCurve(479.8216f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f);
        main.startColor = new ParticleSystem.MinMaxGradient(CloudLayerParticleColor);
        main.maxParticles = 3000;

        var emission = particleSystem.emission;
        emission.enabled = false;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(1f);

        var shape = particleSystem.shape;
        shape.enabled = false;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 1f;
        shape.angle = 25f;
        shape.arc = 360f;
        shape.scale = Vector3.one;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
    }

    private static void ConfigureDistantCloudParticles(ParticleSystem particleSystem)
    {
        var main = particleSystem.main;
        main.duration = 20f;
        main.loop = true;
        main.prewarm = false;
        main.startDelay = new ParticleSystem.MinMaxCurve(0f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(20f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f);
        main.startSize = new ParticleSystem.MinMaxCurve(540f, 810f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f);
        main.startColor = new ParticleSystem.MinMaxGradient(DistantCloudParticleColor);
        main.maxParticles = 300;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(25f);

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 40000f;
        shape.angle = 25f;
        shape.arc = 360f;
        shape.scale = Vector3.one;
        shape.position = Vector3.zero;
        shape.rotation = new Vector3(90f, 0f, 0f);
    }

    private static void ApplyCloudMaterialOverrides(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var renderer = renderers[rendererIndex];
            if (renderer == null) continue;

            var materials = renderer.sharedMaterials;
            var changed = false;

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null || !IsCloudMaterial(renderer, material)) continue;

                if (!material.name.EndsWith(" NOVR", StringComparison.Ordinal))
                {
                    material = new Material(material)
                    {
                        name = $"{material.name} NOVR"
                    };
                    materials[materialIndex] = material;
                    changed = true;
                }

                SetCloudMaterialColor(material);
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static bool IsCloudMaterial(Renderer renderer, Material material)
    {
        return ContainsCloudToken(renderer.gameObject.name) ||
               ContainsCloudToken(material.name) ||
               (material.shader != null && ContainsCloudToken(material.shader.name));
    }

    private static bool ContainsCloudToken(string value)
    {
        return value.IndexOf("cloud", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SetCloudMaterialColor(Material material)
    {
        if (material.HasProperty("_CloudColor"))
        {
            material.SetColor("_CloudColor", CloudMaterialColor);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", CloudMaterialColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", CloudMaterialColor);
        }
    }

    private static int StripCloudPlaneGameplayComponents(GameObject root)
    {
        var stripped = 0;
        var components = root.GetComponentsInChildren<MonoBehaviour>(true);

        for (var index = 0; index < components.Length; index++)
        {
            var component = components[index];
            if (component == null) continue;

            if (!string.Equals(component.GetType().Name, "CloudLayer", StringComparison.Ordinal)) continue;

            Object.DestroyImmediate(component);
            stripped++;
        }

        if (stripped > 0)
        {
            Debug.Log($"[NOVR] Native menu environment stripped {stripped} cloud gameplay component(s) from '{GetTransformPath(root.transform)}'.");
        }

        return stripped;
    }

    private void PlaceRoot(Transform menuTransform)
    {
        if (_environmentRoot == null) return;

        _environmentRoot.transform.position = MenuEnvironmentWorldAnchor;
        _environmentRoot.transform.rotation = Quaternion.LookRotation(menuTransform.forward, Vector3.up);
        _environmentRoot.transform.localScale = Vector3.one;
    }

    private void EnsurePreviewScene()
    {
        if ((_shipRoot != null && _previewUnit != null) || _spawnAttempted || _visualRoot == null) return;

        var encyclopedia = Encyclopedia.i;
        if (encyclopedia == null) return;

        _spawnAttempted = true;
        var shipPrefab = FindUnitPrefab(GetMemberValue(encyclopedia, "ships"), PreviewShipName);
        if (shipPrefab != null)
        {
            _shipRoot = InstantiateInactive(shipPrefab);
            _shipRoot.name = $"NOVR Preview {shipPrefab.name}";
            _shipRoot.transform.SetParent(_visualRoot, false);
            _shipRoot.transform.localPosition = GetShipRootLocalPosition();
            _shipRoot.transform.localRotation = Quaternion.Euler(ShipEuler);
            _shipRoot.transform.localScale = Vector3.one * ShipScale;

            PreparePreviewObject(_shipRoot);
            ApplyShipVisualOverrides();
            _shipRoot.SetActive(true);
            ApplyShipVisualOverrides();
            ApplyShipWakeTuning();

            Debug.Log($"[NOVR] Native menu environment spawned preview ship prefab '{shipPrefab.name}' from Encyclopedia.");
        }
        else
        {
            Debug.LogWarning($"[NOVR] Native menu environment could not find preview ship prefab '{PreviewShipName}'.");
        }

        var aircraftPrefab = FindUnitPrefab(GetMemberValue(encyclopedia, "aircraft"), PreviewUnitName);
        if (aircraftPrefab == null)
        {
            Debug.LogWarning($"[NOVR] Native menu environment could not find preview aircraft prefab '{PreviewUnitName}'.");
            return;
        }

        _previewUnit = InstantiateInactive(aircraftPrefab);
        _previewUnit.name = $"NOVR Preview {aircraftPrefab.name}";
        _previewUnit.transform.SetParent(_visualRoot, false);
        _previewUnit.transform.localPosition = TransformShipSourcePoint(AircraftDeckSourceAnchor) + AircraftDeckOffset;
        _previewUnit.transform.localRotation = Quaternion.Euler(AircraftEuler);
        _previewUnit.transform.localScale = Vector3.one * AircraftScale;

        PreparePreviewObject(_previewUnit);
        _previewUnit.SetActive(true);
        ApplyAircraftVisualOverrides();

        Debug.Log($"[NOVR] Native menu environment spawned preview aircraft prefab '{aircraftPrefab.name}' from Encyclopedia.");
    }

    private static GameObject InstantiateInactive(GameObject prefab)
    {
        var wasActive = prefab.activeSelf;
        prefab.SetActive(false);
        try
        {
            return Object.Instantiate(prefab);
        }
        finally
        {
            prefab.SetActive(wasActive);
        }
    }

    private static GameObject? FindUnitPrefab(object? definitions, string prefabName)
    {
        if (definitions is not IEnumerable enumerable) return null;

        foreach (var definition in enumerable)
        {
            if (definition == null) continue;

            var prefab = GetMemberValue(definition, "unitPrefab") as GameObject;
            if (prefab == null) continue;

            if (string.Equals(prefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }

            if (MatchesMemberString(definition, "name", prefabName) ||
                MatchesMemberString(definition, "jsonKey", prefabName) ||
                MatchesMemberString(definition, "unitName", prefabName))
            {
                return prefab;
            }
        }

        return null;
    }

    private static object? GetMemberValue(object source, string memberName)
    {
        var type = source.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        var property = type.GetProperty(memberName, flags);
        if (property != null)
        {
            return property.GetValue(source, null);
        }

        var field = type.GetField(memberName, flags);
        return field?.GetValue(source);
    }

    private static bool MatchesMemberString(object source, string memberName, string value)
    {
        return GetMemberValue(source, memberName) is string memberValue &&
               string.Equals(memberValue, value, StringComparison.OrdinalIgnoreCase);
    }

    private static void PreparePreviewObject(GameObject root)
    {
        LayerHelper.SetLayerRecursive(root.transform, LayerHelper.GetVrUiLayer());

        var disabledBehaviours = 0;
        var strippedBehaviours = StripPreviewGameplayComponents(root);
        var mutedAudioSources = 0;
        var disabledColliders = 0;
        var kinematicRigidbodies = 0;
        var disabledLodGroups = 0;
        var enabledRenderers = 0;
        var lights = 0;

        var behaviours = root.GetComponentsInChildren<Behaviour>(true);
        for (var index = 0; index < behaviours.Length; index++)
        {
            var behaviour = behaviours[index];
            if (behaviour == null || behaviour is Light) continue;

            if (behaviour is MonoBehaviour)
            {
                behaviour.enabled = false;
                disabledBehaviours++;
            }
        }

        var audioSources = root.GetComponentsInChildren<AudioSource>(true);
        for (var index = 0; index < audioSources.Length; index++)
        {
            var audioSource = audioSources[index];
            if (audioSource == null) continue;

            audioSource.Stop();
            audioSource.enabled = false;
            mutedAudioSources++;
        }

        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (var index = 0; index < colliders.Length; index++)
        {
            var collider = colliders[index];
            if (collider == null) continue;

            collider.enabled = false;
            disabledColliders++;
        }

        var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (var index = 0; index < rigidbodies.Length; index++)
        {
            var rigidbody = rigidbodies[index];
            if (rigidbody == null) continue;

            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            kinematicRigidbodies++;
        }

        var cameras = root.GetComponentsInChildren<Camera>(true);
        for (var index = 0; index < cameras.Length; index++)
        {
            if (cameras[index] != null)
            {
                cameras[index].enabled = false;
            }
        }

        var lodGroups = root.GetComponentsInChildren<LODGroup>(true);
        for (var index = 0; index < lodGroups.Length; index++)
        {
            var lodGroup = lodGroups[index];
            if (lodGroup == null) continue;

            lodGroup.enabled = false;
            disabledLodGroups++;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (renderer == null) continue;

            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            enabledRenderers++;
        }

        var lightComponents = root.GetComponentsInChildren<Light>(true);
        for (var index = 0; index < lightComponents.Length; index++)
        {
            var light = lightComponents[index];
            if (light == null) continue;

            light.enabled = true;
            light.cullingMask = ~0;
            lights++;
        }

        var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (var index = 0; index < particleSystems.Length; index++)
        {
            var particleSystem = particleSystems[index];
            if (particleSystem == null) continue;

            particleSystem.Play(true);
        }

        Debug.Log(
            "[NOVR] Native menu environment prepared preview object: " +
            $"strippedBehaviours={strippedBehaviours}, disabledBehaviours={disabledBehaviours}, mutedAudioSources={mutedAudioSources}, " +
            $"disabledColliders={disabledColliders}, kinematicRigidbodies={kinematicRigidbodies}, disabledLodGroups={disabledLodGroups}, " +
            $"enabledRenderers={enabledRenderers}, lights={lights}.");
    }

    private static int StripPreviewGameplayComponents(GameObject root)
    {
        var stripped = 0;
        var components = root.GetComponentsInChildren<MonoBehaviour>(true);
        var targets = new List<MonoBehaviour>();

        for (var index = 0; index < components.Length; index++)
        {
            var component = components[index];
            if (component == null) continue;

            var componentType = component.GetType();
            if (!ShouldStripPreviewGameplayComponent(componentType)) continue;

            targets.Add(component);
        }

        targets.Sort(static (left, right) =>
        {
            var priorityCompare = GetPreviewStripPriority(left.GetType()).CompareTo(GetPreviewStripPriority(right.GetType()));
            if (priorityCompare != 0) return priorityCompare;

            return GetTransformDepth(right.transform).CompareTo(GetTransformDepth(left.transform));
        });

        for (var index = 0; index < targets.Count; index++)
        {
            var component = targets[index];
            if (component == null) continue;

            var componentType = component.GetType();
            Debug.Log($"[NOVR] Native menu environment stripping preview gameplay component '{componentType.FullName}' from '{GetTransformPath(component.transform)}'.");
            Object.DestroyImmediate(component);
            stripped++;
        }

        return stripped;
    }

    private static int GetPreviewStripPriority(Type componentType)
    {
        var name = componentType.Name;
        return name switch
        {
            "Airbase" => 0,
            "Hangar" => 0,
            "Aircraft" => 0,
            "Ship" => 0,
            "Capture" => 20,
            _ => 10,
        };
    }

    private static bool ShouldStripPreviewGameplayComponent(Type componentType)
    {
        if (PreviewGameplayComponentNames.Contains(componentType.Name))
        {
            return true;
        }

        var fullName = componentType.FullName;
        if (fullName == null) return false;

        return fullName.StartsWith("NuclearOption.NetworkTransforms.", StringComparison.Ordinal) ||
               fullName.StartsWith("Mirage.", StringComparison.Ordinal);
    }

    private void ApplyShipVisualOverrides()
    {
        if (_shipRoot == null) return;

        SetMatchingChildrenActive(_shipRoot.transform, IsShipPreviewHiddenChild, false);
        EnsureShipHangarDoorAnimator();
    }

    private void EnsureShipHangarDoorAnimator()
    {
        if (_shipRoot == null) return;

        var door = FindChildByName(_shipRoot.transform, ShipHangarDoorName);
        if (door == null) return;

        if (!door.gameObject.activeSelf)
        {
            door.gameObject.SetActive(true);
        }

        var renderers = door.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (renderer == null) continue;

            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
        }

        if (door.GetComponent<MenuEnvironmentHangarDoorAnimator>() != null) return;

        door.gameObject
            .AddComponent<MenuEnvironmentHangarDoorAnimator>()
            .Configure(ShipHangarDoorOpenLocalOffset, ShipHangarDoorOpenDelaySeconds, ShipHangarDoorOpenDurationSeconds);
    }

    private void ApplyShipWakeTuning()
    {
        if (_shipRoot == null) return;

        var wakeRoot = FindChildByName(_shipRoot.transform, ShipWakeRootName);
        if (wakeRoot == null)
        {
            Debug.LogWarning("[NOVR] Native menu environment could not find preview ship wake effect root.");
            return;
        }

        if (!wakeRoot.gameObject.activeSelf)
        {
            wakeRoot.gameObject.SetActive(true);
        }

        var flow = -_shipRoot.transform.forward * ShipWakeFlowSpeed;
        var tunedParticles = 0;
        var particleSystems = wakeRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (var index = 0; index < particleSystems.Length; index++)
        {
            var particleSystem = particleSystems[index];
            if (particleSystem == null) continue;

            if (!particleSystem.gameObject.activeSelf)
            {
                particleSystem.gameObject.SetActive(true);
            }

            ApplyShipWakeFlow(particleSystem, flow);
            ApplyShipWakeParticleTuning(particleSystem);
            particleSystem.Clear(true);
            particleSystem.Play(true);
            tunedParticles++;
        }

        Debug.Log($"[NOVR] Native menu environment tuned preview ship wake particles: count={tunedParticles}, flow={flow}.");
    }

    private static void ApplyShipWakeFlow(ParticleSystem particleSystem, Vector3 flow)
    {
        var inherit = particleSystem.inheritVelocity;
        inherit.enabled = false;

        var velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(flow.x);
        velocity.y = new ParticleSystem.MinMaxCurve(flow.y);
        velocity.z = new ParticleSystem.MinMaxCurve(flow.z);

        var limit = particleSystem.limitVelocityOverLifetime;
        limit.enabled = false;
    }

    private static void ApplyShipWakeParticleTuning(ParticleSystem particleSystem)
    {
        var name = particleSystem.gameObject.name;
        var main = particleSystem.main;
        var emission = particleSystem.emission;

        if (string.Equals(name, "wakefoam", StringComparison.Ordinal))
        {
            emission.rateOverTimeMultiplier = 18f;
            main.startSizeMultiplier = 170f;
            main.startLifetimeMultiplier = 255f;
            main.maxParticles = 900;
        }
        else if (string.Equals(name, "bowFoam", StringComparison.Ordinal))
        {
            emission.rateOverTimeMultiplier = 0.5f;
            main.startSizeMultiplier = 520f;
            main.startLifetimeMultiplier = 8f;
            main.maxParticles = 250;
        }
        else if (string.Equals(name, "thrusterSprayR", StringComparison.Ordinal))
        {
            emission.rateOverTimeMultiplier = 1f;
            main.startSizeMultiplier = 4f;
            main.startSpeedMultiplier = 28f;
            main.startLifetimeMultiplier = 0.55f;
            main.maxParticles = 35;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.35f));
        }
        else if (string.Equals(name, "thrusterMistR", StringComparison.Ordinal))
        {
            emission.rateOverTimeMultiplier = 1f;
            main.startSizeMultiplier = 7f;
            main.startSpeedMultiplier = 24f;
            main.startLifetimeMultiplier = 1.2f;
            main.maxParticles = 45;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.28f));
        }
    }

    private void ApplyEnvironmentRenderSettings()
    {
        if (_environmentRenderSettingsApplied) return;

        CaptureRenderSettings();
        var skyboxMaterial = FindSkyboxMaterial();
        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
        }
        else
        {
            Debug.LogWarning("[NOVR] Native menu environment could not find a game skybox material.");
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.58f, 0.68f, 0.78f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = MenuEnvironmentFogColor;
        RenderSettings.fogDensity = MenuEnvironmentFogDensity;
        _environmentRenderSettingsApplied = true;
        ApplyEnvironmentPostProcessing();
        Debug.Log(skyboxMaterial != null
            ? "[NOVR] Native menu environment applied skybox render settings " +
              $"using '{skyboxMaterial.name}' with shader '{skyboxMaterial.shader.name}' and fog density {MenuEnvironmentFogDensity}."
            : $"[NOVR] Native menu environment applied render settings without skybox and fog density {MenuEnvironmentFogDensity}.");
    }

    private void RestoreEnvironmentRenderSettings()
    {
        if (!_environmentRenderSettingsApplied && _postProcessingVolume == null && !_renderSettingsCaptured)
        {
            return;
        }

        RemoveEnvironmentPostProcessing();
        if (_renderSettingsCaptured)
        {
            RestoreRenderSettings(_originalRenderSettings);
            _renderSettingsCaptured = false;
        }

        _environmentRenderSettingsApplied = false;
        Debug.Log("[NOVR] Native menu environment restored scene render settings.");
    }

    private void CaptureRenderSettings()
    {
        if (_renderSettingsCaptured) return;

        _originalRenderSettings = new RenderSettingsSnapshot
        {
            Skybox = RenderSettings.skybox,
            AmbientMode = RenderSettings.ambientMode,
            AmbientLight = RenderSettings.ambientLight,
            AmbientSkyColor = RenderSettings.ambientSkyColor,
            AmbientEquatorColor = RenderSettings.ambientEquatorColor,
            AmbientGroundColor = RenderSettings.ambientGroundColor,
            AmbientIntensity = RenderSettings.ambientIntensity,
            Fog = RenderSettings.fog,
            FogMode = RenderSettings.fogMode,
            FogColor = RenderSettings.fogColor,
            FogDensity = RenderSettings.fogDensity,
            FogStartDistance = RenderSettings.fogStartDistance,
            FogEndDistance = RenderSettings.fogEndDistance,
            ReflectionIntensity = RenderSettings.reflectionIntensity,
        };
        _renderSettingsCaptured = true;
    }

    private static void RestoreRenderSettings(RenderSettingsSnapshot snapshot)
    {
        RenderSettings.skybox = snapshot.Skybox;
        RenderSettings.ambientMode = snapshot.AmbientMode;
        RenderSettings.ambientLight = snapshot.AmbientLight;
        RenderSettings.ambientSkyColor = snapshot.AmbientSkyColor;
        RenderSettings.ambientEquatorColor = snapshot.AmbientEquatorColor;
        RenderSettings.ambientGroundColor = snapshot.AmbientGroundColor;
        RenderSettings.ambientIntensity = snapshot.AmbientIntensity;
        RenderSettings.fog = snapshot.Fog;
        RenderSettings.fogMode = snapshot.FogMode;
        RenderSettings.fogColor = snapshot.FogColor;
        RenderSettings.fogDensity = snapshot.FogDensity;
        RenderSettings.fogStartDistance = snapshot.FogStartDistance;
        RenderSettings.fogEndDistance = snapshot.FogEndDistance;
        RenderSettings.reflectionIntensity = snapshot.ReflectionIntensity;
    }

    private void ApplyEnvironmentPostProcessing()
    {
        if (_postProcessingVolume != null) return;

        var go = new GameObject("NOVR Menu Post Processing");
        if (_environmentScene.IsValid() && _environmentScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, _environmentScene);
        }

        if (_environmentRoot != null)
        {
            go.transform.SetParent(_environmentRoot.transform, false);
        }

        _postProcessingVolume = go.AddComponent<Volume>();
        _postProcessingVolume.isGlobal      = true;
        _postProcessingVolume.blendDistance = 0.5f;
        _postProcessingVolume.weight        = 1.0f;
        _postProcessingVolume.priority      = 1.0f; // sit above any leftover scene volumes

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);

        var bloom = profile.Add<Bloom>();
        bloom.threshold.Override(0f);
        bloom.intensity.Override(1.0f);
        bloom.scatter.Override(0.5f);
        bloom.highQualityFiltering.Override(true);

        var vignette = profile.Add<Vignette>();
        vignette.color.Override(Color.black);
        vignette.center.Override(new Vector2(0.5f, 0.5f));
        vignette.intensity.Override(0.4f);
        vignette.smoothness.Override(0.5f);

        _postProcessingVolume.sharedProfile = profile;

        Debug.Log("[NOVR] Native menu environment created post-processing volume.");
    }

    private void RemoveEnvironmentPostProcessing()
    {
        if (_postProcessingVolume == null) return;

        if (_postProcessingVolume.sharedProfile != null)
        {
            Object.Destroy(_postProcessingVolume.sharedProfile);
        }

        Object.Destroy(_postProcessingVolume.gameObject);
        _postProcessingVolume = null;
        Debug.Log("[NOVR] Native menu environment removed post-processing volume.");
    }

    private void ApplyEnvironmentCameraSettings()
    {
        var camera = FindMenuCamera();
        if (camera == null) return;

        if (_environmentCameraSettingsCaptured && _environmentCamera != camera)
        {
            RestoreEnvironmentCameraSettings();
        }

        if (!_environmentCameraSettingsCaptured)
        {
            _environmentCamera = camera;
            _originalEnvironmentCameraClearFlags = camera.clearFlags;
            _originalEnvironmentCameraBackgroundColor = camera.backgroundColor;
            _originalEnvironmentCameraFarClipPlane = camera.farClipPlane;
            _originalEnvironmentCameraCullingMask = camera.cullingMask;
            var capturedCameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            _originalEnvironmentCameraRenderPostProcessing = capturedCameraData == null || capturedCameraData.renderPostProcessing;
            _environmentCameraSettingsCaptured = true;
        }

        var changed = false;
        if (camera.clearFlags != CameraClearFlags.Skybox)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            changed = true;
        }

        if (Mathf.Abs(camera.farClipPlane - EnvironmentCameraFarClipPlane) > 0.1f)
        {
            camera.farClipPlane = EnvironmentCameraFarClipPlane;
            changed = true;
        }

        if (camera.cullingMask != ~0)
        {
            camera.cullingMask = ~0;
            changed = true;
        }

        var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null && !cameraData.renderPostProcessing)
        {
            cameraData.renderPostProcessing = true;
            changed = true;
        }

        if (changed)
        {
            Debug.Log($"[NOVR] Native menu environment configured '{camera.name}' for skybox rendering.");
        }
    }

    private void RestoreEnvironmentCameraSettings()
    {
        if (!_environmentCameraSettingsCaptured) return;

        if (_environmentCamera != null)
        {
            _environmentCamera.clearFlags = _originalEnvironmentCameraClearFlags;
            _environmentCamera.backgroundColor = _originalEnvironmentCameraBackgroundColor;
            _environmentCamera.farClipPlane = _originalEnvironmentCameraFarClipPlane;
            _environmentCamera.cullingMask = _originalEnvironmentCameraCullingMask;
            var cameraData = _environmentCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = _originalEnvironmentCameraRenderPostProcessing;
            }

            Debug.Log($"[NOVR] Native menu environment restored '{_environmentCamera.name}' camera settings.");
        }

        _environmentCamera = null;
        _environmentCameraSettingsCaptured = false;
    }

    private static Camera? FindMenuCamera()
    {
        var cameras = Resources.FindObjectsOfTypeAll<Camera>();
        Camera? inactiveMatch = null;

        for (var index = 0; index < cameras.Length; index++)
        {
            var camera = cameras[index];
            if (camera == null || !string.Equals(camera.name, MenuCameraName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (camera.isActiveAndEnabled)
            {
                return camera;
            }

            inactiveMatch ??= camera;
        }

        return inactiveMatch;
    }

    private static Material? FindSkyboxMaterial()
    {
        var defaultSkybox = FindMaterialByNormalizedName(DefaultSkyboxMaterialName);
        if (defaultSkybox != null)
        {
            return defaultSkybox;
        }

        var materials = Resources.FindObjectsOfTypeAll<Material>();
        Material? skyboxCandidate = null;

        for (var index = 0; index < materials.Length; index++)
        {
            var material = materials[index];
            if (material == null) continue;

            var normalizedName = NormalizeUnityObjectName(material.name);
            if (string.Equals(normalizedName, "SkyboxBlack", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(normalizedName, "SkyboxCustom", StringComparison.OrdinalIgnoreCase))
            {
                return material;
            }

            if (skyboxCandidate == null && ContainsIgnoreCase(normalizedName, "skybox"))
            {
                skyboxCandidate = material;
            }
        }

        return skyboxCandidate;
    }

    private static Material? FindMaterialByNormalizedName(string materialName)
    {
        var materials = Resources.FindObjectsOfTypeAll<Material>();
        for (var index = 0; index < materials.Length; index++)
        {
            var material = materials[index];
            if (material == null) continue;

            if (string.Equals(NormalizeUnityObjectName(material.name), materialName, StringComparison.OrdinalIgnoreCase))
            {
                return material;
            }
        }

        return null;
    }

    private static string NormalizeUnityObjectName(string name)
    {
        return name.EndsWith(" (Instance)", StringComparison.Ordinal)
            ? name.Substring(0, name.Length - " (Instance)".Length)
            : name;
    }

    private void ApplyAircraftVisualOverrides()
    {
        if (_previewUnit == null) return;

        SetMatchingChildrenActive(_previewUnit.transform, IsAircraftPreviewHiddenChild, false);
    }

    private static bool IsShipPreviewHiddenChild(Transform transform)
    {
        return HiddenShipPreviewChildNames.Contains(transform.name) ||
               ContainsIgnoreCase(transform.name, "lod");
    }

    private static bool IsAircraftPreviewHiddenChild(Transform transform)
    {
        if (string.Equals(transform.name, "pilotRenderer", StringComparison.OrdinalIgnoreCase))
        {
            var parentName = transform.parent != null ? transform.parent.name : string.Empty;
            return string.Equals(parentName, "pilot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(parentName, "gunner", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(transform.name, "UILogicHolder", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "contactSparks", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "helmetCamPoint (1)", StringComparison.OrdinalIgnoreCase) ||
               ContainsIgnoreCase(transform.name, "lod") ||
               string.Equals(transform.name, "navlight_L", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "navlight_R", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "navlight_effects_L", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "navlight_effects_R", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "spotlight_L", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "spotlight_R", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "joystick", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "collective", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(transform.name, "ladder", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetMatchingChildrenActive(Transform root, Func<Transform, bool> predicate, bool active)
    {
        for (var index = 0; index < root.childCount; index++)
        {
            var child = root.GetChild(index);
            SetMatchingChildrenActive(child, predicate, active);

            if (predicate(child) && child.gameObject.activeSelf != active)
            {
                child.gameObject.SetActive(active);
            }
        }
    }

    private static Transform? FindChildByName(Transform root, string objectName)
    {
        if (string.Equals(root.name, objectName, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (var index = 0; index < root.childCount; index++)
        {
            var result = FindChildByName(root.GetChild(index), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Vector3 GetShipRootLocalPosition()
    {
        return PreviewLocalOffset + ShipHangarTargetAnchor - Quaternion.Euler(ShipEuler) * (ShipHangarSourceAnchor * ShipScale);
    }

    private static Vector3 TransformShipSourcePoint(Vector3 sourcePoint)
    {
        return GetShipRootLocalPosition() + Quaternion.Euler(ShipEuler) * (sourcePoint * ShipScale);
    }

    private static bool ContainsIgnoreCase(string text, string value)
    {
        return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int GetTransformDepth(Transform transform)
    {
        var depth = 0;
        var current = transform.parent;
        while (current != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private static string GetTransformPath(Transform transform)
    {
        var path = transform.name;
        var parent = transform.parent;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }

        return path;
    }

    private void SetVisible(bool visible)
    {
        if (_environmentRoot != null && _environmentRoot.activeSelf != visible)
        {
            _environmentRoot.SetActive(visible);
        }
    }
}
