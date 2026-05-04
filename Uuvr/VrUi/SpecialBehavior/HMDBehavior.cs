using UnityEngine;

namespace Uuvr.VrUi.SpecialBehavior;

public class HMDBehavior : MonoBehaviour
{

    private float _offset = 1000;

    private void Update()
    {
        var uiCam = VrUiManager.I.CockpitHudCamera;
        transform.position = uiCam.transform.forward * _offset;
        transform.rotation = uiCam.transform.rotation;

        SetLocalPosition("Speed", new Vector3(-110f, 150f, 0f));
        SetLocalPosition("Altitude", new Vector3(110f, 150f, 0f));
        SetLocalPosition("Bearing", new Vector3(0f, 200f, 0f));
        SetLocalPosition("Artificial Horizon", new Vector3(0f, 150f, 0f));

        SetLocalPositionRotationAndScale(
            "TopRightPanel",
            new Vector3(200f, -50f, -500f),
            new Vector3(0f, 45f, 0f),
            new Vector3(0.3f, 0.3f, 0.3f));

        SetLocalPositionRotationAndScale(
            "LowerLeftPanel",
            new Vector3(-200f, -110f, -500f),
            new Vector3(0f, 315f, 0f),
            new Vector3(0.3f, 0.3f, 0.3f));
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
