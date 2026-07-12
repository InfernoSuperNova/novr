using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRTargetDesignatorBehavior : UIRenderedCanvasBehavior
{
    private float _offset = 1000;

    private void Update()
    {
        if (!TryGetHeadAim(out var rotation, out _))
            return;

        transform.rotation = rotation;
        transform.position = transform.forward * _offset;
    }

    internal static bool TryGetHeadAimRay(out Ray ray)
    {
        return TryGetHeadAim(out _, out ray);
    }

    private static bool TryGetHeadAim(out Quaternion rotation, out Ray ray)
    {
        rotation = Quaternion.identity;
        ray = default;

        var reference = APIBus.CockpitHudReference;
        var camera = APIBus.CockpitHudCamera;
        if (reference == null || camera == null)
            return false;

        rotation = Quaternion.SlerpUnclamped(
            Quaternion.identity,
            reference.transform.rotation,
            1.1f);
        ray = new Ray(camera.transform.position, rotation * Vector3.forward);
        return true;
    }
}
