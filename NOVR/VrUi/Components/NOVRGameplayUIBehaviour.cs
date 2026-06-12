using NOVR.HUD;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRGameplayUIBehaviour : UIRenderedCanvasBehavior
{
    private void Start()
    {
        transform.SetParent(SceneSingleton<StaticHudArmature>.i.transform, false);
        transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
        transform.localPosition = new Vector3(0f, 0f, 3f);
    }
}