#if CPP
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Uuvr.VrCamera;

public class VrCameraManager: MonoBehaviour
{

    private const string NuclearOptionMainCameraName = "Main Camera";
    private const string NuclearOptionMenuCameraName = "Menu Camera";

    public static HashSet<Camera> IgnoredCameras = new();
    private void Start()
    {
        
        
    }
    
    private void Update() // Todo: Make me behave on events if possible
    {
        Camera[] cameras = new Camera[Camera.allCamerasCount];
        Camera.GetAllCameras(cameras);

        foreach (var camera in cameras)
        {
            var gameObject = camera.gameObject;
            if (gameObject.name is not (NuclearOptionMainCameraName or NuclearOptionMenuCameraName) || IgnoredCameras.Contains(camera)) continue;
            HandleChildCameras(camera);
            gameObject.AddComponent<VrCamera>();
            IgnoredCameras.Add(camera);
        }
    }

    private void HandleChildCameras(Camera parentCamera)
    {
        foreach (var child in parentCamera.GetComponentsInChildren<Camera>())
        {
            if (child != parentCamera && !IgnoredCameras.Contains(child)) 
                child.gameObject.AddComponent<StereoCamera>();
        }
    }
}
