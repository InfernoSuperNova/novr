using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace UnityEngine.XR.OpenXR.Features
{
    /// <summary>
    /// Provides comprehensive OpenXR API layer support for debugging and development.
    /// This feature manages the runtime configuration of OpenXR API layers, handles platform environment
    /// setup, integrates with debug utilities, and invokes API layer support interface objects.
    /// </summary>
    /// <remarks>
    /// Enable this feature in your OpenXR settings to activate API layer support in your application.
    /// The ApiLayersFeature manages the lifecycle of API layers, configuring them before the OpenXR instance
    /// is created and cleaning them up on instance destruction. It also provides a registration system
    /// for ISupport implementations that need to perform custom setup and teardown operations.
    /// API layers execute in the order they appear in the collection.
    /// </remarks>
    /// <example>
    /// <para>
    /// This example demonstrates how to programmatically access and configure API layers at runtime:
    /// <c>
    /// OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
    /// ApiLayersFeature apiLayersFeature = settings.GetFeature&lt;ApiLayersFeature&gt;();
    /// if (apiLayersFeature != null &amp;&amp; apiLayersFeature.enabled)
    /// {
    ///     // Enable core validation layer for x64 architecture
    ///     apiLayersFeature.apiLayers.SetEnabled("XR_APILAYER_LUNARG_core_validation", Architecture.X64, true);
    ///     Debug.Log($"API layers configured: {apiLayersFeature.apiLayers.collection.Count} layers available");
    /// }
    /// </c>
    /// </para>
    /// </example>
    /// <seealso cref="ApiLayers"/>
    /// <seealso cref="ApiLayers.ISupport"/>
    /// <seealso cref="DebugUtilsFeature"/>
#if UNITY_EDITOR
    [OpenXRFeature(
        UiName = "API Layers",
        Desc = "Enables support for OpenXR API Layers and provides UI for managing them.",
        DocumentationLink = Constants.k_DocumentationManualURL + "features/api-layers/api-layers-api.html",
        Company = "Unity",
        Version = "1.0.0",
        OpenxrExtensionStrings = "",
        BuildTargetGroups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone },
        FeatureId = featureId
    )]
#endif
    public class ApiLayersFeature : OpenXRFeature
    {
        /// <summary>
        /// A unique identifier for this feature.
        /// </summary>
        public const string featureId = "com.unity.openxr.feature.apilayers";
        static List<ApiLayers.ISupport> s_ApiLayersSupport = new List<ApiLayers.ISupport>();

        /// <summary>
        /// Gets the API layers instance for this feature configuration.
        /// </summary>
        public ApiLayers apiLayers => m_ApiLayers;

        [SerializeField]
        ApiLayers m_ApiLayers = new ApiLayers();


#if UNITY_EDITOR
        /// <summary>
        /// Caches validation rules for each build target group requested by <see cref="GetValidationChecks="/>.
        /// </summary>
        Dictionary<BuildTargetGroup, ValidationRule[]> m_ValidationRules = new Dictionary<BuildTargetGroup, ValidationRule[]>();
#endif


        /// <summary>
        /// Registration method for support objects.
        /// </summary>
        /// <param name="support">The support object to add.</param>
        public static void AddSupport(ApiLayers.ISupport support)
        {
            if (!s_ApiLayersSupport.Contains(support))
                s_ApiLayersSupport.Add(support);
        }

        /// <summary>
        /// De-registration method for support objects.
        /// </summary>
        /// <param name="support">The support object to remove.</param>
        public static void RemoveSupport(ApiLayers.ISupport support)
        {
            s_ApiLayersSupport.Remove(support);
        }

        /// <summary>
        /// Hooks into the OpenXR instance creation process to configure API layers and the environment.
        /// This method is called before the OpenXR instance is created.
        /// </summary>
        /// <param name="hookGetInstanceProcAddr">The original GetInstanceProcAddr function pointer.</param>
        /// <returns>The original function pointer.</returns>
        protected internal override IntPtr HookGetInstanceProcAddr(IntPtr hookGetInstanceProcAddr)
        {
            var enabledApiLayers = GetEnabledApiLayers();
            if (enabledApiLayers.Length > 0)
            {
#if UNITY_EDITOR
                // In the editor, we need to hook into play mode state changes to process logs when exiting play mode.
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
                // Run setup for all added support classes
                foreach (var support in s_ApiLayersSupport)
                    support.Setup(hookGetInstanceProcAddr);

                SetEnabledApiLayers(enabledApiLayers, enabledApiLayers.Length);
            }

            return base.HookGetInstanceProcAddr(hookGetInstanceProcAddr);
        }

        /// <summary>
        /// Called after the OpenXR instance is successfully created.
        /// </summary>
        /// <param name="xrInstance">The handle to the created OpenXR instance.</param>
        /// <returns>True if initialization was successful, false otherwise.</returns>
        protected internal override bool OnInstanceCreate(ulong xrInstance)
        {
            return base.OnInstanceCreate(xrInstance);
        }

        /// <summary>
        /// Called when the OpenXR instance is being destroyed.
        /// This method triggers the teardown process for all support classes.
        /// </summary>
        /// <param name="xrInstance">The handle of the OpenXR instance being destroyed.</param>
        protected internal override void OnInstanceDestroy(ulong xrInstance)
        {
            base.OnInstanceDestroy(xrInstance);

            foreach (var support in s_ApiLayersSupport)
                support.Teardown(xrInstance);
        }

        /// <summary>
        /// Returns the names of all enabled API layers
        /// </summary>
        string[] GetEnabledApiLayers()
        {
            var targetArchitecture = RuntimeInformation.ProcessArchitecture;
            string[] enabledLayers = apiLayers.collection
                                    .Where(layer => layer.isEnabled && !string.IsNullOrEmpty(layer.name) && layer.libraryArchitecture == targetArchitecture)
                                    .Select(layer => layer.name)
                                    .ToArray();
            return enabledLayers;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Called when the editor's play mode state changes. Used to trigger log processing when exiting play mode.
        /// </summary>
        /// <param name="state">The new play mode state.</param>
        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                s_ApiLayersSupport.Clear();
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
        }
#endif

        /// <summary>
        /// P/Invoke declaration for the native function that configures the enabled API layers.
        /// </summary>
        [DllImport(InternalConstants.openXRLibrary, EntryPoint = "NativeConfig_SetEnabledApiLayers")]
        static extern void SetEnabledApiLayers([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] apiLayerNames, int arraySize);

#if UNITY_EDITOR
        protected internal override void GetValidationChecks(List<ValidationRule> rules, BuildTargetGroup targetGroup)
        {
            if (!m_ValidationRules.ContainsKey(targetGroup))
                m_ValidationRules.Add(targetGroup, CreateValidationRules(targetGroup));

            rules.AddRange(m_ValidationRules[targetGroup]);
        }

        private ValidationRule[] CreateValidationRules(BuildTargetGroup targetGroup) =>

            new ValidationRule[]
            {
                    new ValidationRule(this)
                    {
                        message = $"The `{ApiLayers.CoreValidationLogSupport.k_LayerName}` layer is enabled and will negatively affect performance. It is recommended to disable this layer for production builds.",
                        checkPredicate = () =>
                        {
                            if (EditorUserBuildSettings.development)
                                return true;

                            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                            if (settings == null)
                                return false;

                            var apiLayersFeature = settings.GetFeature<ApiLayersFeature>();
                            if (apiLayersFeature == null)
                                return false;

                            if (apiLayersFeature.enabled)
                            {
                                ApiLayers.IPlatformSupport platformSupport = null;

                                switch (targetGroup)
                                {
                                    case BuildTargetGroup.Standalone:
                                        platformSupport = new ApiLayers.WindowsPlatformSupport();
                                        break;
                                    case BuildTargetGroup.Android:
                                        platformSupport = new ApiLayers.AndroidPlatformSupport();
                                        break;
                                }

                                if (platformSupport == null)
                                    return true;

                                foreach (var arch in platformSupport.GetSupportedArchitectures())
                                {
                                    if (apiLayersFeature.apiLayers.IsEnabled(ApiLayers.CoreValidationLogSupport.k_LayerName,arch))
                                        return false;
                                }

                            }
                            return true;
                        },
                        error = false,
                        fixItAutomatic = false,
                        fixItMessage = $"Disable the `{ApiLayers.CoreValidationLogSupport.k_LayerName}` layer in the OpenXR API layers settings."
                    },

                    new ValidationRule(this)
                    {
                        message = $"The `{ApiLayers.ApiDumpLogSupport.k_LayerName}` layer is enabled and will negatively affect performance. It is recommended to disable this layer for production builds.",
                        checkPredicate = () =>
                        {
                            if (EditorUserBuildSettings.development)
                                return true;

                            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                            if (settings == null)
                                return false;

                            var apiLayersFeature = settings.GetFeature<ApiLayersFeature>();
                            if (apiLayersFeature == null)
                                return false;

                            if (apiLayersFeature.enabled)
                            {
                                ApiLayers.IPlatformSupport platformSupport = null;

                                switch (targetGroup)
                                {
                                    case BuildTargetGroup.Standalone:
                                        platformSupport = new ApiLayers.WindowsPlatformSupport();
                                        break;
                                    case BuildTargetGroup.Android:
                                        platformSupport = new ApiLayers.AndroidPlatformSupport();
                                        break;
                                }

                                if (platformSupport == null)
                                    return true;

                                foreach (var arch in platformSupport.GetSupportedArchitectures())
                                {
                                    if (apiLayersFeature.apiLayers.IsEnabled(ApiLayers.ApiDumpLogSupport.k_LayerName, arch))
                                        return false;
                                }

                            }
                            return true;
                        },
                        error = false,
                        fixItAutomatic = false,
                        fixItMessage = $"Disable the `{ApiLayers.ApiDumpLogSupport.k_LayerName}` layer in the OpenXR API layers settings."
                    },
            };


        [CustomEditor(typeof(ApiLayersFeature))]
        internal class Editor : UnityEditor.Editor
        {
            const string k_ApiLayersPropertyName = "m_ApiLayers";
            private SerializedProperty m_SerializedApiLayers;

            void OnEnable()
            {
                m_SerializedApiLayers = serializedObject.FindProperty(k_ApiLayersPropertyName);
            }

            /// <summary>
            /// Renders the custom inspector GUI for the Debug Utils Feature.
            /// </summary>
            public override void OnInspectorGUI()
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(m_SerializedApiLayers);
            }
        }
#endif

    }
}
