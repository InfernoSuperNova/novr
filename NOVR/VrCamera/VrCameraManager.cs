#if CPP
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NOVR.VrCamera;

public class VrCameraManager: MonoBehaviour
{
    private const string NuclearOptionMainCameraName = "Main Camera";
    private const string NuclearOptionMenuCameraName = "Menu Camera";
    private const string VrCameraChildName = "NOVR Main Camera";
    private const float ActiveCameraScanIntervalSeconds = 0.25f;
    private const float IdleCameraScanIntervalSeconds = 2f;
    private static readonly string[] TrackedChildNames = {"cockpitRenderer", "postProcessingRenderer"};

    public static HashSet<Camera> IgnoredCameras = new();

    private bool _cameraScanDirty = true;
    private bool _hasTrackedMainCamera;
    private float _nextCameraScanTime;

    private void Awake()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _cameraScanDirty = true;
        _hasTrackedMainCamera = false;
        _nextCameraScanTime = 0f;
    }

    private void Update()
    {
        if (!_cameraScanDirty && Time.unscaledTime < _nextCameraScanTime)
        {
            return;
        }

        _cameraScanDirty = false;
        _nextCameraScanTime = Time.unscaledTime +
                              (_hasTrackedMainCamera
                                  ? IdleCameraScanIntervalSeconds
                                  : ActiveCameraScanIntervalSeconds);

        ScanCameras();
    }

    private void ScanCameras()
    {
        Camera[] cameras = new Camera[Camera.allCamerasCount];
        Camera.GetAllCameras(cameras);

        _hasTrackedMainCamera = false;
        foreach (var camera in cameras)
        {
            if (camera == null)
            {
                continue;
            }

            var gameObject = camera.gameObject;
            if (gameObject.name is not (NuclearOptionMainCameraName or NuclearOptionMenuCameraName))
            {
                continue;
            }

            if (gameObject.name == NuclearOptionMainCameraName)
            {
                var existingTrackedCamera = GetTrackedMainCamera(gameObject);
                if (existingTrackedCamera != null)
                {
                    EnsureTrackedMainCameraRig(camera, existingTrackedCamera);
                    _hasTrackedMainCamera = true;
                    continue;
                }
            }

            if (IgnoredCameras.Contains(camera))
            {
                continue;
            }

            if (gameObject.name == NuclearOptionMainCameraName)
            {
                SetUpMainCameraRig(camera);
                _hasTrackedMainCamera = true;
            }
            else
            {
                HandleChildCameras(camera);
                if (gameObject.GetComponent<VrCamera>() == null)
                {
                    gameObject.AddComponent<VrCamera>();
                }
                IgnoredCameras.Add(camera);
            }
        }
    }

    public static Camera? GetTrackedMainCamera(GameObject gameCameraRoot)
    {
        var trackedCameraTransform = gameCameraRoot.transform.Find(VrCameraChildName);
        return trackedCameraTransform != null ? trackedCameraTransform.GetComponent<Camera>() : null;
    }

    private void SetUpMainCameraRig(Camera rootCamera)
    {
        HandleChildCameras(rootCamera);

        var existingTrackedCamera = GetTrackedMainCamera(rootCamera.gameObject);
        if (existingTrackedCamera != null)
        {
            EnsureTrackedMainCameraRig(rootCamera, existingTrackedCamera);
            return;
        }

        var trackedCameraObject = new GameObject(VrCameraChildName);
        trackedCameraObject.transform.SetParent(rootCamera.transform, false);
        trackedCameraObject.tag = rootCamera.tag;

        var trackedCamera = trackedCameraObject.AddComponent<Camera>();
        trackedCamera.CopyFrom(rootCamera);
        var additionalCameraData = AdditionalCameraData.Create(trackedCamera);
        additionalCameraData?.SetRenderTypeBase();
        additionalCameraData?.SetAllowXrRendering(true);
        //additionalCameraData.GetCameraStack().AddRange(rootCamera.GetComponent<AdditionalCameraData>().GetCameraStack());

        var universalAdditionalCameraData = trackedCameraObject.GetComponent<UniversalAdditionalCameraData>();
        var rootUniversalAdditionalCameraData = rootCamera.GetComponent<UniversalAdditionalCameraData>();
        
        if (universalAdditionalCameraData != null && rootUniversalAdditionalCameraData != null)
        {
            AddUniqueCameras(universalAdditionalCameraData.cameraStack, rootUniversalAdditionalCameraData.cameraStack);
        }
        
        
        var rootAudioListener = rootCamera.GetComponent<AudioListener>();
        if (rootAudioListener != null)
        {
            var trackedAudioListener = trackedCameraObject.AddComponent<AudioListener>();
            trackedAudioListener.enabled = rootAudioListener.enabled;
            rootAudioListener.enabled = false;
        }

        rootCamera.tag = "Untagged";
        rootCamera.enabled = false;
        ReparentTrackedChildren(rootCamera.transform, trackedCameraObject.transform);
        NormalizeTrackedCameraStack(rootCamera, trackedCamera);

        trackedCameraObject.AddComponent<VrCamera>();

        IgnoredCameras.Add(rootCamera);
        IgnoredCameras.Add(trackedCamera);
    }

    private static void EnsureTrackedMainCameraRig(Camera rootCamera, Camera trackedCamera)
    {
        if (rootCamera.enabled)
        {
            rootCamera.enabled = false;
        }

        if (rootCamera.CompareTag("MainCamera"))
        {
            rootCamera.tag = "Untagged";
        }

        if (!trackedCamera.enabled)
        {
            trackedCamera.enabled = true;
        }

        if (!trackedCamera.CompareTag("MainCamera"))
        {
            trackedCamera.tag = "MainCamera";
        }

        if (trackedCamera.GetComponent<VrCamera>() == null)
        {
            trackedCamera.gameObject.AddComponent<VrCamera>();
        }

        ReparentTrackedChildren(rootCamera.transform, trackedCamera.transform);
        NormalizeTrackedCameraStack(rootCamera, trackedCamera);

        IgnoredCameras.Add(rootCamera);
        IgnoredCameras.Add(trackedCamera);
    }

    private void HandleChildCameras(Camera parentCamera)
    {
        foreach (var child in parentCamera.GetComponentsInChildren<Camera>())
        {
            if (child != parentCamera && !IgnoredCameras.Contains(child)) 
                child.gameObject.AddComponent<StereoCamera>();
        }
    }

    private static void ReparentTrackedChildren(Transform rootCameraTransform, Transform trackedCameraTransform)
    {
        foreach (var childName in TrackedChildNames)
        {
            var child = rootCameraTransform.Find(childName);
            if (child != null)
            {
                child.SetParent(trackedCameraTransform, false);
            }
        }
    }

    private static void NormalizeTrackedCameraStack(Camera rootCamera, Camera trackedCamera)
    {
        var additionalCameraData = trackedCamera.GetComponent<UniversalAdditionalCameraData>();
        if (additionalCameraData == null)
        {
            return;
        }

        additionalCameraData.renderType = CameraRenderType.Base;
        additionalCameraData.allowXRRendering = true;

        var stack = additionalCameraData.cameraStack;
        if (stack == null)
        {
            return;
        }

        RemoveInvalidOrDuplicateStackEntries(stack, rootCamera, trackedCamera);

        foreach (var childName in TrackedChildNames)
        {
            var child = trackedCamera.transform.Find(childName);
            var childCamera = child != null ? child.GetComponent<Camera>() : null;
            if (childCamera == null)
            {
                continue;
            }

            var childData = childCamera.GetComponent<UniversalAdditionalCameraData>();
            if (childData != null)
            {
                childData.renderType = CameraRenderType.Overlay;
            }

            AddUniqueCamera(stack, childCamera);
        }
    }

    private static void AddUniqueCameras(List<Camera> destination, IEnumerable<Camera> source)
    {
        foreach (var camera in source)
        {
            AddUniqueCamera(destination, camera);
        }
    }

    private static void AddUniqueCamera(List<Camera> stack, Camera camera)
    {
        if (camera == null || stack.Contains(camera))
        {
            return;
        }

        stack.Add(camera);
    }

    private static void RemoveInvalidOrDuplicateStackEntries(List<Camera> stack, Camera rootCamera, Camera trackedCamera)
    {
        var seen = new HashSet<Camera>();
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            var stackCamera = stack[i];
            if (stackCamera == null ||
                stackCamera == rootCamera ||
                stackCamera == trackedCamera ||
                !seen.Add(stackCamera))
            {
                stack.RemoveAt(i);
            }
        }
    }
}
