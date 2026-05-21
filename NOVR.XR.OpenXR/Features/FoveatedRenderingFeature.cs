using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR.NativeTypes;

namespace UnityEngine.XR.OpenXR.Features
{
    /// <summary>
    /// This <see cref="OpenXRFeature"/> enables the use of foveated rendering in OpenXR.
    /// </summary>
#if UNITY_EDITOR && UNITY_2023_2_OR_NEWER
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(UiName = "Foveated Rendering",
        BuildTargetGroups = new []{BuildTargetGroup.Standalone, BuildTargetGroup.Android},
        Company = "Unity",
        Desc = "Add foveated rendering.",
        DocumentationLink = Constants.k_DocumentationManualURL + "features/foveatedrendering.html",
        OpenxrExtensionStrings = "XR_UNITY_foveation XR_FB_foveation XR_FB_foveation_configuration XR_FB_swapchain_update_state XR_FB_foveation_vulkan XR_META_foveation_eye_tracked XR_META_vulkan_swapchain_create_info",
        Version = "1",
        Category = UnityEditor.XR.OpenXR.Features.FeatureCategory.Feature,
        FeatureId = featureId)]
#endif
    public class FoveatedRenderingFeature : OpenXRFeature
    {
        /// <summary>
        /// The feature id string. This is used to give the feature a well known id for reference.
        /// </summary>
        public const string featureId = "com.unity.openxr.feature.foveatedrendering";

        /// <summary>
        /// Get whether Vulkan subsampled layout is currently enabled.
        /// </summary>
        public static bool isSubsampledLayoutEnabled { get; private set; }

        [SerializeField]
        bool enableSubsampledLayout;
#if UNITY_EDITOR
        private bool SettingsUseVulkan()
        {
            if (!PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
            {
                GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                if (apis.Length >= 1 && apis[0] == GraphicsDeviceType.Vulkan)
                {
                    return true;
                }
                return false;
            }

            return true;
        }

        protected internal override void GetValidationChecks(List<ValidationRule> rules, BuildTargetGroup targetGroup)
        {
#if UNITY_ANDROID
            rules.Add(new ValidationRule(this)
            {
                message = "Subsampled Layout is only supported on Vulkan graphics API",
                checkPredicate = () =>
                {
                    if (enableSubsampledLayout && !SettingsUseVulkan())
                    {
                        return false;
                    }
                    return true;
                },
                fixIt = () =>
                {
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
                },
                error = true,
                fixItAutomatic = true,
                fixItMessage = "Set Vulkan as Graphics API"
            });
#endif
#if UNITY_2023_2_OR_NEWER
            rules.Add(new ValidationRule(this)
            {
                message = "Only Legacy Foveated Rendering API usage is possible on Built-in Render Pipeline",
                checkPredicate = () =>
                {
                    var currentSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                    return GraphicsSettings.currentRenderPipeline != null || currentSettings.foveatedRenderingApi == OpenXRSettings.BackendFovationApi.Legacy;
                },
                fixIt = () =>
                {
                    var currentSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                    currentSettings.foveatedRenderingApi = OpenXRSettings.BackendFovationApi.Legacy;
                },
                error = true,
                fixItAutomatic = true,
                fixItMessage = "Set Foveated Rendering Method to Foveated Rendering (Legacy)"
            });
#endif

#if UNITY_ANDROID_XR && UNITY_2023_2_OR_NEWER
            // Have a rule that gives the user a warning if they're using the AndroidXR Build Profile.
            rules.Add(new ValidationRule(this)
            {
                message = "Quad Views is not supported for the AndroidXR Build Profile.",
                checkPredicate = () =>
                {
                    var currentSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                    return currentSettings.foveatedRenderingApi != OpenXRSettings.BackendFovationApi.QuadViews;
                },
                fixIt = () =>
                {
                    var currentSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                    currentSettings.foveatedRenderingApi = OpenXRSettings.BackendFovationApi.SRPFoveation;
                },
                error = true,
                fixItAutomatic = true,
                fixItMessage = "Set Foveated Rendering Method to Foveated Rendering (SRP API)"
            });
#endif
            rules.Add(new ValidationRule(this)
            {
                message = "Quad Views is only supported with the \"Single-Pass Instanced \\ Multi-view\" Render Mode.",
                helpText = "Use a different foveation method, switch to \"Single-Pass Instanced \\ Multi-view\" Render Mode, or disable the Foveated Rendering feature.",
                checkPredicate = () =>
                {
                    var currentSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                    if (currentSettings == null) return true;

                    if (currentSettings.foveatedRenderingApi == OpenXRSettings.BackendFovationApi.QuadViews)
                    {
                        return currentSettings.renderMode == OpenXRSettings.RenderMode.SinglePassInstanced;
                    }

                    return true;
                },
                error = true,
                fixIt = () =>
                {
                    var currentSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                    currentSettings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                }
            });

        }

        [CustomEditor(typeof(FoveatedRenderingFeature))]
        internal class FoveatedRenderingFeatureEditor : Editor
        {
            private SerializedProperty subsampledLayout;
            static GUIContent s_SubsampledLayout = EditorGUIUtility.TrTextContent("Subsampled Layout (Vulkan)", "An optimization technique that can improve foveated rendering performance by optimizing eye texture sampling.");

#if UNITY_2023_2_OR_NEWER
            private SerializedProperty foveatedRenderingApi;
            private SerializedObject openXRSettings;
            private BuildTargetGroup selectedBuildSettings;

            private static readonly GUIContent[] k_foveatedRenderingApiOptions = new GUIContent[2]
            {
                new GUIContent("Legacy"),
                new GUIContent("SRP Foveation"),
            };

#if UNITY_6000_5_OR_NEWER
            private static readonly GUIContent[] k_androidfoveatedRenderingApiOptions = new GUIContent[3]
            {
                new GUIContent("Foveated rendering (Legacy API)"),
                new GUIContent("Foveated rendering (SRP API)"),
                new GUIContent("Quad Views"),
            };
#else
            private static readonly GUIContent[] k_androidfoveatedRenderingApiOptions = new GUIContent[2]
            {
                new GUIContent("Foveated rendering (Legacy API)"),
                new GUIContent("Foveated rendering (SRP API)"),
            };
#endif

            private static readonly GUIContent k_foveatedRenderingApiLabel = new GUIContent("Foveated Rendering Method", "Choose the foveated rendering api.");
#endif

            void OnEnable()
            {
                subsampledLayout = serializedObject.FindProperty("enableSubsampledLayout");

#if UNITY_2023_2_OR_NEWER
                selectedBuildSettings = EditorUserBuildSettings.selectedBuildTargetGroup;
                var currentSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
                openXRSettings = new SerializedObject(currentSettings);
                if (openXRSettings != null)
                {
                    foveatedRenderingApi = openXRSettings.FindProperty("m_foveatedRenderingApi");
                }
#endif
            }

            public override void OnInspectorGUI()
            {
#if UNITY_2023_2_OR_NEWER
                if (selectedBuildSettings != EditorUserBuildSettings.selectedBuildTargetGroup)
                {
                    selectedBuildSettings = EditorUserBuildSettings.selectedBuildTargetGroup;
                    openXRSettings = new SerializedObject(OpenXRSettings.GetSettingsForBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
                    if (openXRSettings != null)
                    {
                        foveatedRenderingApi = openXRSettings.FindProperty("m_foveatedRenderingApi");
                    }
                }
                openXRSettings.Update();
#endif

                EditorGUIUtility.labelWidth = 300.0f;
                serializedObject.Update();
                EditorGUILayout.PropertyField(subsampledLayout, s_SubsampledLayout);
#if UNITY_2023_2_OR_NEWER

                var currentSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
                int newFoveatedRenderingApi;
                GUILayout.BeginHorizontal();
                if (EditorUserBuildSettings.selectedBuildTargetGroup == BuildTargetGroup.Android)
                {
                    newFoveatedRenderingApi = EditorGUILayout.Popup(
                        k_foveatedRenderingApiLabel,
                        (int)currentSettings.foveatedRenderingApi,
                        k_androidfoveatedRenderingApiOptions
                    );
                }
                else
                {
                    newFoveatedRenderingApi = EditorGUILayout.Popup(
                        k_foveatedRenderingApiLabel,
                        (int)currentSettings.foveatedRenderingApi,
                        k_foveatedRenderingApiOptions
                    );
                }

                if (newFoveatedRenderingApi != (int)currentSettings.foveatedRenderingApi)
                {
                    currentSettings.foveatedRenderingApi = (OpenXRSettings.BackendFovationApi)newFoveatedRenderingApi;
                }

                GUILayout.EndHorizontal();
                openXRSettings.ApplyModifiedProperties();
#endif
                serializedObject.ApplyModifiedProperties();

                EditorGUIUtility.labelWidth = 0.0f;
            }
        }
#endif

        /// <summary>
        /// Attempts to set whether subsampled layout is enabled.
        /// </summary>
        /// <param name="enableSubsampling">Indicates whether to enable subsampling.</param>
        /// <returns>
        /// Returns <see langword="true"/> if subsampling state was updated
        /// and returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool TrySetSubsampledLayoutEnabled(bool enableSubsampling)
        {
            if (enableSubsampling)
            {
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
                {
                    Debug.LogError("Could not enable subsampling. Subsampled Layout is only supported on Vulkan graphics API.");
                    return false;
                }

                if (!OpenXRRuntime.IsExtensionEnabled("XR_META_vulkan_swapchain_create_info"))
                {
                    Debug.LogError("$Could not enable Vulkan subsampling. OpenXR extension XR_META_vulkan_swapchain_create_info is not available on the current runtime.");
                    return false;
                }
            }

            var wasSuccessful = Internal_Unity_MetaSetSubsampledLayout(enableSubsampling) == XrResult.Success;

            if (wasSuccessful)
            {
                isSubsampledLayoutEnabled = enableSubsampling;
            }

            return wasSuccessful;
        }

        /// <inheritdoc />
        protected internal override bool OnInstanceCreate(ulong instance)
        {
            // If using BiRP, the feature must know not to use the newer API for FSR/FDM
            Internal_Unity_SetUseFoveatedRenderingLegacyMode(GraphicsSettings.defaultRenderPipeline == null);

            TrySetSubsampledLayoutEnabled(enableSubsampledLayout);

            return base.OnInstanceCreate(instance);
        }

        /// <inheritdoc />
        protected internal override IntPtr HookGetInstanceProcAddr(IntPtr func)
        {
            return Internal_Unity_intercept_xrGetInstanceProcAddr(func);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////
        private const string Library = "UnityOpenXR";

        [DllImport(Library, EntryPoint = "UnityFoveation_intercept_xrGetInstanceProcAddr")]
        private static extern IntPtr Internal_Unity_intercept_xrGetInstanceProcAddr(IntPtr func);

        [DllImport(Library, EntryPoint = "UnityFoveation_SetUseFoveatedRenderingLegacyMode")]
        private static extern void Internal_Unity_SetUseFoveatedRenderingLegacyMode([MarshalAs(UnmanagedType.I1)] bool value);

        [DllImport(Library, EntryPoint = "UnityFoveation_GetUseFoveatedRenderingLegacyMode")]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool Internal_Unity_GetUseFoveatedRenderingLegacyMode();

        [DllImport(Library, EntryPoint = "MetaSetSubsampledLayout")]
        private static extern XrResult Internal_Unity_MetaSetSubsampledLayout([MarshalAs(UnmanagedType.U1)] bool enableSubsampling);
    }
}
