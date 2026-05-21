using System;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.XR.OpenXR
{
    public partial class ApiLayers
    {
        /// <summary>
        /// An abstract base class for support classes that provide logging functionality for specific API layers.
        /// This class handles the common logic for setting up log file redirection, processing the log file content,
        /// and cleaning up resources on exit.
        /// </summary>
        internal abstract class LogSupport : ISupport
        {
            const char k_NewLineChar = '\n';
            protected const string k_TextExportType = "text";
            protected const string k_LogsDir = "Logs~";

            protected abstract string LogFileName { get; }
            protected abstract string LogPrefix { get; }

            protected string m_LogFilePath;

            /// <summary>
            /// Sets up logging and registers for play mode state changes in the editor.
            /// </summary>
            /// <param name="hookGetInstanceProcAddr">GetInstance function pointer for potential native setup.</param>
            public virtual void Setup(IntPtr hookGetInstanceProcAddr)
            {
                SetupLog();

                // Clear existing default log file if it exists.
                var defaultLog = GetDefaultLogPath();
                if (File.Exists(defaultLog))
                    File.WriteAllText(defaultLog, string.Empty);
#if UNITY_EDITOR
                // In the editor, we need to hook into play mode state changes to process logs when exiting play mode.
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
            }

            /// <summary>
            // Processes and cleans up the log file during teardown.
            /// </summary>
            /// <param name="xrInstance">The OpenXR instance handle.</param>
            public virtual void Teardown(ulong xrInstance)
            {
                ProcessLog();
            }

            /// <summary>
            /// Abstract method for platform-specific log setup (e.g., setting environment variables).
            /// </summary>
            protected abstract void SetupLog();

            protected string GetDefaultLogPath()
            {
                string logDir = Path.Combine(Application.dataPath, k_EditorXrDir, k_EditorApiLayersDir, k_LogsDir);
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                return Path.Combine(logDir, LogFileName);
            }

            /// <summary>
            /// Reads the content of the generated log file, prints it to the Unity console, and deletes the file.
            /// </summary>
            protected void ProcessLog()
            {
                if (string.IsNullOrEmpty(m_LogFilePath) || !File.Exists(m_LogFilePath))
                    return;
                try
                {
                    ProcessLogFile(m_LogFilePath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{LogPrefix}Failed to process log file: {e.Message}");
                }
                finally
                {
                    m_LogFilePath = null;
                }
            }

            /// <summary>
            /// Reads a file in chunks to avoid potential memory overflow from very large log files.
            /// </summary>
            /// <param name="filePath">The path to the file to read.</param>
            /// <param name="maxLines">The max number of lines to read.</param>
            private void ProcessLogFile(string filePath, int maxLines = 5000)
            {
                var lines = File.ReadLines(filePath).Take(maxLines).ToList();

                if (lines.Count == maxLines)
                {
                    // Tell user the log path if we didn't automatically create the log for them (since we delete the log file).
                    //if (!string.Equals(GetDefaultLogPath(), filePath, StringComparison.OrdinalIgnoreCase))
                    Debug.LogWarning($"{LogPrefix} Log truncated to {maxLines} lines. Full log located at `{filePath}`.");
                }

                var log = string.Join(k_NewLineChar.ToString(), lines);
                Debug.Log($"{LogPrefix}\n{log}");
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
                    ProcessLog();
                    EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                }
            }
#endif
        }
    }

}
