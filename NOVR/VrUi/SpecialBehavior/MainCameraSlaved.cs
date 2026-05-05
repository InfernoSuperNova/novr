using System;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class MainCameraSlaved : MonoBehaviour
{

    private Camera MainCamera => EventBus.MainCamera;

    private void Update()
    {
        var camTrans = MainCamera.transform;
        transform.SetPositionAndRotation(camTrans.position, camTrans.rotation);
    }
    
}