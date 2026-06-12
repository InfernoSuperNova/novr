using System;
using System.Reflection;
using NOVR.UnityTypesHelper;
using UnityEngine;

namespace NOVR;

[Flags]
public enum CalibrationAxes : byte
{
    None = 0,
    Pitch = 1,
    Yaw = 2,
    Roll = 4,
    All = Pitch | Yaw | Roll,
    X = Pitch,
    Y = Yaw,
    Z = Roll,
}

public class NOVRPoseDriver : NOVRBehaviour
{
    
    protected override void OnBeforeRender()
    {
        base.OnBeforeRender();
        UpdateTransform();
    }

    private void Update()
    {
        UpdateTransform();
    }

    private void LateUpdate()
    {
        UpdateTransform();
    }

    private void UpdateTransform()
    {
        transform.localRotation = NOVRHeadsetData.Rotation;
        transform.localPosition = NOVRHeadsetData.Translation;
    }

   
}
