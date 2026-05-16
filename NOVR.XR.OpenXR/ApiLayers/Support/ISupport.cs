using System;

namespace UnityEngine.XR.OpenXR
{
    public partial class ApiLayers
    {
        /// <summary>
        /// Defines an interface for objects that provide support for API layers.
        /// This allows for modular logic setup and teardown that support API layers,
        /// such as debug callbacks or configuring layer-specific logging.
        /// </summary>
        /// <remarks>
        /// Implement this interface to provide custom support logic for OpenXR API layers.
        /// The APILayersFeature calls the Setup method before the OpenXR instance creation phase, allowing you to configure
        /// environment variables, register callbacks, or perform other initialization before the OpenXR instance is created.
        /// The APILayersFeature calls the Teardown method on OpenXR instance destruction to clean up any resources created during Setup.
        /// Register your implementation using <see cref="Features.ApiLayersFeature.AddSupport"/> to integrate it with the API layers workflow.
        /// </remarks>
        /// <example>
        /// <para>
        /// This example shows how to create a custom support implementation that logs API layer lifecycle events:
        /// <c>
        /// public class CustomLayerSupport : ApiLayers.ISupport
        /// {
        ///     public void Setup(IntPtr hookGetInstanceProcAddr)
        ///     {
        ///         Debug.Log("Setting up custom API layer support");
        ///         // Configure environment or register callbacks here
        ///     }
        ///
        ///     public void Teardown(ulong xrInstance)
        ///     {
        ///         Debug.Log("Tearing down custom API layer support");
        ///         // Clean up resources here
        ///     }
        /// }
        ///
        /// // Register the support object
        /// ApiLayersFeature.AddSupport(new CustomLayerSupport());
        /// </c>
        /// </para>
        /// </example>
        /// <seealso cref="Features.ApiLayersFeature.AddSupport"/>
        /// <seealso cref="Features.ApiLayersFeature.RemoveSupport"/>
        public interface ISupport
        {
            /// <summary>
            /// Called during the HookGetInstanceProcAddr phase of the ApiLayersFeature, before OnInstanceCreate is called.
            /// Use this method to perform any necessary configuration before the OpenXR instance is created.
            /// </summary>
            /// <remarks>
            /// In this method, you can hook into the OpenXR function chain to intercept calls, set environment
            /// variables needed by a layer, or initialize third-party libraries that interact with the layer.
            /// The hookGetInstanceProcAddr parameter provides access to the function pointer chain, allowing you
            /// to intercept or modify OpenXR function calls if needed for advanced layer support scenarios.
            /// </remarks>
            /// <param name="hookGetInstanceProcAddr">The GetInstanceProcAddr function pointer for potential native setup operations.</param>
            void Setup(IntPtr hookGetInstanceProcAddr);

            /// <summary>
            /// Cleans up resources and configurations during the OnInstanceDestroy phase of the ApiLayersFeature.
            /// Use this method to clean up any resources, environment variables, or configurations
            /// that were created during the Setup phase.
            /// </summary>
            /// <remarks>
            /// Implement this method to perform cleanup operations when the OpenXR instance is being destroyed.
            /// This is the appropriate place to release native resources, unregister callbacks, clear environment
            /// variables, or dispose of any objects that were created during the Setup phase. The xrInstance
            /// parameter provides the handle to the instance being destroyed, which may be needed for certain
            /// cleanup operations that require interaction with the OpenXR runtime.
            /// </remarks>
            /// <param name="xrInstance">The handle of the OpenXR instance being destroyed.</param>
            void Teardown(ulong xrInstance);
        }
    }
}
