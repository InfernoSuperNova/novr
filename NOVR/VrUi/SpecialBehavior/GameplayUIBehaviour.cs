using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class GameplayUIBehaviour : MonoBehaviour
{
    // private void Start()
    // {
    //     transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
    //     transform.position = new Vector3(0f, 0f, 3f);
    // }

    private void Update()
    {
        transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
        transform.position = new Vector3(0f, 0f, 3f);
    }
}