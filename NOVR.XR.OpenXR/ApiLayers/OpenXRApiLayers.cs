using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.XR.OpenXR
{
    /// <summary>
    /// Manages OpenXR API layers settings and operations within the Unity Editor.
    /// This class handles the importing, removing, reordering, and configuration of API layers
    /// for use in OpenXR applications, including all associated file operations.
    /// </summary>
    /// <remarks>
    /// The `ApiLayers` class provides comprehensive management of OpenXR API layers in your Unity project.
    /// API layers are middleware components that intercept OpenXR function calls to provide debugging,
    /// validation, or performance analysis capabilities. Use this class to programmatically configure
    /// which layers are active for your application. In the Unity Editor, you can import layers from
    /// JSON manifest files, enable or disable them, and control their order of execution.
    /// </remarks>
    /// <example>
    /// <para>
    /// This example demonstrates how to check and enable an API layer programmatically:
    /// <c>
    /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
    /// if (apiLayersFeature != null &amp;&amp; !apiLayersFeature.apiLayers.IsEnabled("XR_APILAYER_LUNARG_core_validation"))
    /// {
    ///     apiLayersFeature.apiLayers.SetEnabled("XR_APILAYER_LUNARG_core_validation", Architecture.X64, true);
    ///     Debug.Log("Core validation layer enabled");
    /// }
    /// </c>
    /// </para>
    /// </example>
    /// <seealso cref="ApiLayer"/>
    /// <seealso cref="Features.ApiLayersFeature"/>
    /// <seealso cref="ISupport"/>
    [Serializable]
    public partial class ApiLayers
    {
        internal ApiLayers() { }

        internal const string k_OpenXRApiMajorVersionFallback = "1";

        // File and directory constants
        internal const string k_JsonExt = ".json";
        internal const string k_LibraryPathPrefix = "./";
        internal const string k_EditorXrDir = "XR";
        internal const string k_EditorApiLayersDir = "APILayers~";
        internal const string k_LogPrefix = "[OpenXR API Layers] ";
        internal const string k_FileFormatVersion = "1.0.0";

        // OpenXR runtime constants for StreamingAssets path
        internal const string k_RuntimeOpenXRPath = "openxr";
        internal const string k_RuntimeApiLayersPath = "api_layers";
        internal const string k_RuntimeExplicitLayersDir = "explicit.d";

        // Characters to trim from the start of paths
        internal static readonly char[] k_PathTrimChars = { '.', '/', '\\' };

        /// <summary>
        /// Gets a read-only list of the currently configured API layers.
        /// </summary>
        public IReadOnlyList<ApiLayer> collection => m_Collection;

        [SerializeField]
        List<ApiLayer> m_Collection = new List<ApiLayer>();

        /// <summary>
        /// Gets the directory that OpenXR expects the explicit api layers to be in.
        /// </summary>
        /// <returns>The explicit api layers directory.</returns>
        internal static string GetExplicitLayersDir()
        {
            var majorApiVersion = k_OpenXRApiMajorVersionFallback;
            var apiVersion = OpenXRRuntime.apiVersion;
            if (Version.TryParse(apiVersion, out Version version))
                majorApiVersion = version.Major.ToString();

            return Path.Combine(k_RuntimeOpenXRPath, majorApiVersion, k_RuntimeApiLayersPath, k_RuntimeExplicitLayersDir);
        }

        /// <summary>
        /// Checks if a specific API layer is enabled for the specified architecture.
        /// </summary>
        /// <remarks>
        /// Use this method to verify whether a specific API layer is currently active for a given architecture
        /// before attempting operations that depend on that layer. This is particularly useful when you have
        /// multiple architectures in your project and need to ensure a layer is enabled for the target platform.
        /// If the layer doesn't exist for the specified architecture, this method returns false.
        /// </remarks>
        /// <param name="layerName">The name of the API layer to check the enabled state for (e.g., "XR_APILAYER_LUNARG_core_validation").</param>
        /// <param name="libraryArchitecture">The target architecture of the layer's library file to check (e.g., Architecture.X64).</param>
        /// <returns>`true` if the API layer exists and is enabled for the specified architecture; otherwise, `false`.</returns>
        /// <example>
        /// <para>
        /// This example shows how to check if a layer is enabled for a specific architecture:
        /// <c>
        /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
        /// if (apiLayersFeature != null)
        /// {
        ///     bool isEnabled = apiLayersFeature.apiLayers.IsEnabled("XR_APILAYER_LUNARG_core_validation", Architecture.X64);
        ///     Debug.Log($"Core validation layer for x64: {(isEnabled ? "enabled" : "disabled")}");
        /// }
        /// </c>
        /// </para>
        /// </example>
        public bool IsEnabled(string layerName, Architecture libraryArchitecture) => m_Collection.Find(layer => layer.name == layerName && layer.libraryArchitecture == libraryArchitecture).isEnabled;


        /// <summary>
        /// Checks if a specific API layer is enabled for any architecture.
        /// </summary>
        /// <remarks>
        /// This overload checks if the specified layer is enabled for at least one architecture in the collection.
        /// Use this method when you want to know if a layer is active anywhere in your project, regardless of
        /// the target architecture. This is useful for validation checks or when displaying layer status in the UI.
        /// </remarks>
        /// <param name="layerName">The name of the API layer to check the enabled state for across all architectures.</param>
        /// <returns>`true` if the layer exists and is enabled for at least one architecture; otherwise, `false`.</returns>
        /// <example>
        /// <para>
        /// This example demonstrates checking if a layer is enabled for any architecture:
        /// <c>
        /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
        /// if (apiLayersFeature != null)
        /// {
        ///     if (apiLayersFeature.apiLayers.IsEnabled("XR_APILAYER_LUNARG_core_validation"))
        ///     {
        ///         Debug.Log("Core validation layer is enabled for at least one architecture");
        ///     }
        /// }
        /// </c>
        /// </para>
        /// </example>
        public bool IsEnabled(string layerName) => m_Collection.FindAll(layer => layer.name == layerName).Any(layer => layer.isEnabled == true);

        /// <summary>
        /// Enables or disables a specific API layer by name and architecture.
        /// </summary>
        /// <remarks>
        /// Use this method to control the enabled state of an API layer for a specific architecture at runtime
        /// or in Editor scripts. The changes take effect when the OpenXR instance is created. If the specified layer
        /// doesn't exist for the given architecture, this method has no effect. For build-time configuration,
        /// consider setting the layer state in the OpenXR settings UI instead.
        /// </remarks>
        /// <param name="layerName">The name of the API layer to check the enabled state for (e.g., "XR_APILAYER_LUNARG_core_validation").</param>
        /// <param name="libraryArchitecture">The target architecture of the layer's library file to modify.</param>
        /// <param name="enabled">The desired enabled state to set for the specified API layer (`true` to enable, `false` to disable).</param>
        /// <example>
        /// <para>
        /// This example shows how to enable a layer for a specific architecture:
        /// <c>
        /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
        /// if (apiLayersFeature != null)
        /// {
        ///     // Enable core validation for x64 builds
        ///     apiLayersFeature.apiLayers.SetEnabled("XR_APILAYER_LUNARG_core_validation", Architecture.X64, true);
        ///     Debug.Log("Core validation layer enabled for x64");
        /// }
        /// </c>
        /// </para>
        /// </example>
        public void SetEnabled(string layerName, Architecture libraryArchitecture, bool enabled)
        {
            int index = m_Collection.FindIndex(layer => layer.name == layerName && layer.libraryArchitecture == libraryArchitecture);
            if (index != -1)
            {
                var layer = m_Collection[index];
                layer.isEnabled = enabled;
                m_Collection[index] = layer;
            }
        }

        /// <summary>
        /// Enables or disables a specific API layer.
        /// </summary>
        /// <remarks>
        /// This is a convenience overload that accepts an ApiLayer struct directly. Use this when you already
        /// have a reference to the layer object from the collection. This method modifies the enabled state
        /// for the specific architecture associated with the provided layer instance.
        /// </remarks>
        /// <param name="apiLayer">The API layer object to modify the enabled state for.</param>
        /// <param name="enabled">The desired enabled state to set for the specified API layer (`true` to enable, `false` to disable).</param>
        /// <example>
        /// <para>
        /// This example demonstrates enabling a layer using the ApiLayer object:
        /// <c>
        /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
        /// if (apiLayersFeature != null)
        /// {
        ///     foreach (ApiLayers.ApiLayer layer in apiLayersFeature.apiLayers.collection)
        ///     {
        ///         if (layer.name == "XR_APILAYER_LUNARG_core_validation")
        ///         {
        ///             apiLayersFeature.apiLayers.SetEnabled(layer, true);
        ///         }
        ///     }
        /// }
        /// </c>
        /// </para>
        /// </example>
        public void SetEnabled(ApiLayer apiLayer, bool enabled) => SetEnabled(apiLayer.name, apiLayer.libraryArchitecture, enabled);

        /// <summary>
        /// Enables or disables a specific API layer by name, affecting all architectures.
        /// </summary>
        /// <remarks>
        /// This overload modifies the enabled state for all architectures of the specified layer. Use this
        /// when you want to enable or disable a layer across all platforms simultaneously, which is useful
        /// for global development or debugging scenarios. If you need architecture-specific control, use
        /// the overload that accepts an architecture parameter instead.
        /// </remarks>
        /// <param name="layerName">The name of the API layer to modify the enabled state for across all architectures.</param>
        /// <param name="enabled">The desired enabled state to set for all architectures of the specified API layer.</param>
        /// <example>
        /// <para>
        /// This example shows how to disable a layer for all architectures:
        /// <c>
        /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
        /// if (apiLayersFeature != null)
        /// {
        ///     // Disable core validation for all architectures
        ///     apiLayersFeature.apiLayers.SetEnabled("XR_APILAYER_LUNARG_core_validation", false);
        ///     Debug.Log("Core validation layer disabled for all architectures");
        /// }
        /// </c>
        /// </para>
        /// </example>
        public void SetEnabled(string layerName, bool enabled)
        {
            var apiLayers = m_Collection.FindAll(layer => layer.name == layerName);
            foreach (var apiLayer in apiLayers)
            {
                SetEnabled(apiLayer, enabled);
            }

        }

        /// <summary>
        /// Sets an API layer to a new index in the list with its original index, affecting layer order.
        /// </summary>
        /// <remarks>
        /// Use this method to reorder layers when execution order matters for your debugging or validation workflow.
        /// The layer at originalIndex will be moved to destinationIndex, and other layers will shift accordingly.
        /// If either index is out of bounds, the method returns false and no changes are made.
        /// </remarks>
        /// <param name="originalIndex">The current index of the API layer to move within the collection.</param>
        /// <param name="destinationIndex">The target index to move the API layer to within the collection.</param>
        /// <returns>`true` if the API layer was successfully moved to the new index; otherwise, `false` if indices are invalid.</returns>
        /// <example>
        /// <para>
        /// This example demonstrates reordering layers by index:
        /// <c>
        /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
        /// if (apiLayersFeature != null)
        /// {
        ///     // Move the layer at index 2 to index 0 (make it execute first)
        ///     bool success = apiLayersFeature.apiLayers.SetIndex(2, 0);
        ///     if (success)
        ///     {
        ///         Debug.Log("Layer reordered successfully");
        ///     }
        /// }
        /// </c>
        /// </para>
        /// </example>
        public bool SetIndex(int originalIndex, int destinationIndex)
        {
            if (originalIndex < 0 || originalIndex >= m_Collection.Count || destinationIndex < 0 || destinationIndex >= m_Collection.Count)
                return false;

            var item = m_Collection[originalIndex];
            m_Collection.RemoveAt(originalIndex);
            m_Collection.Insert(destinationIndex, item);
            return true;
        }


        /// <summary>
        /// Sets an API layer to a new index in the list with its name and architecture, affecting layer order.
        /// </summary>
        /// <remarks>
        /// Use this method to reorder a layer by name and architecture when you do not know its current index.
        /// This is useful when you know the layer you want to move but not its current
        /// position in the collection. The method returns false if no matching layer is found.
        /// </remarks>
        /// <param name="layerName">The name of the API layer to reposition within the collection.</param>
        /// <param name="libraryArchitecture">The architecture of the API layer's library file to reposition.</param>
        /// <param name="destinationIndex">The target index to move the API layer to within the collection.</param>
        /// <returns>`true` if the API layer was successfully moved to the new index; otherwise, `false` if the layer was not found.</returns>
        /// <example>
        /// <para>
        /// This example shows how to move a specific layer to the beginning of the list:
        /// <c>
        /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
        /// if (apiLayersFeature != null)
        /// {
        ///     bool success = apiLayersFeature.apiLayers.SetIndex(
        ///         "XR_APILAYER_LUNARG_core_validation",
        ///         Architecture.X64,
        ///         0);
        ///     if (success)
        ///     {
        ///         Debug.Log("Core validation layer moved to execute first");
        ///     }
        /// }
        /// </c>
        /// </para>
        /// </example>
        public bool SetIndex(string layerName, Architecture libraryArchitecture, int destinationIndex)
        {
            var originalIndex = m_Collection.FindIndex(layer => layer.name == layerName && layer.libraryArchitecture == libraryArchitecture);
            return SetIndex(originalIndex, destinationIndex);
        }

        /// <summary>
        /// Sets an API layer to a new index in the list with its object instance, affecting layer order.
        /// </summary>
        /// <remarks>
        /// This convenience overload accepts an ApiLayer struct directly. Use this when you already have
        /// a reference to the layer object you want to reorder. The layer will be moved to the specified
        /// destination index in the collection.
        /// </remarks>
        /// <param name="apiLayer">The API layer object to move to a new position within the collection.</param>
        /// <param name="destinationIndex">The target index to move the API layer to within the collection.</param>
        /// <returns>`true` if the API layer was successfully moved to the new index; otherwise, `false`.</returns>
        /// <example>
        /// <para>
        /// This example demonstrates reordering using an ApiLayer object:
        /// <c>
        /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
        /// if (apiLayersFeature != null)
        /// {
        ///     ApiLayers.ApiLayer layer = apiLayersFeature.apiLayers.collection[2];
        ///     bool success = apiLayersFeature.apiLayers.SetIndex(layer, 0);
        ///     if (success)
        ///     {
        ///         Debug.Log($"Layer {layer.name} moved to first position");
        ///     }
        /// }
        /// </c>
        /// </para>
        /// </example>
        public bool SetIndex(ApiLayer apiLayer, int destinationIndex) => SetIndex(apiLayer.name, apiLayer.libraryArchitecture, destinationIndex);


#if UNITY_EDITOR
        /// <summary>
        /// Adds a new API layer from a JSON manifest file. This method validates the manifest,
        /// copies the layer files into the project, and updates the settings.
        /// </summary>
        /// <param name="jsonPath">The absolute path to the JSON manifest file.</param>
        /// <param name="libraryArchitecture">The architecture of the api layer's library file.</param>
        /// <param name="targetGroup">The build target group to use.</param>
        /// <param name="apiLayer">The created API layer instance if successful.</param>
        /// <returns>True if the layer was successfully added, false otherwise.</returns>
        public bool TryAdd(string jsonPath, Architecture libraryArchitecture, BuildTargetGroup targetGroup, out ApiLayer apiLayer)
        {
            try
            {
                // Validate file existence and extension before processing.
                if (!File.Exists(jsonPath))
                {
                    Debug.LogError(k_LogPrefix + $"JSON file not found: {jsonPath}");
                    apiLayer = default;
                    return false;
                }

                if (!Path.GetExtension(jsonPath).Equals(k_JsonExt, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError(k_LogPrefix + "File must have .json extension");
                    apiLayer = default;
                    return false;
                }

                // Parse the JSON manifest
                string jsonContent = File.ReadAllText(jsonPath);
                var layerManifest = JsonUtility.FromJson<ApiLayerManifestJson>(jsonContent);

                if (string.IsNullOrEmpty(layerManifest.api_layer.name) || string.IsNullOrEmpty(layerManifest.api_layer.api_version) || string.IsNullOrEmpty(layerManifest.api_layer.library_path))
                {
                    Debug.LogError(k_LogPrefix + "Invalid JSON - missing some required members.");
                    apiLayer = default;
                    return false;
                }

                // Check if a layer with the same name and architecture already exists.
                if (Exists(layerManifest.api_layer.name, libraryArchitecture))
                {
                    Debug.LogWarning(k_LogPrefix + $"Layer '{layerManifest.api_layer.name}' for architecture '{Enum.GetName(typeof(Architecture), libraryArchitecture).ToLower()}' already imported");
                    apiLayer = default;
                    return false;
                }

                // Resolve and validate the library path
                var libraryPath = FileProcessor.ResolveLibraryPath(jsonPath, layerManifest.api_layer.library_path);
                if (!File.Exists(libraryPath))
                {
                    Debug.LogError(k_LogPrefix + $"Library file not found: {libraryPath}");
                    apiLayer = default;
                    return false;
                }

                // Only allow library files in the same directory as json file
                if (!string.Equals(Path.GetDirectoryName(libraryPath), Path.GetDirectoryName(jsonPath)))
                {
                    Debug.LogError(k_LogPrefix + $"Library file must be in the same directory as json file: {Path.GetDirectoryName(jsonPath)}");
                    apiLayer = default;
                    return false;
                }

                IPlatformSupport platformSupport = null;
                switch (targetGroup)
                {
                    case BuildTargetGroup.Standalone:
                        // We currently only support Windows for Standalone.
                        platformSupport = new ApiLayers.WindowsPlatformSupport();
                        break;
                    case BuildTargetGroup.Android:
                        platformSupport = new ApiLayers.AndroidPlatformSupport();
                        break;
                }

                if (platformSupport == null)
                {
                    apiLayer = default;
                    return false;
                }

                // Copy the manifest and library files to the project's API layers directory
                if (!FileProcessor.CopyApiLayerFiles(jsonPath, libraryPath, libraryArchitecture, platformSupport, ref layerManifest))
                {
                    apiLayer = default;
                    return false;
                }

                // Create and add the new API layer to the settings
                var archName = Enum.GetName(typeof(Architecture), libraryArchitecture);
                if (string.IsNullOrEmpty(archName))
                {
                    apiLayer = default;
                    return false;
                }

                apiLayer = new ApiLayer(layerManifest.api_layer, Path.GetFileName(jsonPath), libraryArchitecture, true);
                m_Collection.Add(apiLayer);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(k_LogPrefix + $"Import failed: {e.Message}");
                apiLayer = default;
                return false;
            }
        }

        /// <summary>
        /// Removes an API layer by name. This cleans up the associated files from the project
        /// and removes the layer from the settings.
        /// </summary>
        /// <param name="layerName">Name of the layer to remove.</param>
        /// <param name="libraryArchitecture">Architecture of the layer to remove's library.</param>
        /// <returns>True if the layer was successfully removed, false otherwise.</returns>
        public bool TryRemove(string layerName, Architecture libraryArchitecture)
        {
            try
            {
                if (string.IsNullOrEmpty(layerName))
                    return false;

                var layerToRemove = m_Collection.FirstOrDefault(l => l.name == layerName && l.libraryArchitecture == libraryArchitecture);
                if (string.IsNullOrEmpty(layerToRemove.name))
                {
                    Debug.LogWarning(k_LogPrefix + $"Layer '{layerName}' not found");
                    return false;
                }

                // Remove from settings list
                m_Collection.Remove(layerToRemove);

                // Clean up the physical files
                IPlatformSupport platformSupport = null;
#if UNITY_EDITOR_WIN
                platformSupport = new ApiLayers.WindowsPlatformSupport();
#endif
                if (platformSupport == null)
                    return false;

                var archName = Enum.GetName(typeof(Architecture), libraryArchitecture)?.ToLower();
                if (string.IsNullOrEmpty(archName))
                    return false;

                var apiLayersDir = Path.Combine(platformSupport.GetApiLayersDir(), archName);
                var jsonPath = Path.GetFullPath(Path.Combine(apiLayersDir, layerToRemove.jsonFileName));
                FileProcessor.CleanupApiLayerFiles(layerName, jsonPath);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(k_LogPrefix + $"Remove failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes an API layer by index. This cleans up the associated files from the project
        /// and removes the layer from the settings.
        /// </summary>
        /// <param name="layerIndex">Index of the layer to remove.</param>
        /// <returns>True if the layer was successfully removed, false otherwise.</returns>

        public bool TryRemove(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= m_Collection.Count)
                return false;

            var layer = m_Collection.ElementAt(layerIndex);
            if (string.IsNullOrEmpty(layer.name))
            {
                m_Collection.RemoveAt(layerIndex);
                return true;
            }

            return TryRemove(layer.name, layer.libraryArchitecture);
        }
#endif
        /// <summary>
        /// Checks if an API layer with the given name already exists in the settings.
        /// </summary>
        /// <param name="layerName">The name of the layer to check.</param>
        /// <param name="libraryArchitecture">The architecture of the api layer's library file.</param>
        /// <returns>True if the layer exists, false otherwise.</returns>
        bool Exists(string layerName, Architecture libraryArchitecture)
        {
            return m_Collection.Any(layer => string.Equals(layer.name, layerName)
            && layer.libraryArchitecture == libraryArchitecture);
        }

        /// <summary>
        /// Handles file operations for API layer management, including copying, cleanup, and path resolution.
        /// This nested class encapsulates the logic for managing the physical files associated with API layers.
        /// </summary>
        static class FileProcessor
        {
            /// <summary>
            /// Copies the API layer's JSON manifest and library file to the project's import directory.
            /// It also updates the library path within the manifest to be relative to the new location.
            /// </summary>
            /// <param name="jsonPath">Original path to the JSON manifest.</param>
            /// <param name="libraryPath">Original path to the library file.</param>
            /// <param name="libraryArchitecture">Architecture of the library file.</param>
            /// <param name="platformSupport">Platform strategy to use.</param>
            /// <param name="layerManifest">The parsed layer manifest, which may be modified.</param>
            /// <returns>True if the files were copied successfully, false otherwise.</returns>
            internal static bool CopyApiLayerFiles(string jsonPath, string libraryPath, Architecture libraryArchitecture, IPlatformSupport platformSupport, ref ApiLayerManifestJson layerManifest)
            {
                try
                {
                    // Verify the library file contains a supported extension.
                    var supportedExtensions = platformSupport.GetSupportedExtensions();
                    var isExtensionSupported = false;
                    var libraryExtension = Path.GetExtension(libraryPath);

                    foreach (string extension in supportedExtensions)
                    {
                        if (string.Equals(extension, libraryExtension))
                        {
                            isExtensionSupported = true;
                            break;
                        }
                    }

                    if (!isExtensionSupported)
                    {
                        Debug.LogWarning(k_LogPrefix + $"`{libraryExtension}` is not supported on this platform.");
                        return false;
                    }

                    var archName = Enum.GetName(typeof(Architecture), libraryArchitecture);
                    if (string.IsNullOrEmpty(archName))
                        return false;

                    string importDir = Path.Combine(platformSupport.GetApiLayersDir(), archName.ToLower());
                    if (!Directory.Exists(importDir))
                        Directory.CreateDirectory(importDir);

                    string targetJsonPath = Path.Combine(importDir, Path.GetFileName(jsonPath));

                    // We've already verified that the library path is in the same directory as the jsonPath
                    string targetLibraryPath = Path.Combine(importDir, Path.GetFileName(libraryPath));

                    // Copy files to the import directory, intentionally overwriting if they exist
                    if (!string.Equals(jsonPath, targetJsonPath, StringComparison.OrdinalIgnoreCase))
                        File.Copy(jsonPath, targetJsonPath, true);

                    if (!string.Equals(libraryPath, targetLibraryPath, StringComparison.OrdinalIgnoreCase))
                        File.Copy(libraryPath, targetLibraryPath, true);

                    // Standardize the library_path in the manifest to be a relative path from the JSON file if we were given an absolute path.
                    // This ensures the OpenXR loader can find it correctly.
                    if (Path.IsPathRooted(libraryPath))
                    {
                        var newLibraryPath = $"{k_LibraryPathPrefix}{Path.GetFileName(libraryPath)}";

                        // Regex pattern to match "library_path": "any value"
                        // This handles both single and double quotes, and escaped characters
                        var pattern = @"(""library_path""\s*:\s*"")([^""]*?)("")";

                        // Replace with new path
                        var jsonContent = File.ReadAllText(targetJsonPath);
                        var updatedJson = Regex.Replace(jsonContent, pattern, $"$1{newLibraryPath}$3");

                        // Write back to file
                        File.WriteAllText(targetJsonPath, updatedJson);

                        layerManifest.api_layer.library_path = newLibraryPath;
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError(k_LogPrefix + $"File copy failed: {e.Message}");
                    return false;
                }
            }


            /// <summary>
            /// Deletes the files associated with a specific API layer from the import directory.
            /// </summary>
            /// <param name="layerName">The name of the layer to clean up.</param>
            /// <param name="jsonPath">The file path of the JSON manifest to delete.</param>
            internal static void CleanupApiLayerFiles(string layerName, string jsonPath)
            {
                try
                {
                    if (!File.Exists(jsonPath))
                        return;

                    // Read the manifest to find the associated library file
                    string content = File.ReadAllText(jsonPath);
                    var layerManifest = JsonUtility.FromJson<ApiLayerManifestJson>(content);

                    if (layerManifest.api_layer.name == layerName)
                    {
                        File.Delete(jsonPath);

                        string jsonDir = Path.GetDirectoryName(jsonPath);
                        string libraryPath = Path.Combine(jsonDir, layerManifest.api_layer.library_path.TrimStart(k_PathTrimChars));

                        // Verify library file is in the expected directory before deleting.
                        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(libraryPath)), Path.GetDirectoryName(Path.GetFullPath(jsonPath)), StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"Cannot find library file in json directory `{jsonDir}`, it may have been modified outside of Unity.");
                            return;
                        }

                        // Delete the library file
                        if (File.Exists(libraryPath))
                            File.Delete(libraryPath);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning(k_LogPrefix + $"Cleanup failed: {e.Message}");
                }
            }

            /// <summary>
            /// Resolves the absolute path to an API layer's library file from the relative path in its manifest.
            /// </summary>
            /// <param name="jsonPath">The path to the JSON manifest file.</param>
            /// <param name="libraryPath">The library path specified in the manifest.</param>
            /// <returns>The absolute path to the library file.</returns>
            internal static string ResolveLibraryPath(string jsonPath, string libraryPath)
            {
                // If the path is already absolute, return it directly
                if (Path.IsPathRooted(libraryPath))
                    return libraryPath;

                // Otherwise, resolve it relative to the JSON manifest's directory
                string jsonDir = Path.GetDirectoryName(jsonPath);
                return Path.GetFullPath(Path.Combine(jsonDir, libraryPath));
            }
        }
    }

}
