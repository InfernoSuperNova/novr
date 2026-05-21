using System;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace UnityEngine.XR.OpenXR.Features
{
    /// <summary>
    /// Provides support for the XR_EXT_debug_utils OpenXR extension for debugging and development.
    /// This feature enables debug message callbacks from the OpenXR runtime with configurable severity and type filtering.
    /// </summary>
    /// <remarks>
    /// Enable this feature to receive debug messages from the OpenXR runtime. Use the `messageSeverity` and `messageType`
    /// properties to filter which messages you want to receive. Messages are logged to the Unity console with the
    /// [Debug Utils] prefix. This feature is particularly useful during development to identify validation errors,
    /// performance warnings, and conformance issues in your OpenXR application.
    /// </remarks>
    /// <example>
    /// <para>
    /// This example shows how to configure the Debug Utils feature to receive only error and warning messages:
    /// <c>
    /// OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
    /// DebugUtilsFeature debugUtilsFeature = settings.GetFeature&lt;DebugUtilsFeature&gt;();
    /// if (debugUtilsFeature != null)
    /// {
    ///     debugUtilsFeature.messageSeverity = DebugUtilsFeature.MessageSeverity.Error | DebugUtilsFeature.MessageSeverity.Warning;
    ///     debugUtilsFeature.messageType = DebugUtilsFeature.MessageType.Validation | DebugUtilsFeature.MessageType.Performance;
    /// }
    /// </c>
    /// </para>
    /// </example>
    /// <seealso cref="ApiLayersFeature"/>
    /// <seealso cref="MessageSeverity"/>
    /// <seealso cref="MessageType"/>
#if UNITY_EDITOR
    [OpenXRFeature(
        UiName = "Debug Utils",
        Desc = "Enables support for OpenXR Debug Utils extension.",
        Company = "Unity",
        Version = "1.0.0",
        OpenxrExtensionStrings = "XR_EXT_debug_utils",
        BuildTargetGroups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone },
        FeatureId = featureId
    )]
#endif
    public class DebugUtilsFeature : OpenXRFeature
    {
        /// <summary>
        /// Bitmask for specifying the severity of debug messages to be received.
        /// </summary>
        /// <remarks>
        /// Use these flags to filter OpenXR debug messages by severity level. Combine multiple values using the
        /// bitwise OR operator to receive messages at multiple severity levels. Setting this to all values
        /// will result in receiving all messages, which may impact performance during development.
        /// </remarks>
        /// <example>
        /// <para>
        /// This example demonstrates how to configure message severity filtering:
        /// <c>
        /// DebugUtilsFeature debugUtils = OpenXRSettings.Instance.GetFeature&lt;DebugUtilsFeature&gt;();
        /// // Receive only error messages
        /// debugUtils.messageSeverity = DebugUtilsFeature.MessageSeverity.Error;
        ///
        /// // Receive warnings and errors
        /// debugUtils.messageSeverity = DebugUtilsFeature.MessageSeverity.Warning | DebugUtilsFeature.MessageSeverity.Error;
        /// </c>
        /// </para>
        /// </example>
        [Flags]
        public enum MessageSeverity
        {
            /// <summary>
            /// Verbose/diagnostic information - most detailed level.
            /// </summary>
            Verbose = 1,

            /// <summary>
            /// Informational messages about normal operations.
            /// </summary>
            Info = 16,

            /// <summary>
            /// Warning messages about potential issues or non-optimal usage.
            /// </summary>
            Warning = 256,

            /// <summary>
            /// Error messages indicating failures or invalid operations.
            /// </summary>
            Error = 4096,
        }

        /// <summary>
        /// Bitmask for specifying the type of debug messages to be received.
        /// </summary>
        /// <remarks>
        /// Use these flags to filter OpenXR debug messages by category. Combine multiple values using the
        /// bitwise OR operator to receive messages of multiple types. For production builds, consider
        /// enabling only Performance messages to identify optimization opportunities. Validation messages
        /// are most useful during development to catch API usage errors.
        /// </remarks>
        /// <example>
        /// <para>
        /// This example shows how to configure message type filtering for validation and performance messages:
        /// <c>
        /// DebugUtilsFeature debugUtils = OpenXRSettings.Instance.GetFeature&lt;DebugUtilsFeature&gt;();
        /// // Receive only validation messages
        /// debugUtils.messageType = DebugUtilsFeature.MessageType.Validation;
        ///
        /// // Receive validation and performance messages
        /// debugUtils.messageType = DebugUtilsFeature.MessageType.Validation | DebugUtilsFeature.MessageType.Performance;
        /// </c>
        /// </para>
        /// </example>
        [Flags]
        public enum MessageType
        {
            /// <summary>
            /// General debug messages not covered by other types.
            /// </summary>
            General = 1,

            /// <summary>
            /// Messages related to validation of API usage and parameters.
            /// </summary>
            Validation = 2,

            /// <summary>
            /// Messages related to performance warnings and optimization suggestions.
            /// </summary>
            Performance = 4,

            /// <summary>
            /// Messages related to OpenXR specification conformance.
            /// </summary>
            Conformance = 8,
        }

        /// <summary>
        /// A unique identifier for this feature.
        /// </summary>
        public const string featureId = "com.unity.openxr.feature.debugutils";

        internal const MessageSeverity k_AllMessageSeverities = MessageSeverity.Verbose | MessageSeverity.Info | MessageSeverity.Warning | MessageSeverity.Error;
        internal const MessageType k_AllMessageTypes = MessageType.General | MessageType.Validation | MessageType.Performance | MessageType.Conformance;
        const string k_DebugLogPrefix = "[Debug Utils] ";
        delegate void DebugCallbackDelegate(string message);

        /// <summary>
        /// The severity level the debug utils extension should use to filter messages.
        /// </summary>
        public MessageSeverity messageSeverity
        {
            get => m_MessageSeverity;
            set
            {
                if (m_MessageSeverity == value)
                    return;

                m_MessageSeverity = value;
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        /// <summary>
        /// The message type that the debug utils extension should use to filter messages.
        /// </summary>
        public MessageType messageType
        {
            get => m_MessageType;
            set
            {
                if (m_MessageType == value)
                    return;

                m_MessageType = value;
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        DebugCallbackDelegate callback;

        [SerializeField]
        MessageSeverity m_MessageSeverity = MessageSeverity.Verbose | MessageSeverity.Info | MessageSeverity.Warning | MessageSeverity.Error;

        [SerializeField]
        MessageType m_MessageType = MessageType.General | MessageType.Validation | MessageType.Performance | MessageType.Conformance;

        /// <summary>
        /// Hooks into the OpenXR instance creation process to configure the XR Debug Utils extension.
        /// This override initializes the native debug callback with the configured message filters.
        /// </summary>
        /// <remarks>
        /// This method is called before the OpenXR instance is created. It registers a managed callback
        /// that will receive debug messages from the OpenXR runtime based on the configured messageSeverity
        /// and messageType filters. The callback remains active until the OpenXR instance is destroyed.
        /// </remarks>
        /// <param name="hookGetInstanceProcAddr">The original GetInstanceProcAddr function pointer.</param>
        /// <returns>The original GetInstanceProcAddr function pointer passed to the method.</returns>
        /// <example>
        /// <para>
        /// This method is called automatically by the OpenXR loader. You typically don't need to call it directly.
        /// However, you can override it in a derived class to add custom initialization:
        /// <c>
        /// public class CustomDebugUtils : DebugUtilsFeature
        /// {
        ///     protected internal override IntPtr HookGetInstanceProcAddr(IntPtr hookGetInstanceProcAddr)
        ///     {
        ///         Debug.Log("Initializing custom debug utilities");
        ///         return base.HookGetInstanceProcAddr(hookGetInstanceProcAddr);
        ///     }
        /// }
        /// </c>
        /// </para>
        /// </example>
        protected internal override IntPtr HookGetInstanceProcAddr(IntPtr hookGetInstanceProcAddr)
        {
            SetCallback(DebugCallback);
            SetConfig(messageSeverity, messageType);
            return base.HookGetInstanceProcAddr(hookGetInstanceProcAddr);
        }

        /// <summary>
        /// Sets the native callback for receiving OpenXR debug messages.
        /// </summary>
        /// <param name="callback">The action to be called with the debug message string.</param>
        void SetCallback(DebugCallbackDelegate callbackDelegate)
        {
            callback = callbackDelegate;
            DebugUtilsSetCallback(callback);
        }

        /// <summary>
        /// Sets the native configuration for receiving OpenXR debug messages.
        /// </summary>
        /// <param name="messageSeverity">Configures the XR_EXT_debug_utils extension to filter log messages for specific severity levels.</param>
        /// <param name="messageType">Configures the XR_EXT_debug_utils extension to filter for specific types of log messages.</param>
        /// <returns>`true` if the configuration was successfully applied, `false` otherwise.</returns>
        bool SetConfig(MessageSeverity messageSeverity, MessageType messageType) => DebugUtilsSetConfig((ulong)messageSeverity, (ulong)messageType);

        /// <summary>
        /// This method triggers when the OpenXR instance is successfully created.
        /// </summary>
        /// <param name="xrInstance">The handle to the created OpenXR instance.</param>
        /// <returns>`true` if initialization was successful, `false` otherwise.</returns>
        protected internal override bool OnInstanceCreate(ulong xrInstance)
        {
            return base.OnInstanceCreate(xrInstance);
        }

        /// <summary>
        /// This method triggers the teardown process for all support classes.
        /// </summary>
        /// <param name="xrInstance">The handle of the OpenXR instance being destroyed.</param>
        protected internal override void OnInstanceDestroy(ulong xrInstance)
        {
            base.OnInstanceDestroy(xrInstance);

            // Clear the callback to prevent messages after the instance is destroyed.
            callback = null;
            SetCallback(null);

        }

        [AOT.MonoPInvokeCallback(typeof(DebugCallbackDelegate))]
        static void DebugCallback(string msg) => Debug.Log($"{k_DebugLogPrefix}{msg}");

        /// <summary>
        /// P/Invoke declaration for the native function that sets the debug utils callback.
        /// </summary>
        [DllImport(InternalConstants.openXRLibrary, EntryPoint = "DebugUtils_SetCallback")]
        static extern void DebugUtilsSetCallback(DebugCallbackDelegate callback);

        /// <summary>
        /// P/Invoke declaration for the native function that sets the debug utils configuration.
        /// </summary>
        [DllImport(InternalConstants.openXRLibrary, EntryPoint = "DebugUtils_SetConfig")]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool DebugUtilsSetConfig(ulong messageSeverity, ulong messageType);

#if UNITY_EDITOR
        [CustomEditor(typeof(DebugUtilsFeature))]
        internal class Editor : UnityEditor.Editor
        {
            const int k_EverythingMaskValue = -1;
            const string k_DebugUtilsMessageSeverityLabel = "Message Severity";
            const string k_DebugUtilsMesssageTypeLabel = "Message Type";

            const string k_DebugUtilsMessageSeverityTooltip = "Configures the XR_EXT_debug_utils extension to filter log messages for specific severity levels.";
            const string k_DebugUtilsMessageTypeTooltip = "Configures the XR_EXT_debug_utils extension to filter for specific types of log messages";

            /// <summary>
            /// Renders the custom inspector GUI for the Debug Utils Feature.
            /// </summary>
            public override void OnInspectorGUI()
            {
                var feature = (DebugUtilsFeature)target;
                var debugUtilsMessageSeverityContent = new GUIContent(k_DebugUtilsMessageSeverityLabel, k_DebugUtilsMessageSeverityTooltip);
                var debugUtilsMessageTypeContent = new GUIContent(k_DebugUtilsMesssageTypeLabel, k_DebugUtilsMessageTypeTooltip);

                EditorGUI.indentLevel++;
                var debugUtilsMessageSeverity = (MessageSeverity)EditorGUILayout.EnumFlagsField(debugUtilsMessageSeverityContent, feature.messageSeverity);
                var debugUtilsMessageType = (MessageType)EditorGUILayout.EnumFlagsField(debugUtilsMessageTypeContent, feature.messageType);
                EditorGUI.indentLevel--;

                // -1 is when user selects the default "Everything" option.
                if ((int)debugUtilsMessageSeverity == k_EverythingMaskValue)
                    debugUtilsMessageSeverity = k_AllMessageSeverities;

                // -1 is when user selects the default "Everything" option.
                if ((int)debugUtilsMessageType == k_EverythingMaskValue)
                    debugUtilsMessageType = k_AllMessageTypes;

                feature.messageSeverity = debugUtilsMessageSeverity;
                feature.messageType = debugUtilsMessageType;
            }
        }
#endif
    }
}
