using System;
using System.Runtime.InteropServices;
using UnityEngine.XR.OpenXR.Features;

namespace UnityEngine.XR.OpenXR
{
    public partial class ApiLayers
    {
        /// <summary>
        /// Provides support for the `XR_APILAYER_LUNARG_core_validation` layer.
        /// This class manages the setup and teardown of environment variables required
        /// to redirect the layer's validation output to a log file.
        /// </summary>
        internal class CoreValidationLogSupport : LogSupport
        {
            internal const string k_LayerName = "XR_APILAYER_LUNARG_core_validation";
            const string k_LogFileNameConst = "core_validation_logs.txt";
            const string k_LogPrefixConst = "[Validation Core] ";

            // Windows-specific environment variables for configuring the core_validation layer
            const string k_WindowsFileEnvVar = "XR_CORE_VALIDATION_FILE_NAME";
            const string k_WindowsExportTypeEnvVar = "XR_CORE_VALIDATION_EXPORT_TYPE";

            protected override string LogFileName => k_LogFileNameConst;
            protected override string LogPrefix => k_LogPrefixConst;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            public static void AddSupport() => ApiLayersFeature.AddSupport(new CoreValidationLogSupport());

            /// <summary>
            /// Sets up logging for the Core Validation layer based on the current platform.
            /// </summary>
            protected override void SetupLog()
            {
                m_LogFilePath = string.Empty;
#if UNITY_EDITOR
                var openXRSettings = OpenXRSettings.Instance;
#else
                var openXRSettings = OpenXRSettings.ActiveBuildTargetInstance;
#endif
                if (openXRSettings == null)
                    return;

                var apiLayersFeature = openXRSettings.GetFeature<ApiLayersFeature>();
                if (apiLayersFeature == null || !apiLayersFeature.enabled)
                    return;

                // Logging is only enabled if the layer is enabled and debug utils are active
                if (!apiLayersFeature.apiLayers.IsEnabled(k_LayerName, RuntimeInformation.ProcessArchitecture))
                    return;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                SetupWindowsLog();
#endif
            }

            /// <summary>
            /// Configures environment variables to direct the Core Validation layer's output to a file on Windows.
            /// </summary>
            void SetupWindowsLog()
            {
                // Do not override user-configured environment variables
                var filePathEnvVar = Environment.GetEnvironmentVariable(k_WindowsFileEnvVar);
                if (!string.IsNullOrEmpty(filePathEnvVar))
                {
                    m_LogFilePath = filePathEnvVar;
                    return;
                }

                m_LogFilePath = GetDefaultLogPath();
                Environment.SetEnvironmentVariable(k_WindowsExportTypeEnvVar, k_TextExportType);
                Environment.SetEnvironmentVariable(k_WindowsFileEnvVar, m_LogFilePath);
            }

            /// <summary>
            /// Performs teardown operations, including cleaning up environment variables and processing the log file.
            /// </summary>
            /// <param name="xrInstance">The OpenXR instance handle.</param>
            public override void Teardown(ulong xrInstance)
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                TeardownWindowsLog();
#endif
                base.Teardown(xrInstance);
            }


            /// <summary>
            /// Cleans up the environment variables set during setup on Windows.
            /// </summary>
            void TeardownWindowsLog()
            {
                // Clean up environment variables only if we were the ones who set them
                if (string.Equals(m_LogFilePath, GetDefaultLogPath()))
                {
                    Environment.SetEnvironmentVariable(k_WindowsFileEnvVar, null);
                    Environment.SetEnvironmentVariable(k_WindowsExportTypeEnvVar, null);
                }
            }
        }
    }
}
