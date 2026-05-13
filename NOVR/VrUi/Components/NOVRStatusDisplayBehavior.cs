using System;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRStatusDisplayBehavior : UIRenderedCanvasBehavior
{
    private void Update()
    {
        transform.position = new Vector3(630f, 145f, 1000f);
    }
}