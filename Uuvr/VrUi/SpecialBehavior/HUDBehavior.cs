using UnityEngine;

namespace Uuvr.VrUi.SpecialBehavior;

public class HUDBehavior : MonoBehaviour
{
    private void Update()
    {
        transform.position = new Vector3(0f, 0f, 1000f);
        transform.rotation = Quaternion.identity;

        var statusDisplay = FindChildStartingWith(transform, "StatusDisplay_");
        if (statusDisplay != null)
        {
            statusDisplay.position = new Vector3(500f, 200f, 1000f);
        }
    }

    private static Transform FindChildStartingWith(Transform parent, string childNamePrefix)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith(childNamePrefix))
            {
                return child;
            }

            var nestedChild = FindChildStartingWith(child, childNamePrefix);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }
}
