using System.Runtime.InteropServices;

namespace UnityEngine.XR.OpenXR
{
    public partial class ApiLayers
    {
        /// <summary>
        /// Defines an interface for platform-specific behaviors related to OpenXR API Layer management.
        /// Implementations of this interface handle differences in how layers are discovered and
        /// configured on various operating systems (e.g., Windows vs. Android).
        /// </summary>
        internal interface IPlatformSupport : ISupport
        {
            /// <summary>
            /// Gets the directory path where API layer files are stored or should be copied to.
            /// The location can differ between the Unity Editor and a built application.
            /// </summary>
            /// <returns>The path to the API layers directory for the current context.</returns>
            string GetApiLayersDir();

            /// <summary>
            /// Gets the directory path where bundled API layers (included with the OpenXR package) are stored for this platform.
            /// </summary>
            /// <returns>The path to the bundled API layers directory.</returns>
            string GetBundleDir();

            /// <summary>
            /// Gets the supported architectures for this platform.
            /// </summary>
            /// <returns>The supported architectures for this platform.</returns>
            Architecture[] GetSupportedArchitectures();

            /// <summary>
            /// Gets the supported library extensions for this platform.
            /// </summary>
            /// <returns>The supported extensions for this platform.</returns>
            string[] GetSupportedExtensions();
        }
    }

}
