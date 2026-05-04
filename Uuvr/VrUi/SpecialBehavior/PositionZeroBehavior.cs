using UnityEngine;

namespace Uuvr.VrUi.SpecialBehavior;

public class PositionZeroBehavior : MonoBehaviour
{
    private void Update()
    {
        transform.position = new Vector3(0f, 0f, 0f);
    }
}