using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NOVR.VrUi.Native;

public sealed class NativeMenuEnvironment : MonoBehaviour
{
    private const string EnvironmentRootName = "NOVR Native Menu Environment";
    private const string VisualRootName = "Visuals";
    private const string PreviewUnitName = "AttackHelo1";
    private const string PreviewShipName = "Frigate1";
    private const string NavLightStarburstTextureName = "starburst";
    private const string BackdropWaterName = "BackdropWater";
    private const string WaterUnderLayerName = "waterUnderLayer";
    private const string MenuBackdropWaterName = "Menu Environment BackdropWater";
    private const string CloudPlaneName = "cloudPlane";
    private const string MenuCloudPlaneName = "NOVR Menu Environment Cloud Plane";
    private const string MenuCameraName = "Menu Camera";
    private const string DefaultSkyboxMaterialName = "Default-Skybox";
    private const string EncyclopediaSceneName = "Encyclopedia";
    private const string EncyclopediaScenePath = "Assets/Scenes/Encyclopedia/Encyclopedia.unity";
    private const float EnvironmentCameraFarClipPlane = 80000f;
    private const float MenuEnvironmentFogDensity = 0.00105f;
    private const float MissingAssetRetryIntervalSeconds = 1f;
    private const float AssetWarmupRetryIntervalSeconds = 3f;
    private const float AssetWarmupStartupDelaySeconds = 2f;
    private const float AssetWarmupReadyTimeoutSeconds = 12f;
    private static readonly Vector3 MenuEnvironmentWorldAnchor = new(-23.8808f, 2.774f, -821.3446f);
    private static readonly Vector3 WaterLocalPosition = new(0f, -10f, -9.66f);
    private static readonly Vector3 CloudPlaneLocalPosition = new(0f, 500f, 0f);
    private static readonly Vector3 CloudPlaneLocalScale = new(1f, 10f, 1f);
    private static readonly Color MenuEnvironmentFogColor = new(0.52f, 0.68f, 0.80f, 1f);
    private static readonly Color CloudMaterialColor = new(3.75f, 3.78f, 1.72f, 0.98f);
    private static readonly Color CloudLayerParticleColor = new(1f, 1f, 1f, 0.5f);
    private static readonly Color DistantCloudParticleColor = Color.white;
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
    private GameObject? _waterBackdrop;
    private GameObject? _waterUnderLayer;
    private GameObject? _cloudPlane;
    private Material? _originalSkybox;
    private Color _originalAmbientLight;
    private AmbientMode _originalAmbientMode;
    private bool _originalFog;
    private Color _originalFogColor;
    private float _originalFogDensity;
    private FogMode _originalFogMode;
    private Camera? _environmentCamera;
    private CameraClearFlags _originalEnvironmentCameraClearFlags;
    private Color _originalEnvironmentCameraBackgroundColor;
    private float _originalEnvironmentCameraFarClipPlane;
    private int _originalEnvironmentCameraCullingMask;
    private bool _renderSettingsCaptured;
    private bool _environmentRenderSettingsApplied;
    private bool _environmentCameraSettingsCaptured;
    private bool _spawnAttempted;
    private bool _assetWarmupStarted;
    private bool _assetWarmupComplete;
    private bool _assetWarmupFailed;
    private Scene _warmupScene;
    private bool _warmupSceneLoadedByEnvironment;
    private float _nextAssetWarmupAttemptTime;
    private float _nextMissingAssetRetryTime;
    private static GameObject[]? _resourceGameObjectCache;
    private static int _lastLoggedResourceGameObjectCount = -1;

    public void UpdateEnvironment(Transform menuTransform, bool shouldShow)
    {
        var enabled = shouldShow && ModConfiguration.Instance.EnableNativeMenuEnvironment.Value;
        if (!enabled)
        {
            SetVisible(false);
            RestoreEnvironmentCameraSettings();
            RestoreEnvironmentRenderSettings();
            return;
        }

        EnsureRoot();
        PlaceRoot(menuTransform);
        RefreshMissingAssetCachesIfNeeded();
        EnsureEnvironmentAssetWarmup();
        EnsureGameWaterObjects();
        EnsureCloudPlane();
        ApplyEnvironmentRenderSettings();
        ApplyEnvironmentCameraSettings();
        EnsurePreviewScene();
        SetVisible(true);
        ApplyShipVisualOverrides();
        ApplyAircraftVisualOverrides();
        RestorePreviewEffectObjects();
    }

    public void Hide()
    {
        SetVisible(false);
        RestoreEnvironmentCameraSettings();
        RestoreEnvironmentRenderSettings();
    }

    private void OnDestroy()
    {
        RestoreEnvironmentCameraSettings();
        RestoreEnvironmentRenderSettings();
        UnloadWarmupScene();
    }

    private void EnsureEnvironmentAssetWarmup()
    {
        if (_assetWarmupStarted ||
            _assetWarmupComplete ||
            (HasGameWaterObjects() && _cloudPlane != null))
        {
            return;
        }

        if (_assetWarmupFailed && Time.unscaledTime < _nextAssetWarmupAttemptTime)
        {
            return;
        }

        _assetWarmupFailed = false;
        _assetWarmupStarted = true;
        StartCoroutine(WarmEncyclopediaSceneAssets());
    }

    private IEnumerator WarmEncyclopediaSceneAssets()
    {
        var startTime = Time.unscaledTime;
        while (Time.unscaledTime - startTime < AssetWarmupStartupDelaySeconds)
        {
            yield return null;
        }

        startTime = Time.unscaledTime;
        while (Encyclopedia.i == null && Time.unscaledTime - startTime < AssetWarmupReadyTimeoutSeconds)
        {
            yield return null;
        }

        var scene = FindLoadedScene(EncyclopediaScenePath, EncyclopediaSceneName);
        if (!scene.IsValid())
        {
            Debug.Log($"[NOVR] Native menu environment loading '{EncyclopediaScenePath}' additively to warm environment assets.");

            AsyncOperation? loadOperation;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(EncyclopediaScenePath, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                MarkAssetWarmupFailed($"could not start Encyclopedia scene warmup: {exception}");
                yield break;
            }

            if (loadOperation == null)
            {
                MarkAssetWarmupFailed($"SceneManager could not start loading '{EncyclopediaScenePath}'.");
                yield break;
            }

            yield return loadOperation;

            scene = FindLoadedScene(EncyclopediaScenePath, EncyclopediaSceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                MarkAssetWarmupFailed($"SceneManager finished loading '{EncyclopediaScenePath}', but the scene was not available.");
                yield break;
            }

            _warmupScene = scene;
            _warmupSceneLoadedByEnvironment = true;
            Debug.Log($"[NOVR] Native menu environment loaded warmup scene '{scene.name}' with {scene.rootCount} root objects.");
        }
        else
        {
            Debug.Log($"[NOVR] Native menu environment using already-loaded warmup scene '{scene.name}'.");
        }

        DisableWarmupSceneRoots(scene, _warmupSceneLoadedByEnvironment);
        yield return null;
        yield return new WaitForEndOfFrame();
        _resourceGameObjectCache = null;
        EnsureGameWaterObjects();
        EnsureCloudPlane();

        _assetWarmupComplete = HasGameWaterObjects() && _cloudPlane != null;
        _assetWarmupFailed = !_assetWarmupComplete;
        _assetWarmupStarted = false;
        var loadedWarmupScene = _warmupSceneLoadedByEnvironment;

        if (_assetWarmupComplete && _warmupSceneLoadedByEnvironment)
        {
            UnloadWarmupScene();
        }

        if (_assetWarmupFailed)
        {
            _nextAssetWarmupAttemptTime = Time.unscaledTime + AssetWarmupRetryIntervalSeconds;
        }

        Debug.Log(
            "[NOVR] Native menu environment Encyclopedia scene warmup finished: " +
            $"water={HasGameWaterObjects()}, cloudPlane={_cloudPlane != null}, sceneLoadedByEnvironment={loadedWarmupScene}.");
    }

    private void MarkAssetWarmupFailed(string message)
    {
        _assetWarmupStarted = false;
        _assetWarmupFailed = true;
        _nextAssetWarmupAttemptTime = Time.unscaledTime + AssetWarmupRetryIntervalSeconds;
        Debug.LogWarning($"[NOVR] Native menu environment {message} Retrying.");
    }

    private static Scene FindLoadedScene(string scenePath, string sceneName)
    {
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            if (string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return scene;
            }
        }

        return default;
    }

    private void UnloadWarmupScene()
    {
        if (!_warmupSceneLoadedByEnvironment || !_warmupScene.IsValid() || !_warmupScene.isLoaded) return;

        SceneManager.UnloadSceneAsync(_warmupScene);
        _warmupScene = default;
        _warmupSceneLoadedByEnvironment = false;
    }

    private static void DisableWarmupSceneRoots(Scene scene, bool stripWeatherBehaviours)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        var roots = scene.GetRootGameObjects();
        for (var index = 0; index < roots.Length; index++)
        {
            var root = roots[index];
            if (root != null)
            {
                if (stripWeatherBehaviours)
                {
                    StripCloudPlaneGameplayComponents(root);
                }

                root.SetActive(false);
            }
        }
    }

    private void EnsureRoot()
    {
        if (_environmentRoot != null) return;

        _environmentRoot = new GameObject(EnvironmentRootName);
        DontDestroyOnLoad(_environmentRoot);

        var visualRoot = new GameObject(VisualRootName);
        visualRoot.transform.SetParent(_environmentRoot.transform, false);
        _visualRoot = visualRoot.transform;

        Debug.Log("[NOVR] Native menu environment root created.");
    }

    private void EnsureGameWaterObjects()
    {
        if (_visualRoot == null || HasGameWaterObjects()) return;

        var backdropSource = FindWaterObjectSource(BackdropWaterName);
        if (backdropSource == null) return;

        _waterBackdrop = CloneGameWaterObject(backdropSource, MenuBackdropWaterName, WaterLocalPosition);
        _waterUnderLayer = FindChildByName(_waterBackdrop.transform, WaterUnderLayerName)?.gameObject;

        Debug.Log(
            "[NOVR] Native menu environment cloned game water from " +
            $"'{GetTransformPath(backdropSource.transform)}' underLayer={_waterUnderLayer != null}.");
    }

    private static GameObject? FindWaterObjectSource(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (var index = 0; index < objects.Length; index++)
        {
            var gameObject = objects[index];
            if (gameObject == null ||
                IsUnderNativeMenuEnvironment(gameObject.transform) ||
                !string.Equals(gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(gameObject.scene.name, EncyclopediaSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return gameObject;
            }
        }

        return null;
    }

    private GameObject CloneGameWaterObject(GameObject source, string name, Vector3 localPosition)
    {
        var clone = Object.Instantiate(source);
        clone.name = name;
        clone.SetActive(false);
        clone.transform.SetParent(_visualRoot, false);
        clone.transform.localPosition = localPosition;
        clone.transform.localRotation = source.transform.localRotation;
        clone.transform.localScale = source.transform.localScale;

        PrepareGameWaterObject(clone);
        clone.SetActive(true);
        PrepareGameWaterObject(clone);
        LogWaterObjectState(clone);

        return clone;
    }

    private static void PrepareGameWaterObject(GameObject root)
    {
        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (var index = 0; index < colliders.Length; index++)
        {
            var collider = colliders[index];
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        var cameras = root.GetComponentsInChildren<Camera>(true);
        for (var index = 0; index < cameras.Length; index++)
        {
            var camera = cameras[index];
            if (camera != null)
            {
                camera.enabled = false;
            }
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (renderer == null) continue;

            EnsureUniqueWaterMaterials(renderer);
            renderer.enabled = !IsWaterUnderLayer(renderer.transform);
            renderer.forceRenderingOff = false;
        }

        SyncWaterMaterialOriginOffset(root);
    }

    private static bool IsWaterUnderLayer(Transform transform)
    {
        while (transform != null)
        {
            if (string.Equals(transform.name, WaterUnderLayerName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            transform = transform.parent;
        }

        return false;
    }

    private static void EnsureUniqueWaterMaterials(Renderer renderer)
    {
        var materials = renderer.sharedMaterials;
        var changed = false;

        for (var index = 0; index < materials.Length; index++)
        {
            var material = materials[index];
            if (material == null || material.name.EndsWith(" NOVR", StringComparison.Ordinal)) continue;

            materials[index] = new Material(material)
            {
                name = $"{material.name} NOVR"
            };
            changed = true;
        }

        if (changed)
        {
            renderer.sharedMaterials = materials;
        }
    }

    private static void SyncWaterMaterialOriginOffset(GameObject root)
    {
        var localPosition = root.transform.localPosition;
        var originOffset = new Vector4(-localPosition.x, -localPosition.z, 0f, 0f);

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var renderer = renderers[rendererIndex];
            if (renderer == null) continue;

            var materials = renderer.sharedMaterials;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null || !material.HasProperty("_OriginOffset")) continue;

                material.SetVector("_OriginOffset", originOffset);
            }
        }
    }

    private static void LogWaterObjectState(GameObject root)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("[NOVR] Native menu environment water clone state:");

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var renderer = renderers[rendererIndex];
            if (renderer == null) continue;

            builder
                .AppendLine()
                .Append("  renderer=")
                .Append(GetTransformPath(renderer.transform))
                .Append(" layer=")
                .Append(renderer.gameObject.layer)
                .Append(" enabled=")
                .Append(renderer.enabled);

            var materials = renderer.sharedMaterials;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null) continue;

                builder
                    .AppendLine()
                    .Append("    material=")
                    .Append(material.name)
                    .Append(" shader=")
                    .Append(material.shader != null ? material.shader.name : "null")
                    .Append(" queue=")
                    .Append(material.renderQueue);
            }
        }

        Debug.Log(builder.ToString());
    }

    private bool HasGameWaterObjects()
    {
        return _waterBackdrop != null;
    }

    private void EnsureCloudPlane()
    {
        if (_visualRoot == null || _cloudPlane != null) return;

        var cloudSource = FindCloudPlaneSource();
        if (cloudSource == null) return;

        _cloudPlane = Object.Instantiate(cloudSource);
        _cloudPlane.name = MenuCloudPlaneName;
        _cloudPlane.SetActive(false);
        _cloudPlane.transform.SetParent(_visualRoot, false);
        _cloudPlane.transform.localPosition = CloudPlaneLocalPosition;
        _cloudPlane.transform.localRotation = Quaternion.identity;
        _cloudPlane.transform.localScale = CloudPlaneLocalScale;

        PrepareCloudPlane(_cloudPlane);
        _cloudPlane.AddComponent<MenuEnvironmentCloudWind>();
        _cloudPlane.SetActive(true);
        PrepareCloudPlane(_cloudPlane);

        Debug.Log($"[NOVR] Native menu environment cloned cloud plane from '{GetTransformPath(cloudSource.transform)}'.");
    }

    private static GameObject? FindCloudPlaneSource()
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject? fallback = null;

        for (var index = 0; index < objects.Length; index++)
        {
            var gameObject = objects[index];
            if (gameObject == null ||
                !string.Equals(gameObject.name, CloudPlaneName, StringComparison.OrdinalIgnoreCase) ||
                IsUnderNativeMenuEnvironment(gameObject.transform))
            {
                continue;
            }

            if (string.Equals(gameObject.scene.name, "Encyclopedia", StringComparison.OrdinalIgnoreCase))
            {
                return gameObject;
            }

            fallback ??= gameObject;
        }

        return fallback ?? FindResourceGameObjectByName(CloudPlaneName);
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
            if (renderer != null)
            {
                renderer.enabled = true;
            }
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
               ContainsCloudToken(material.shader.name);
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
        RestorePreviewEffectObjects();

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

    private void RestorePreviewEffectObjects()
    {
        if (_previewUnit == null) return;

        RestorePreviewEffectObject(_previewUnit, "navlight_effects_L");
        RestorePreviewEffectObject(_previewUnit, "navlight_effects_R");
        HideNavLightStarburstRenderers(_previewUnit);
    }

    private void ApplyShipVisualOverrides()
    {
        if (_shipRoot == null) return;

        SetMatchingChildrenActive(_shipRoot.transform, IsShipPreviewHiddenChild, false);
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
        Debug.Log(skyboxMaterial != null
            ? "[NOVR] Native menu environment applied skybox render settings " +
              $"using '{skyboxMaterial.name}' with shader '{skyboxMaterial.shader.name}' and fog density {MenuEnvironmentFogDensity}."
            : $"[NOVR] Native menu environment applied render settings without skybox and fog density {MenuEnvironmentFogDensity}.");
    }

    private void RestoreEnvironmentRenderSettings()
    {
        if (!_environmentRenderSettingsApplied || !_renderSettingsCaptured) return;

        RenderSettings.skybox = _originalSkybox;
        RenderSettings.ambientMode = _originalAmbientMode;
        RenderSettings.ambientLight = _originalAmbientLight;
        RenderSettings.fog = _originalFog;
        RenderSettings.fogColor = _originalFogColor;
        RenderSettings.fogDensity = _originalFogDensity;
        RenderSettings.fogMode = _originalFogMode;
        _environmentRenderSettingsApplied = false;
        Debug.Log("[NOVR] Native menu environment restored skybox render settings.");
    }

    private void CaptureRenderSettings()
    {
        if (_renderSettingsCaptured) return;

        _originalSkybox = RenderSettings.skybox;
        _originalAmbientMode = RenderSettings.ambientMode;
        _originalAmbientLight = RenderSettings.ambientLight;
        _originalFog = RenderSettings.fog;
        _originalFogColor = RenderSettings.fogColor;
        _originalFogDensity = RenderSettings.fogDensity;
        _originalFogMode = RenderSettings.fogMode;
        _renderSettingsCaptured = true;
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

    private void RefreshMissingAssetCachesIfNeeded()
    {
        if (HasGameWaterObjects() && _cloudPlane != null) return;
        if (Time.unscaledTime < _nextMissingAssetRetryTime) return;

        _nextMissingAssetRetryTime = Time.unscaledTime + MissingAssetRetryIntervalSeconds;
        _resourceGameObjectCache = null;
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

    private static GameObject? FindResourceGameObjectByName(string gameObjectName)
    {
        var resourceGameObjects = GetResourceGameObjectCache();
        for (var index = 0; index < resourceGameObjects.Length; index++)
        {
            var gameObject = resourceGameObjects[index];
            if (gameObject == null) continue;

            if (string.Equals(gameObject.name, gameObjectName, StringComparison.OrdinalIgnoreCase))
            {
                return gameObject;
            }
        }

        return null;
    }

    private static GameObject[] GetResourceGameObjectCache()
    {
        if (_resourceGameObjectCache != null) return _resourceGameObjectCache;

        _resourceGameObjectCache = Resources.LoadAll<GameObject>(string.Empty);
        if (_resourceGameObjectCache.Length != _lastLoggedResourceGameObjectCount)
        {
            _lastLoggedResourceGameObjectCount = _resourceGameObjectCache.Length;
            Debug.Log($"[NOVR] Native menu environment scanned {_resourceGameObjectCache.Length} resource game objects.");
        }

        return _resourceGameObjectCache;
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
        return ContainsIgnoreCase(transform.name, "hangarDoor") ||
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

    private static void RestorePreviewEffectObject(GameObject root, string objectName)
    {
        var target = FindChildByName(root.transform, objectName);
        if (target == null)
        {
            Debug.LogWarning($"[NOVR] Native menu environment could not find preview effect object '{objectName}'.");
            return;
        }

        var changed = false;
        if (!target.gameObject.activeSelf)
        {
            target.gameObject.SetActive(true);
            changed = true;
        }

        var lights = target.GetComponentsInChildren<Light>(true);
        for (var index = 0; index < lights.Length; index++)
        {
            var light = lights[index];
            if (light == null) continue;

            if (!light.gameObject.activeSelf)
            {
                light.gameObject.SetActive(true);
                changed = true;
            }

            if (!light.enabled)
            {
                light.enabled = true;
                changed = true;
            }

            if (light.cullingMask != ~0)
            {
                light.cullingMask = ~0;
                changed = true;
            }
        }

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (renderer == null) continue;
            if (RendererUsesMainTexture(renderer, NavLightStarburstTextureName)) continue;

            if (!renderer.gameObject.activeSelf)
            {
                renderer.gameObject.SetActive(true);
                changed = true;
            }

            if (!renderer.enabled)
            {
                renderer.enabled = true;
                changed = true;
            }
        }

        var particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
        for (var index = 0; index < particleSystems.Length; index++)
        {
            var particleSystem = particleSystems[index];
            if (particleSystem == null) continue;

            if (!particleSystem.gameObject.activeSelf)
            {
                particleSystem.gameObject.SetActive(true);
                changed = true;
            }

            if (!particleSystem.isPlaying)
            {
                particleSystem.Play(true);
                changed = true;
            }
        }

        if (changed)
        {
            Debug.Log($"[NOVR] Native menu environment restored preview effect object '{objectName}'.");
        }
    }

    private static void HideNavLightStarburstRenderers(GameObject root)
    {
        HideNavLightStarburstRenderers(root, "navlight_effects_L");
        HideNavLightStarburstRenderers(root, "navlight_effects_R");
    }

    private static void HideNavLightStarburstRenderers(GameObject root, string effectRootName)
    {
        var effectRoot = FindChildByName(root.transform, effectRootName);
        if (effectRoot == null) return;

        var renderers = effectRoot.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (renderer == null || !RendererUsesMainTexture(renderer, NavLightStarburstTextureName)) continue;

            if (renderer.enabled)
            {
                renderer.enabled = false;
                Debug.Log($"[NOVR] Native menu environment disabled navlight starburst renderer '{GetTransformPath(renderer.transform)}'.");
            }
        }
    }

    private static bool RendererUsesMainTexture(Renderer renderer, string textureName)
    {
        var materials = renderer.sharedMaterials;
        if (materials == null) return false;

        for (var index = 0; index < materials.Length; index++)
        {
            var material = materials[index];
            if (material == null || material.mainTexture == null) continue;

            if (string.Equals(material.mainTexture.name, textureName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private static bool IsUnderNativeMenuEnvironment(Transform transform)
    {
        var current = transform;
        while (current != null)
        {
            if (string.Equals(current.name, EnvironmentRootName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
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
