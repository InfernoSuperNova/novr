using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

public class PatchDispatcher : UuvrBehaviour
{
    private const float UiChildDepthStep = -0.001f;
    private const float OrderDepthStep = 0.01f;

    private Dictionary<GameObject, PatchRegistry.Template> _patchedObjects = new();
    private Dictionary<GameObject, PatchRegistry.Template> _patchedObjectsSwap = new();

    private void Update()
    {
        PatchTargets();
    }

    private void PatchTargets()
    {
        if (NOUIManager.I == null)
        {
            return;
        }

        var gameObjects = FindObjectsOfType<GameObject>();
        for (var index = 0; index < gameObjects.Length; index++)
        {
            var gameObject = gameObjects[index];
            if (gameObject == null || _patchedObjects.ContainsKey(gameObject))
            {
                continue;
            }

            for (var templateIndex = 0; templateIndex < PatchRegistry.Data.Length; templateIndex++)
            {
                var template = PatchRegistry.Data[templateIndex];
                if (!Matches(gameObject, template))
                {
                    continue;
                }

                PatchObject(gameObject, template);
                _patchedObjects.Add(gameObject, template);
                break;
            }
        }

        RefreshPatchedObjects();
    }

    private static bool Matches(GameObject gameObject, PatchRegistry.Template template)
    {
        if (!gameObject.name.Equals(template.ObjectName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        switch (template.TargetKind)
        {
            case PatchTargetKind.Canvas:
                return gameObject.GetComponent<Canvas>() != null;
            case PatchTargetKind.GameObject:
                return true;
            default:
                return false;
        }
    }

    private static void PatchObject(GameObject gameObject, PatchRegistry.Template template)
    {
        AddBehaviour(gameObject, template);

        if (template.TargetKind != PatchTargetKind.Canvas)
        {
            return;
        }

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            PatchCanvas(canvas, template);
        }
    }

    private static void AddBehaviour(GameObject gameObject, PatchRegistry.Template template)
    {
        if (template.Behaviour == null || gameObject.GetComponent(template.Behaviour) != null)
        {
            return;
        }

        gameObject.AddComponent(template.Behaviour);
    }

    private static void PatchCanvas(Canvas canvas, PatchRegistry.Template template)
    {
        ApplyVrUiLayerRecursive(canvas.transform);
        canvas.transform.localPosition = canvas.transform.localPosition with { z = OrderDepthStep * template.Order };

        if (template.DepthFix)
        {
            ApplyUiDepthRecursive(canvas.transform);
        }

        switch (template.TranslationSpace)
        {
            case UiTranslationSpace.InFront:
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = GetCameraForRegion(template.Region);
                canvas.planeDistance = 1f;
                break;
            case UiTranslationSpace.WorldPoint:
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = VrCamera.VrCamera.HighestDepthVrCamera?.ParentCamera;
                break;
            case UiTranslationSpace.ScreenSpace:
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = GetCameraForRegion(template.Region);
                canvas.planeDistance = 1f;
                break;
            case UiTranslationSpace.CockpitScreen:
                // TODO: Setup cockpit screen override.
                break;
        }
    }

    private void RefreshPatchedObjects()
    {
        _patchedObjectsSwap.Clear();
        foreach (var kvp in _patchedObjects)
        {
            if (kvp.Key != null)
            {
                _patchedObjectsSwap.Add(kvp.Key, kvp.Value);
            }
        }

        (_patchedObjectsSwap, _patchedObjects) = (_patchedObjects, _patchedObjectsSwap);

        foreach (var kvp in _patchedObjects)
        {
            if (kvp.Value.TargetKind != PatchTargetKind.Canvas)
            {
                continue;
            }

            var canvas = kvp.Key.GetComponent<Canvas>();
            if (canvas == null)
            {
                continue;
            }

            ApplyVrUiLayerRecursive(canvas.transform);
            if (kvp.Value.DepthFix)
            {
                ApplyUiDepthRecursive(canvas.transform);
            }
        }
    }

    private static void ApplyUiDepthRecursive(Transform root, int depth = 0)
    {
        if (root is RectTransform rectTransform)
        {
            var localPosition = rectTransform.localPosition;
            localPosition.z = depth * UiChildDepthStep;
            rectTransform.localPosition = localPosition;
        }

        for (var index = 0; index < root.childCount; index++)
        {
            ApplyUiDepthRecursive(root.GetChild(index), depth + 1);
        }
    }

    private static Camera GetCameraForRegion(GameUiRegion region)
    {
        return NOUIManager.I.CockpitHudCamera;
    }

    private static void ApplyVrUiLayerRecursive(Transform root)
    {
        LayerHelper.SetLayerRecursive(root, LayerHelper.GetVrUiLayer());
    }
}
