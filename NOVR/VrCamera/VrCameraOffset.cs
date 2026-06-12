using UnityEngine;

namespace NOVR.VrCamera;

// TODO: add manual offsets.
public class VrCameraOffset: NOVRBehaviour
{
#if CPP
    public VrCameraOffset(System.IntPtr pointer) : base(pointer)
    {
    }
#endif

    protected override void OnBeforeRender()
    {
        base.OnBeforeRender();
        UpdateTransform();
    }

    protected override void OnSettingChanged()
    {
        base.OnSettingChanged();
        var config = ModConfiguration.Instance;
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
        transform.localRotation = Quaternion.identity;
    }
}
