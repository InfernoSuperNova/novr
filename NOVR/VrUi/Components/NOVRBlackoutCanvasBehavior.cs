using System;
using NOVR.HUD;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRBlackoutCanvasBehavior : MonoBehaviour
{
    
    protected virtual void Awake()
    {
        var hmdHudArmature = SceneSingleton<HMDHudArmature>.i;
        
        transform.SetParent(hmdHudArmature.transform, false);
        transform.localPosition = Vector3.forward * 1f;
        transform.localRotation = Quaternion.identity;
        
    }
}