using System;
using UnityEngine;
using UnityEngine.UI;

namespace Uuvr.VrUi.SpecialBehavior;

public class MainMenuBehaviour : MonoBehaviour
{
    private void Start()
    {
        transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
        transform.localPosition = new Vector3(0f, 0f, 3f);
    }

    private void Update()
    {
        FixUiFilterTags();
        FixUiMissions();
    }

    private void FixUiFilterTags()
    {
        var filterTagsContainer = transform.Find("Mission Picker/SelectionPanel/MissionSelectPanel/Filter Tags");
        if (filterTagsContainer == null)
        {
            throw new Exception("NOVR: Unexpected main menu layout in FixUiFilterTags");
        }

        for (int i = 0; i < filterTagsContainer.childCount; i++)
        {
            var child = filterTagsContainer.GetChild(i);
            child.localPosition = child.localPosition with { z = 0 };
        }
    }

    private void FixUiMissions()
    {
        var contentContainer = transform.Find("Mission Picker/SelectionPanel/MissionSelectPanel/Background/Mission List/Viewport/Content");
        if (contentContainer == null)
        {
            throw new Exception("NOVR: Unexpected main menu layout in FixUiMissions");
        }
        for (int i = 0; i < contentContainer.childCount; i++)
        {
            var child = contentContainer.GetChild(i);
            child.localPosition = child.localPosition with { z = 0 };
            FixMissionFilterTags(child);
        }
    }

    private void FixMissionFilterTags(Transform mission)
    {
        var tagContainer = mission.transform.Find("Tag List");
        if (tagContainer == null)
        {
            throw new Exception("NOVR: Unexpected main menu layout in FixMissionFilterTags");
        }
        for (int i = 0; i < tagContainer.childCount; i++)
        {
            var child = tagContainer.GetChild(i);
            child.localPosition = child.localPosition with { z = 0 };
        }
    }
}