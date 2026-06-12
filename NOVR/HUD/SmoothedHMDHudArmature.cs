using System;
using UnityEngine;

namespace NOVR.HUD;


// HMD hud armature but smoothed
public class SmoothedHMDHudArmature : SceneSingleton<SmoothedHMDHudArmature>
{
    private const float SmoothingFactor = 10f; // TODO: Make config variable
    protected override void Awake()
    {
        i = this;
    }

    private void OnDestroy()
    {
        if (i == this) i = null;
    }

    protected void Update()
    {
        var targetTransform = SceneSingleton<HMDHudArmature>.i.transform;
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetTransform.localRotation, Mathf.Clamp(Time.deltaTime * SmoothingFactor, 0, 1));
    }
}
