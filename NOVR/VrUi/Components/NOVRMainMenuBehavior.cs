using System;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRMainMenuBehavior : UIRenderedCanvasBehavior
{
    protected override bool ShouldInitializeCanvas => !IsNativeMenuUiEnabled;

    private void Start()
    {
        if (IsNativeMenuUiEnabled) return;

        transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
        transform.localPosition = new Vector3(0f, 0f, 3f);
    }

    private static bool IsNativeMenuUiEnabled =>
        ModConfiguration.Instance?.EnableNativeMenuUi.Value == true;
}
