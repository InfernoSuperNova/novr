namespace NOVR.VrUi.SpecialBehavior;

public class NOVRTargetDesignatorBehavior : UIRenderedCanvasBehavior
{
    private float _offset = 1000;

    private void Update()
    {
        var uiCam = EventBus.CockpitHudReference;
        transform.position = uiCam.transform.forward * _offset;
        transform.rotation = uiCam.transform.rotation;
    }
}