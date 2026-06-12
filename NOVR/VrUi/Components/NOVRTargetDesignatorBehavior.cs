using NOVR.HUD;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRTargetDesignatorBehavior : UIRenderedCanvasBehavior
{
    private float _offset = 1000;


    private void Start()
    {
        transform.SetParent(SceneSingleton<StaticHudArmature>.i.transform, false);
    }

    private void Update()
    {
        var target = SceneSingleton<SmoothedHMDHudArmature>.i.transform;
        
        transform.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, target.rotation, 1.1f);
        transform.position = transform.forward * _offset;
    }
}