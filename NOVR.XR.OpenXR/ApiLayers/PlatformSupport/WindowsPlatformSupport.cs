using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace UnityEngine.XR.OpenXR
{
    public partial class ApiLayers
    {
        /// <summary>
        /// Implements the platform-specific support for discovering OpenXR API Layers on Windows.
        /// </summary>
        internal class WindowsPlatformSupport : IPlatformSupport
        {
            const string k_WindowsApiLayersPath = "WindowsLayers";

            // Environment variable used by the OpenXR loader to find API layers.
            const string k_ApiLayerPathVar = "XR_API_LAYER_PATH";
            const string k_PathSeparator = ";";
            const string k_DllExt = ".dll";

            const string k_OpenXRPackageName = "com.unity.xr.openxr";

            static readonly Architecture[] s_SupportedArchitectures = new Architecture[] { Architecture.X64 };

            /// <summary>
            /// Defines the execution order for the build callbacks.
            /// </summary>
            public int callbackOrder => 0;

            string m_OriginalEnabledLayersPath;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            public static void AddSupport() => Features.ApiLayersFeature.AddSupport(new WindowsPlatformSupport());
#endif
            /// <summary>
            /// Sets the Windows environment variable that OpenXR will use to scan for Api Layer manifest files.
            /// </summary>
            public void Setup(IntPtr hookGetInstanceProcAddr)
            {
#if UNITY_EDITOR
                var processArchitecture = RuntimeInformation.ProcessArchitecture;
                var archName = Enum.GetName(typeof(Architecture), processArchitecture);
                if (string.IsNullOrEmpty(archName))
                    return;

                var platformEnabledApiLayersDir = Path.Combine(GetApiLayersDir(), archName.ToLower());
#else
                var platformEnabledApiLayersDir = GetApiLayersDir();
#endif
                var existing = Environment.GetEnvironmentVariable(k_ApiLayerPathVar);

                // Prepend our layer path to the existing environment variable to give it priority.
                if (string.IsNullOrEmpty(existing))
                {
                    m_OriginalEnabledLayersPath = string.Empty;
                    Environment.SetEnvironmentVariable(k_ApiLayerPathVar, platformEnabledApiLayersDir);
                }
                else if (!existing.Contains(platformEnabledApiLayersDir))
                {
                    m_OriginalEnabledLayersPath = existing;
                    Environment.SetEnvironmentVariable(k_ApiLayerPathVar,
                        $"{platformEnabledApiLayersDir}{k_PathSeparator}{existing}");
                }
            }

            /// <summary>
            /// Sets the Windows environment variable back to it's original state.
            /// </summary>
            public void Teardown(ulong xrInstance)
            {
                Environment.SetEnvironmentVariable(k_ApiLayerPathVar, m_OriginalEnabledLayersPath);
            }

            /// <summary>
            /// Gets the directory where API layer assets are stored for the Windows platform.
            /// </summary>
            /// <returns>The full path to the API layers directory.</returns>
            public string GetApiLayersDir()
            {
#if UNITY_EDITOR
                // In the editor, layers are stored in a special directory next to Assets
                return Path.Combine(Application.dataPath, k_EditorXrDir, k_EditorApiLayersDir, k_WindowsApiLayersPath);
#else
                return Path.Combine(Application.streamingAssetsPath, GetExplicitLayersDir());
#endif
            }

            /// <summary>
            /// Gets the directory path where bundled Windows API layers are stored within the OpenXR package.
            /// </summary>
            /// <returns>The full path to the bundled layers directory for the appropriate architecture.</returns>
            public string GetBundleDir()
            {

#if UNITY_EDITOR
                var packagePath = string.Empty;

                var packageInfo = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                    .FirstOrDefault(p => p.name == k_OpenXRPackageName);

                if (packageInfo != null)
                    packagePath = packageInfo.resolvedPath;

                if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
                    return string.Empty;

                return Path.Combine(packagePath, k_EditorApiLayersDir, k_WindowsApiLayersPath);
#else
                return string.Empty;
#endif
            }

            public Architecture[] GetSupportedArchitectures() => s_SupportedArchitectures;

            public string[] GetSupportedExtensions() => new string[] { k_DllExt };
        }
    }

}
