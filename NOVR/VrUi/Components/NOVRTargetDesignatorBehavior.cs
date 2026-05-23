using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRTargetDesignatorBehavior : UIRenderedCanvasBehavior
{
    private float _offset = 1000;

    private void Update()
    {
        var uiCam = APIBus.CockpitHudReference;
        
        transform.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, uiCam.transform.rotation, 1.1f);
        transform.position = transform.forward * _offset;
    }
}