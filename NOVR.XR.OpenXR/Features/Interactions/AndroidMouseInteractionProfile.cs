using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XR;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.XR.OpenXR.Features.Interactions
{
    /// <summary>
    /// This <see cref="OpenXRInteractionFeature"/> enables the use of Android mouse interaction profiles in OpenXR.
    /// It provides support for mouse and trackpad devices in Android XR applications, enabling 3D pointer ray interactions.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(UiName = "Android Mouse Interaction Profile",
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "Unity",
        Desc = "Add mouse interaction profile for an Android mouse pointer device.",
        DocumentationLink = Constants.k_DocumentationManualURL + "features/androidmouseinteraction.html",
        OpenxrExtensionStrings = extensionString,
        Version = "0.0.1",
        Category = UnityEditor.XR.OpenXR.Features.FeatureCategory.Interaction,
        FeatureId = featureId)]
#endif
    public class AndroidMouseInteractionProfile : OpenXRInteractionFeature
    {
        [StructLayout(LayoutKind.Sequential, Size = k_SizeInBytes)]
        struct AndroidMouseInteractionState : IInputStateTypeInfo
        {
            const int k_SizeInBytes = 60 + sizeof(bool) * 3 + sizeof(float) * 2;
#if UNITY_INPUT_SYSTEM_ENABLE_XR
            [InputControl(layout = "Pose", usage = "Aim")]
            public PoseState aim;
#endif
            [InputControl(layout = "Button", usage = "Click")]
            public bool click;
            [InputControl(layout = "Button", usage = "SecondaryClick")]
            public bool secondaryClick;
            [InputControl(layout = "Button", usage = "TertiaryClick")]
            public bool tertiaryClick;
            [InputControl(layout = "Vector2", usage = "Scroll")]
            public Vector2 scroll;
            public FourCC format => new FourCC('A', 'M', 'I', 'S');
        }

        /// <summary>
        /// The feature id string. This is used to uniquely identify this feature in the OpenXR runtime.
        /// </summary>
        public const string featureId = "com.unity.openxr.feature.input.androidmouse";

        /// <summary>
        /// The OpenXR Extension string. This is used by the OpenXR runtime to check if the extension is available or enabled.
        /// </summary>
        public const string extensionString = "XR_ANDROID_mouse_interaction";

        /// <summary>
        /// An <see cref="InputDevice"/> representing an Android mouse that supports interaction in OpenXR.
        /// </summary>
        [Preserve, InputControlLayout(displayName = "Android Mouse Interaction (OpenXR)", stateType = typeof(AndroidMouseInteractionState), isGenericTypeOfDevice = true)]
        public class AndroidMouseInteraction : UnityEngine.InputSystem.InputDevice
        {
#if UNITY_INPUT_SYSTEM_ENABLE_XR
            /// <summary>
            /// A 3D pointer ray pose for interaction. Position is typically at the user's head location,
            /// and orientation where -Z direction is the forward aiming direction. Relative mouse movement
            /// computes movement along a sphere around the user's head. Position supports depth movement
            /// via scroll input (positive scroll moves forward, negative moves backward).
            /// </summary>
            public PoseControl aim { get; private set; }
#endif

            /// <summary>
            /// Primary mouse button (select/click). Returns <c>true</c> when the primary button is pressed.
            /// </summary>
            [InputControl(layout = "Button", usage = "Click")]
            public ButtonControl click { get; private set; }

            /// <summary>
            /// Secondary mouse button (right-click). Returns <c>true</c> when the secondary button is pressed.
            /// </summary>
            public ButtonControl secondaryClick { get; private set; }

            /// <summary>
            /// Tertiary mouse button (scroll wheel button or middle button). Returns <c>true</c> when pressed.
            /// </summary>
            public ButtonControl tertiaryClick { get; private set; }

            /// <summary>
            /// Mouse scroll wheel input. 2D continuous input with X and Y components in range -1.0 to 1.0.
            /// The Y component represents vertical scrolling (where -1.0 is scroll down and 1.0 is scroll up).
            /// The X component represents horizontal scrolling. Used for depth movement and interaction scrolling.
            /// </summary>
            public Vector2Control scroll { get; private set; }

            /// <inheritdoc/>
            protected override void FinishSetup()
            {
                base.FinishSetup();
#if UNITY_INPUT_SYSTEM_ENABLE_XR
                aim = GetChildControl<PoseControl>("aim");
#endif
                click = GetChildControl<ButtonControl>("click");
                secondaryClick = GetChildControl<ButtonControl>("secondaryClick");
                tertiaryClick = GetChildControl<ButtonControl>("tertiaryClick");
                scroll = GetChildControl<Vector2Control>("scroll");
            }
        }

        /// <summary>
        /// The interaction profile path string for the Android mouse interaction profile.
        /// </summary>
        public const string profile = "/interaction_profiles/android/mouse_interaction_android";

        /// <summary>
        /// The OpenXR input path for the aim pose. Used for 3D pointer ray interactions.
        /// </summary>
        public const string aimPath = "/input/aim/pose";

        /// <summary>
        /// The OpenXR input path for the primary mouse button (select/click).
        /// </summary>
        public const string clickPath = "/input/select/click";

        /// <summary>
        /// The OpenXR input path for the secondary mouse button (right-click).
        /// </summary>
        public const string secondaryClickPath = "/input/secondary_android/click";

        /// <summary>
        /// The OpenXR input path for the tertiary mouse button (scroll wheel button/middle button).
        /// </summary>
        public const string tertiaryClickPath = "/input/tertiary_android/click";

        /// <summary>
        /// The OpenXR input path for the scroll wheel value.
        /// </summary>
        public const string scrollPath = "/input/scroll_android/value";

        const string k_DeviceLocalizedName = "Android Mouse Interaction";
        /// <inheritdoc/>
        protected internal override bool OnInstanceCreate(ulong instance)
        {
            // Requires Android mouse interaction extension
            if (!OpenXRRuntime.IsExtensionEnabled(extensionString))
                return false;

            return base.OnInstanceCreate(instance);
        }

        /// <summary>
        /// Registers the <see cref="AndroidMouseInteraction"/> layout with the Input System.
        /// </summary>
        protected override void RegisterDeviceLayout()
        {
            InputSystem.InputSystem.RegisterLayout(typeof(AndroidMouseInteraction),
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct(k_DeviceLocalizedName));
        }

        /// <summary>
        /// Removes the <see cref="AndroidMouseInteraction"/> layout from the Input System.
        /// </summary>
        protected override void UnregisterDeviceLayout()
        {
            InputSystem.InputSystem.RemoveLayout(nameof(AndroidMouseInteraction));
        }

        /// <summary>
        /// Return device layout string that used for registering device in InputSystem.
        /// </summary>
        /// <returns>Device layout string.</returns>
        protected override string GetDeviceLayoutName()
        {
            return nameof(AndroidMouseInteraction);
        }

        /// <summary>
        /// Return Interaction profile type.
        /// </summary>
        /// <returns>Interaction profile type.</returns>
        protected override InteractionProfileType GetInteractionProfileType()
        {
            return InteractionProfileType.Device;
        }

        /// <inheritdoc/>
        protected override void RegisterActionMapsWithRuntime()
        {
            ActionMapConfig actionMap = new ActionMapConfig
            {
                name = "android-mouse-interaction",
                localizedName = "Android Mouse Interaction",
                desiredInteractionProfile = profile,
                manufacturer = "",
                serialNumber = "",
                deviceInfos = new List<DeviceConfig>
                {
                    new()
                    {
                        characteristics = InputDeviceCharacteristics.None,
                        userPath = "/user/mouse"
                    }
                },
                actions = new List<ActionConfig>
                {
                    new()
                    {
                        name = "aim",
                        localizedName = "Aim Pose",
                        type = ActionType.Pose,
                        bindings = new List<ActionBinding>
                        {
                            new()
                            {
                                interactionPath = aimPath,
                                interactionProfileName = profile
                            }
                        }
                    },
                    new()
                    {
                        name = "click",
                        localizedName = "Click",
                        type = ActionType.Binary,
                        bindings = new List<ActionBinding>
                        {
                            new()
                            {
                                interactionPath = clickPath,
                                interactionProfileName = profile
                            }
                        }
                    },
                    new()
                    {
                        name = "secondaryClick",
                        localizedName = "Secondary Click",
                        type = ActionType.Binary,
                        bindings = new List<ActionBinding>
                        {
                            new()
                            {
                                interactionPath = secondaryClickPath,
                                interactionProfileName = profile
                            }
                        }
                    },
                    new()
                    {
                        name = "tertiaryClick",
                        localizedName = "Tertiary Click",
                        type = ActionType.Binary,
                        bindings = new List<ActionBinding>
                        {
                            new()
                            {
                                interactionPath = tertiaryClickPath,
                                interactionProfileName = profile
                            }
                        }
                    },
                    new()
                    {
                        name = "scroll",
                        localizedName = "Scroll",
                        type = ActionType.Axis2D,
                        bindings = new List<ActionBinding>
                        {
                            new()
                            {
                                interactionPath = scrollPath,
                                interactionProfileName = profile
                            }
                        }
                    }
                }
            };
            AddActionMap(actionMap);
        }
    }
}
