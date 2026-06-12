using System;
using NOVR.VrCamera;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NOVR.VrUi;

public class NOUIManager : NOVRBehaviour
{
    
    private Camera? _cockpitHudCamera;
    private GameObject? _smoothedForwardReference;
    
    public static NOUIManager I { get; private set; }
    
    public GameObject CockpitHudReference => _smoothedForwardReference ??= CreateSmoothedForwardReference();
    
    private GameObject CreateSmoothedForwardReference()
    {
        var go = new GameObject("SmoothedForwardReference");
        go.transform.SetParent(transform);
        return go;
    }
    
    private new void Awake()
    {
        base.Awake();
        APIBus.OnMainCameraChanged += OnMainCameraChanged;
    }
    
    private void OnDestroy()
    {
        APIBus.OnMainCameraChanged -= OnMainCameraChanged;
    }
    
    private void Start()
    {
        I = this;
        Create<UIBehaviorPatcher>(transform);
        UIBehaviorPatcher.DoPatching();
        Create<VrUiCursor>(transform);
        Create<NativeVrUiRoot>(transform);
    }
    
    
    private void OnMainCameraChanged(Camera? previous, Camera? newCam)
    {
        if (newCam == null)
        {
            return;
        }
    }
    
    

}
