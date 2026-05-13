using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRHMDBehavior : UIRenderedCanvasBehavior
{
    
    
    private float _offset = 1000;

    private void Update()
    {
        var uiCam = EventBus.CockpitHudReference;
        transform.position = uiCam.transform.forward * _offset;
        transform.rotation = uiCam.transform.rotation;
        
        SetLocalPosition("Speed", new Vector3(-110f, 150f, 0f)); // TODO: Patch game files and use events to set these gameobjects
        SetLocalPosition("Altitude", new Vector3(110f, 150f, 0f));
        SetLocalPosition("Bearing", new Vector3(0f, 200f, 0f));
        SetLocalPosition("Artificial Horizon", new Vector3(0f, 150f, 0f));
    }

    private void SetLocalPosition(string childName, Vector3 localPosition)
    {
        var child = FindChildRecursive(transform, childName);
        if (child == null)
        {
            return;
        }

        child.localPosition = localPosition;
    }

    private void SetLocalPositionRotationAndScale(
        string childName,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        var child = FindChildRecursive(transform, childName);
        if (child == null)
        {
            return;
        }

        child.localPosition = localPosition;
        child.localEulerAngles = localEulerAngles;
        child.localScale = localScale;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            var nestedChild = FindChildRecursive(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }
}
