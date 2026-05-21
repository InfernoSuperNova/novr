using System;
using System.Runtime.InteropServices;

namespace UnityEngine.XR.OpenXR
{
    public partial class ApiLayers
    {
        /// <summary>
        /// Contains information about an imported OpenXR API layer, including its enabled state and path.
        /// This struct is the primary representation of an API layer within the OpenXR settings.
        /// </summary>
        /// <remarks>
        /// The `ApiLayer` struct represents a single OpenXR API layer that has been imported into your Unity project.
        /// This struct contains metadata parsed from the layer's JSON manifest file.
        /// The OpenXR runtime processes API layers in order to provide debugging, validation, or performance analysis capabilities.
        /// </remarks>
        /// <example>
        /// <para>
        /// This example shows how to check if a specific API layer is enabled:
        /// <c>
        /// ApiLayersFeature apiLayersFeature = OpenXRSettings.Instance.GetFeature&lt;ApiLayersFeature&gt;();
        /// if (apiLayersFeature != null)
        /// {
        ///     foreach (ApiLayers.ApiLayer layer in apiLayersFeature.apiLayers.collection)
        ///     {
        ///         if (layer.isEnabled)
        ///         {
        ///             Debug.Log($"Layer {layer.name} is enabled for {layer.libraryArchitecture}");
        ///         }
        ///     }
        /// }
        /// </c>
        /// </para>
        /// </example>
        /// <seealso cref="ApiLayers"/>
        /// <seealso cref="Features.ApiLayersFeature"/>
        [Serializable]
        public struct ApiLayer
        {
            [SerializeField]
            ApiLayerJson m_Json;

            [SerializeField]
            string m_JsonFileName;

            [SerializeField]
            Architecture m_LibraryArchitecture;

            [SerializeField]
            bool m_IsEnabled;

            /// <summary>
            /// Gets whether this API layer is enabled for use during runtime.
            /// An enabled layer will be included in the build and configured for the OpenXR loader.
            /// </summary>
            public bool isEnabled
            {
                get => m_IsEnabled;
                internal set => m_IsEnabled = value;
            }

            /// <summary>
            /// Gets the architecture for this API layer's library file.
            /// </summary>
            public Architecture libraryArchitecture => m_LibraryArchitecture;

            /// <summary>
            /// Gets the display name of this API layer, as specified in its manifest.
            /// </summary>
            public string name => m_Json.name;

            /// <summary>
            /// Gets the relative path to the API layer library file (e.g., a .dll or .so file).
            /// </summary>
            public string libraryPath => m_Json.library_path;

            /// <summary>
            /// Gets the OpenXR API version supported by this layer.
            /// </summary>
            public string apiVersion => m_Json.api_version;

            /// <summary>
            /// Gets the implementation version of this API layer.
            /// </summary>
            public string implementationVersion => m_Json.implementation_version;

            /// <summary>
            /// Gets the description text for this API layer.
            /// </summary>
            public string description => m_Json.description;

            /// <summary>
            /// Gets the file path to the JSON manifest file for this API layer.
            /// This path points to the imported JSON file within the Unity project.
            /// </summary>
            internal string jsonFileName => m_JsonFileName;

            /// <summary>
            /// Internal constructor for creating an ApiLayer instance.
            /// </summary>
            /// <param name="json">The parsed JSON data for the layer.</param>
            /// <param name="jsonPath">The path to the layer's JSON manifest file.</param>
            /// <param name="libraryArchitecture">The architecture of the api layer's library file.</param>
            /// <param name="isEnabled">The initial enabled state of the layer.</param>
            internal ApiLayer(ApiLayerJson json, string jsonFileName, Architecture libraryArchitecture, bool isEnabled)
            {
                m_Json = json;
                m_JsonFileName = jsonFileName;
                m_IsEnabled = isEnabled;
                m_LibraryArchitecture = libraryArchitecture;
            }
        }

        /// <summary>
        /// Represents the `api_layer` object within an OpenXR API layer manifest file.
        /// This struct is used for serialization and deserialization of the layer's properties from JSON.
        /// </summary>
        /// <remarks>
        /// This internal struct maps directly to the `api_layer` JSON object in an OpenXR layer manifest.
        /// It contains all the metadata needed to identify and load an API layer.
        /// </remarks>
        /// <exclude />
        [Serializable]
        internal struct ApiLayerJson
        {
            public string name;
            public string library_path;
            public string api_version;
            public string implementation_version;
            public string description;
        }

        /// <summary>
        /// Represents the top-level structure of an OpenXR API layer manifest JSON file.
        /// This is the root object when parsing the manifest.
        /// </summary>
        /// <remarks>
        /// This internal struct is the root container for deserializing OpenXR API layer manifest files.
        /// The file format version indicates the manifest schema version, and the api layer member contains
        /// the actual layer metadata.
        /// </remarks>
        /// <exclude />
        [Serializable]
        internal struct ApiLayerManifestJson
        {
            public string file_format_version;
            public ApiLayerJson api_layer;
        }
    }
}
