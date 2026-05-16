#if XR_COMPOSITION_LAYERS
using System;
using Unity.XR.CompositionLayers.Services;
using UnityEngine.XR.OpenXR.CompositionLayers;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace UnityEngine.XR.OpenXR.Features.CompositionLayers
{
    /// <summary>
    /// Enables OpenXR Composition Layers.
    /// </summary>
#if UNITY_EDITOR
    [OpenXRFeature(UiName = FeatureName,
        Desc = "Necessary to use OpenXR Composition Layers.",
        DocumentationLink = Constants.k_DocumentationManualURL + "features/compositionlayers.html",
        Company = "Unity",
        OpenxrExtensionStrings = "XR_KHR_composition_layer_cylinder XR_KHR_composition_layer_equirect XR_KHR_composition_layer_equirect2 XR_KHR_composition_layer_cube XR_KHR_composition_layer_color_scale_bias XR_KHR_android_surface_swapchain",
        Version = "1.0.0",
        BuildTargetGroups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone },
        FeatureId = FeatureId
    )]
#endif
    public class OpenXRCompositionLayersFeature : OpenXRFeature
    {
        public const string FeatureId = "com.unity.openxr.feature.compositionlayers";
        internal const string FeatureName = "Composition Layers Support";

        protected internal override void OnSessionBegin(ulong xrSession)
        {
            InitApplicationLifecycleHook();
            InitOpenXRLayerProvider();
        }

        protected internal override void OnSessionEnd(ulong xrSession)
        {
            DeinitApplicationLifecycleHook();
            DeinitOpenXRLayerProvider();
        }

        static void InitOpenXRLayerProvider()
        {
            if (CompositionLayerManager.Instance != null)
            {
                CompositionLayerManager.Instance.LayerProvider ??= new OpenXRLayerProvider();
            }
        }

        static void DeinitOpenXRLayerProvider()
        {
            if (CompositionLayerManager.Instance?.LayerProvider is OpenXRLayerProvider)
            {
                ((OpenXRLayerProvider)CompositionLayerManager.Instance.LayerProvider).Dispose();
                CompositionLayerManager.Instance.LayerProvider = null;
            }
        }

        static void InitApplicationLifecycleHook()
        {
            ApplicationLifecycleHook.Init(OnPause);
        }

        static void DeinitApplicationLifecycleHook()
        {
            ApplicationLifecycleHook.Deinit();
        }

        static void OnPause(bool isPaused)
        {
            if (isPaused)
            {
                DeinitOpenXRLayerProvider();
            }

            else
            {
                InitOpenXRLayerProvider();
            }
        }

        class ApplicationLifecycleHook : MonoBehaviour
        {
            static event Action<bool> OnPause;
            static ApplicationLifecycleHook Instance;

            public static void Init(Action<bool> OnPauseCallback)
            {
                if (Instance == null)
                {
                    GameObject go = new GameObject(nameof(ApplicationLifecycleHook));
                    Instance = go.AddComponent<ApplicationLifecycleHook>();
                    OnPause = OnPauseCallback;
                    DontDestroyOnLoad(go);
                }
            }

            public static void Deinit()
            {
                if (Instance != null && Instance.gameObject != null)
                {
#if UNITY_EDITOR
                    DestroyImmediate(Instance.gameObject);
#else
                    Destroy(Instance.gameObject);
#endif
                    Instance = null;
                    OnPause = null;
                }
            }

            void OnApplicationPause(bool pauseStatus)
            {
                OnPause?.Invoke(pauseStatus);
            }
        }
    }
}
#endif
