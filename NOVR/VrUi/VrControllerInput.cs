using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace NOVR.VrUi
{
    internal static class VrControllerInput
    {
        private static bool _leftTriggerWasPressedCache;
        private static bool _rightTriggerWasPressedCache;

        private static InputAction? _rightAimPos;
        private static InputAction? _rightAimRot;
        private static InputAction? _leftAimPos;
        private static InputAction? _leftAimRot;
        private static InputAction? _rightTrigger;
        private static InputAction? _leftTrigger;
        private static InputAction? _headPos;
        private static InputAction? _headRot;
        private static bool _actionsInitialized;

        // Frame cache — all raw tracking data read once per frame
        private static int _cachedFrame = -1;
        private static bool _rightValid, _leftValid, _headValid;
        private static Vector3 _rawRightPos, _rawLeftPos, _rawHeadPos;
        private static Quaternion _rawRightRot, _rawLeftRot, _rawHeadRot;
        private static float _rawRightTrigger, _rawLeftTrigger;

        // Per-frame edge detection — GetTriggerWasPressedThisFrame and
        // GetTriggerWasReleasedThisFrame are idempotent within a frame so a
        // consumer (e.g. VrUiCursor) can poll them from multiple call sites in
        // the same Update without the second call seeing a cache the first
        // call already advanced.
        private static int _triggerEdgeFrame = -1;
        private static bool _leftTriggerPressedThisFrame;
        private static bool _rightTriggerPressedThisFrame;
        private static bool _leftTriggerReleasedThisFrame;
        private static bool _rightTriggerReleasedThisFrame;

        // Filtered tracking-space poses
        private static Vector3 _filtRightPos, _filtLeftPos;
        private static Quaternion _filtRightRot, _filtLeftRot;
        private static bool _filtInitialized;

        // Filter instances (lazy-created)
        private static OneEuroQuaternionFilter? _rightRotFilter;
        private static OneEuroQuaternionFilter? _leftRotFilter;

        // XR Rig transform (lazy, refreshed periodically)
        private static Transform? _xrRig;
        private static int _rigSearchFrame;

        // Throttled diagnostic logging
        private static float _lastDiagLogTime = -100f;
        private const float DiagLogInterval = 1f;

        // --- Internal: cache/refresh poses once per frame ---
        private static void EnsureFrame()
        {
            if (Time.frameCount == _cachedFrame)
                return;
            _cachedFrame = Time.frameCount;

            EnsureActions();

            // Read raw tracking data
            _rightValid = TryReadRawPose(_rightAimPos, _rightAimRot, out _rawRightPos, out _rawRightRot);
            _leftValid = TryReadRawPose(_leftAimPos, _leftAimRot, out _rawLeftPos, out _rawLeftRot);
            _headValid = TryReadRawPose(_headPos, _headRot, out _rawHeadPos, out _rawHeadRot);
            _rawRightTrigger = TryReadFloat(_rightTrigger);
            _rawLeftTrigger = TryReadFloat(_leftTrigger);

            // Initialize filters on first valid data
            if (!_filtInitialized)
            {
                if (_rightValid)
                {
                    _rightRotFilter = new OneEuroQuaternionFilter(1.2f, 0.15f, 1f);
                    _rightRotFilter.Reset(_rawRightRot);
                    _filtRightRot = _rawRightRot;
                    _filtRightPos = _rawRightPos;
                }
                if (_leftValid)
                {
                    _leftRotFilter = new OneEuroQuaternionFilter(1.2f, 0.15f, 1f);
                    _leftRotFilter.Reset(_rawLeftRot);
                    _filtLeftRot = _rawLeftRot;
                    _filtLeftPos = _rawLeftPos;
                }
                _filtInitialized = true;
            }

            // Apply filters
            float dt = Time.unscaledDeltaTime;

            if (_rightValid)
            {
                if (_rightRotFilter == null)
                {
                    _rightRotFilter = new OneEuroQuaternionFilter(1.2f, 0.15f, 1f);
                    _rightRotFilter.Reset(_rawRightRot);
                    _filtRightRot = _rawRightRot;
                    _filtRightPos = _rawRightPos;
                }
                else
                {
                    _filtRightRot = _rightRotFilter.Filter(_rawRightRot, dt);
                    _filtRightPos = FilterPositionEma(_rawRightPos, _filtRightPos, dt);
                }
            }
            if (_leftValid)
            {
                if (_leftRotFilter == null)
                {
                    _leftRotFilter = new OneEuroQuaternionFilter(1.2f, 0.15f, 1f);
                    _leftRotFilter.Reset(_rawLeftRot);
                    _filtLeftRot = _rawLeftRot;
                    _filtLeftPos = _rawLeftPos;
                }
                else
                {
                    _filtLeftRot = _leftRotFilter.Filter(_rawLeftRot, dt);
                    _filtLeftPos = FilterPositionEma(_rawLeftPos, _filtLeftPos, dt);
                }
            }

            // Refresh XR rig periodically
            RefreshRig();

            // Throttled diagnostic — logs raw vs filtered pose once per second
            // Gated behind VerboseDiagnostics to avoid string allocs and Camera.main queries.
            if (ModConfiguration.Instance != null && ModConfiguration.Instance.VerboseDiagnostics.Value)
            {
                float now = Time.unscaledTime;
                if (now - _lastDiagLogTime > DiagLogInterval)
                {
                    _lastDiagLogTime = now;
                    string msg = $"[VrControllerInput] R_valid={_rightValid} rawPos={_rawRightPos} filtPos={_filtRightPos} | L_valid={_leftValid} rawPos={_rawLeftPos} filtPos={_filtLeftPos} | headValid={_headValid} rawHead={_rawHeadPos} rig={(_xrRig != null ? _xrRig.name : "<null>")} camMain={(Camera.main != null ? Camera.main.name : "<null>")} camParent={(Camera.main != null && Camera.main.transform.parent != null ? Camera.main.transform.parent.name : "<null>")}";
                    if (NOVRPlugin.LogSource != null) NOVRPlugin.LogSource.LogMessage(msg);
                    else Debug.Log(msg);
                }
            }
        }

        private static Vector3 FilterPositionEma(Vector3 raw, Vector3 smoothed, float dt)
        {
            if (dt <= 0f) return raw;
            float cutoff = 8f;
            float alpha = 1f - Mathf.Exp(-dt * cutoff);
            return Vector3.Lerp(smoothed, raw, alpha);
        }

        private static bool TryReadRawPose(InputAction? posAction, InputAction? rotAction,
            out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            if (posAction == null || rotAction == null) return false;
            try
            {
                Vector3 p = posAction.ReadValue<Vector3>();
                Quaternion r = rotAction.ReadValue<Quaternion>();
                if (p.sqrMagnitude > 0.0001f)
                {
                    pos = p;
                    rot = r;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static float TryReadFloat(InputAction? action)
        {
            if (action == null) return 0f;
            try { return action.ReadValue<float>(); }
            catch { return 0f; }
        }

        private static void RefreshRig()
        {
            if (_xrRig != null && Time.frameCount - _rigSearchFrame <= 120)
                return;
            _rigSearchFrame = Time.frameCount;
            var cam = Camera.main;
            if (cam != null)
                _xrRig = cam.transform.parent ?? cam.transform;
        }

        // --- Existing methods (unchanged) ---

        private static void EnsureActions()
        {
            if (_actionsInitialized) return;
            _actionsInitialized = true;

            _rightAimPos = new InputAction(binding: "<XRController>{RightHand}/pointerPosition");
            _rightAimPos.AddBinding("<XRController>{RightHand}/devicePosition");
            _rightAimRot = new InputAction(binding: "<XRController>{RightHand}/pointerRotation");
            _rightAimRot.AddBinding("<XRController>{RightHand}/deviceRotation");
            _leftAimPos = new InputAction(binding: "<XRController>{LeftHand}/pointerPosition");
            _leftAimPos.AddBinding("<XRController>{LeftHand}/devicePosition");
            _leftAimRot = new InputAction(binding: "<XRController>{LeftHand}/pointerRotation");
            _leftAimRot.AddBinding("<XRController>{LeftHand}/deviceRotation");

            _rightTrigger = new InputAction(type: InputActionType.Button, binding: "<XRController>{RightHand}/triggerPressed");
            _rightTrigger.AddBinding("<XRController>{RightHand}/trigger");
            _leftTrigger = new InputAction(type: InputActionType.Button, binding: "<XRController>{LeftHand}/triggerPressed");
            _leftTrigger.AddBinding("<XRController>{LeftHand}/trigger");

            _headPos = new InputAction(binding: "<XRHMD>/centerEyePosition");
            _headPos.AddBinding("<XRHMD>/devicePosition");
            _headRot = new InputAction(binding: "<XRHMD>/centerEyeRotation");
            _headRot.AddBinding("<XRHMD>/deviceRotation");

            _rightAimPos.Enable();
            _rightAimRot.Enable();
            _leftAimPos.Enable();
            _leftAimRot.Enable();
            _rightTrigger.Enable();
            _leftTrigger.Enable();
            _headPos.Enable();
            _headRot.Enable();

            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private static void OnDeviceChange(UnityEngine.InputSystem.InputDevice device, InputDeviceChange change)
        {
            if (ModConfiguration.Instance != null && ModConfiguration.Instance.VerboseDiagnostics.Value)
                Debug.Log($"[VrControllerInput] DeviceChange: {change} name={device.name} layout={device.layout}");
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed)
                LogDiagnostics();
        }

        /// <summary>
        /// Reads the head pose from the cached frame data.
        /// Returns false if no head pose is available.
        /// </summary>
        public static bool TryGetHeadPose(out Vector3 position, out Quaternion rotation)
        {
            EnsureFrame();
            if (_headValid)
            {
                position = _rawHeadPos;
                rotation = _rawHeadRot;
                return true;
            }
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        /// <summary>
        /// Returns the filtered controller pose in tracking space.
        /// </summary>
        public static bool TryGetPose(XRNode hand, out Vector3 position, out Quaternion rotation)
        {
            EnsureFrame();

            bool isLeft = hand == XRNode.LeftHand;
            bool valid = isLeft ? _leftValid : _rightValid;

            if (valid)
            {
                position = isLeft ? _filtLeftPos : _filtRightPos;
                rotation = isLeft ? _filtLeftRot : _filtRightRot;
                return true;
            }

            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        /// <summary>
        /// Converts a filtered controller pose from tracking space to world space
        /// using the XR rig transform. No headset data is used — the controller
        /// and headset are in the same tracking space, so we transform directly.
        /// </summary>
        public static bool TryGetPoseInWorldSpace(XRNode hand, Vector3 cameraWorldPos,
            out Vector3 worldPosition, out Quaternion worldRotation)
        {
            EnsureFrame();

            bool isLeft = hand == XRNode.LeftHand;
            bool valid = isLeft ? _leftValid : _rightValid;
            Vector3 trackingPos = isLeft ? _filtLeftPos : _filtRightPos;
            Quaternion trackingRot = isLeft ? _filtLeftRot : _filtRightRot;

            if (valid)
            {
                // Position: controller at head-relative offset from the calibrated headset
                // world position. Using NOVRHeadsetData.Translation (rather than Camera.main's
                // transform) keeps the controller's world position stable when Camera.main
                // changes between scenes (e.g. menu → mission picker swap).
                // Subtracting rawHeadPos removes the HMD height, so a controller at
                // chest height IRL (trackingPos.y ≈ 0.8) appears at chest height
                // in game rather than at eye-level + 0.8.
                // Rotation: controller's own tracking orientation — not multiplied by
                // headset rotation, so the ray direction does not follow the HMD / aircraft.
                worldPosition = NOVRHeadsetData.Translation + (trackingPos - _rawHeadPos);
                worldRotation = trackingRot;
                return true;
            }

            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            return false;
        }

        internal static void LogDiagnostics()
        {
            EnsureFrame();

            var sb = new StringBuilder();
            sb.AppendLine("=== VrControllerInput Diagnostics ===");

            sb.AppendLine("--- InputSystem.devices ---");
            foreach (var d in InputSystem.devices)
            {
                bool isXR = d is XRController;
                sb.AppendLine($"  {(isXR ? "[XR]" : "     ")} name={d.name} layout={d.layout} usages=[{string.Join(",", d.usages)}] desc.interface={d.description.interfaceName} desc.product={d.description.product}");
            }

            sb.AppendLine("--- InputSystem.GetUnsupportedDevices ---");
            foreach (var d in InputSystem.GetUnsupportedDevices())
                sb.AppendLine($"  interface={d.interfaceName} product={d.product} manufacturer={d.manufacturer}");

            sb.AppendLine("--- XRInputSubsystem ---");
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
                sb.AppendLine($"  running={s.running}");

            try
            {
                var openXRSettingsType = System.Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
                if (openXRSettingsType != null)
                {
                    var instanceProp = openXRSettingsType.GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (instanceProp != null)
                    {
                        var instance = instanceProp.GetValue(null);
                        var featuresProp = openXRSettingsType.GetProperty("features");
                        if (featuresProp != null && instance != null)
                        {
                            var features = featuresProp.GetValue(instance) as System.Collections.IList;
                            if (features != null)
                            {
                                sb.AppendLine("--- OpenXR Features ---");
                                foreach (var f in features)
                                {
                                    var nameProp = f.GetType().GetProperty("name");
                                    var enabledProp = f.GetType().GetProperty("enabled");
                                    string fName = nameProp?.GetValue(f)?.ToString() ?? "(no name)";
                                    bool fEnabled = enabledProp != null && (bool)(enabledProp.GetValue(f) ?? false);
                                    sb.AppendLine($"  {fName} enabled={fEnabled}");
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            sb.AppendLine("--- Filtered poses (tracking space) ---");
            sb.AppendLine($"  RightHand pos={_filtRightPos:F3} rot={_filtRightRot.eulerAngles:F3}");
            sb.AppendLine($"  LeftHand  pos={_filtLeftPos:F3} rot={_filtLeftRot.eulerAngles:F3}");
            sb.AppendLine($"  RightHand trigger={_rawRightTrigger:F2}");
            sb.AppendLine($"  LeftHand  trigger={_rawLeftTrigger:F2}");

            sb.AppendLine("--- Rig transform ---");
            if (_xrRig != null)
                sb.AppendLine($"  name={_xrRig.name} pos={_xrRig.position:F3} rot={_xrRig.rotation.eulerAngles:F3}");
            else
                sb.AppendLine("  (null)");

            sb.AppendLine("=== End Diagnostics ===");
            Debug.Log(sb.ToString());
        }

        public static bool GetTrigger(XRNode hand)
        {
            EnsureFrame();
            return hand == XRNode.LeftHand ? _rawLeftTrigger > 0.5f : _rawRightTrigger > 0.5f;
        }

        public static bool GetTriggerWasPressedThisFrame(XRNode hand)
        {
            EnsureTriggerEdgeCache();
            return hand == XRNode.LeftHand ? _leftTriggerPressedThisFrame : _rightTriggerPressedThisFrame;
        }

        public static bool GetTriggerWasReleasedThisFrame(XRNode hand)
        {
            EnsureTriggerEdgeCache();
            return hand == XRNode.LeftHand ? _leftTriggerReleasedThisFrame : _rightTriggerReleasedThisFrame;
        }

        private static void EnsureTriggerEdgeCache()
        {
            EnsureFrame();
            if (Time.frameCount == _triggerEdgeFrame)
                return;

            _triggerEdgeFrame = Time.frameCount;

            bool leftNow = _rawLeftTrigger > 0.5f;
            bool rightNow = _rawRightTrigger > 0.5f;

            _leftTriggerPressedThisFrame = leftNow && !_leftTriggerWasPressedCache;
            _rightTriggerPressedThisFrame = rightNow && !_rightTriggerWasPressedCache;
            _leftTriggerReleasedThisFrame = !leftNow && _leftTriggerWasPressedCache;
            _rightTriggerReleasedThisFrame = !rightNow && _rightTriggerWasPressedCache;

            _leftTriggerWasPressedCache = leftNow;
            _rightTriggerWasPressedCache = rightNow;
        }

        public static bool TryGetDominantHand(out Vector3 position, out Quaternion rotation, out bool triggerPressed)
        {
            EnsureFrame();

            Vector3 camWorldPos = APIBus.CockpitHudCamera != null
                ? APIBus.CockpitHudCamera.transform.position
                : Vector3.zero;

            bool leftValid = TryGetPoseInWorldSpace(XRNode.LeftHand, camWorldPos, out var leftPos, out var leftRot);
            bool rightValid = TryGetPoseInWorldSpace(XRNode.RightHand, camWorldPos, out var rightPos, out var rightRot);

            if (leftValid && !rightValid)
            {
                position = leftPos;
                rotation = leftRot;
                triggerPressed = _rawLeftTrigger > 0.5f;
                return true;
            }
            if (rightValid && !leftValid)
            {
                position = rightPos;
                rotation = rightRot;
                triggerPressed = _rawRightTrigger > 0.5f;
                return true;
            }

            if (!leftValid && !rightValid)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                triggerPressed = false;
                return false;
            }

            bool rightTrigger = _rawRightTrigger > 0.5f;
            bool leftTrigger = _rawLeftTrigger > 0.5f;

            if (rightTrigger)
            {
                position = rightPos;
                rotation = rightRot;
                triggerPressed = true;
                return true;
            }
            if (leftTrigger)
            {
                position = leftPos;
                rotation = leftRot;
                triggerPressed = true;
                return true;
            }

            position = rightPos;
            rotation = rightRot;
            triggerPressed = false;
            return true;
        }
    }
}
