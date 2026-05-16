using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace UnityEngine.XR.OpenXR
{
    public partial class ApiLayers
    {
        /// <summary>
        /// Implements the platform-specific support for OpenXR API Layers on Android.
        /// OpenXR loads enabled api layers in pre-defined paths on Android and does not have an override path like Windows, so
        /// we don't need to add this support object to ApiLayersFeature.
        /// </summary>
        internal class AndroidPlatformSupport : IPlatformSupport
        {
            const string k_OpenXRPackageName = "com.unity.xr.openxr";
            const string k_AndroidApiLayersPath = "AndroidLayers";
            internal const string k_SoExt = ".so";
            const string k_Arm64Arch = "arm64-v8a";

            // Only these architectures supported at the moment
            static readonly Architecture[] s_SupportedArchitectures = new Architecture[] { Architecture.Arm64 };
            static readonly string[] s_SupportedExtensions = new string[] { k_SoExt };

            public void Setup(IntPtr hookGetInstanceProcAddr)
            {
                // OpenXR loader on Android handles API layer loading with pre-defined paths
            }

            public void Teardown(ulong xrInstance)
            {
                // OpenXR loader on Android handles API layer unloading with pre-defined paths
            }

            /// <summary>
            /// Gets the runtime directory path for API layers on Android, relative to StreamingAssets.
            /// </summary>
            /// <returns>The relative path to the explicit layers directory.</returns>
            public string GetApiLayersDir()
            {
#if UNITY_EDITOR
                // In the editor, layers are stored in a special directory next to Assets
                return Path.Combine(Application.dataPath, k_EditorXrDir, k_EditorApiLayersDir, k_AndroidApiLayersPath);
#else
                return Path.Combine(Application.streamingAssetsPath, GetExplicitLayersDir());
#endif
            }

            /// <summary>
            /// Gets the directory path where bundled Android API layers are stored within the OpenXR package.
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

                return Path.Combine(packagePath, k_EditorApiLayersDir, k_AndroidApiLayersPath);
#else
                return string.Empty;
#endif
            }

            public Architecture[] GetSupportedArchitectures() => s_SupportedArchitectures;

            public string[] GetSupportedExtensions() => s_SupportedExtensions;


            public string GetExpectedArchitectureImportString(Architecture architecture)
            {
                switch (architecture)
                {
                    case Architecture.Arm64:
                        return k_Arm64Arch;
                }

                return string.Empty;
            }
        }
    }

}
