using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRAircraftSelectionMenuBehavior : UIRenderedCanvasBehavior
{
    private bool _layoutApplied;

    public void Start()
    {
        base.Awake();
        //ConfigureLoadoutSelectorLayout();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        //ConfigureLoadoutSelectorLayout();
    }

    private void ConfigureLoadoutSelectorLayout()
    {
        if (_layoutApplied)
            return;

        if (!TryGetComponent<AircraftSelectionMenu>(out var menu))
            return;
        
        var loadoutSelector = GetComponentInChildren<LoadoutSelector>(true);
        

        if (loadoutSelector == null)
            return;

        var selectorTransform = loadoutSelector.transform;
        selectorTransform.SetParent(transform, false);
        selectorTransform.SetAsFirstSibling();

        if (selectorTransform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0f, 0.05f);
            rectTransform.anchorMax = new Vector2(0.48f, 0.95f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.offsetMin = new Vector2(30f, 30f);
            rectTransform.offsetMax = new Vector2(-30f, -30f);
            rectTransform.anchoredPosition = Vector2.zero;
        }
        
        

       
        
        _layoutApplied = true;
    }

    
}
