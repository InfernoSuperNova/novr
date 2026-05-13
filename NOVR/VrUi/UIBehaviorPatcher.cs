using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NOVR.VrUi;

public class UIBehaviorPatcher : UuvrBehaviour
{

    private static Dictionary<Type, Type> _patchMap = new()
    {
        { typeof(FlightHud), typeof(NOVRFlightHudBehavior) },
        { typeof(GameplayUI), typeof(NOVRGameplayUIBehaviour) },
        { typeof(MessageUI), typeof(NOVRGameplayUIBehaviour) },
        { typeof(StatusDisplay), typeof(NOVRStatusDisplayBehavior) }
    };

    private static Dictionary<string, Type> _sceneLoadPatchMap = new() // We patch gameobjects by name the first time a scene is loaded (yes we iterate the tree recursively)
    {
        { "MainCanvas", typeof(NOVRMainMenuBehavior) },
        { "MenuCanvas", typeof(NOVRGameplayUIBehaviour) },
        { "BlackoutCanvas", typeof(NOVRBlackoutCanvasBehavior)}, //Template.ForCanvas("BlackoutCanvas", UiTranslationSpace.ScreenSpace, GameUiRegion.Absolute),
        { "SceneEssentials", typeof(PositionZeroBehavior)},
        { "Canvas", typeof(PositionZeroBehavior)},
    };


    private static Dictionary<Component, Type> _toPatch_component = new();
    private static Dictionary<string, Type> _toPatch_name = new();
    private static List<GameObject> _toReactivate = new();
    

    static UIBehaviorPatcher()
    {
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private static void SceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        Debug.Log("UIBehaviorPatcher: Scene loaded");
        foreach (var kvp in _sceneLoadPatchMap) _toPatch_name[kvp.Key] = kvp.Value;
    }


    public static void DoPatching()
    {
        var harmony = new Harmony("novr.vrui.behavioradder");
        
        var postfixMethod = typeof(UIBehaviorPatcher).GetMethod(nameof(UniversalPostfix), BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var entry in _patchMap)
        {
            Type targetType = entry.Key;
            
            ConstructorInfo ctor = targetType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

            if (ctor != null)
            {
                harmony.Patch(ctor, postfix: new HarmonyMethod(postfixMethod));
            }
        }
    }
    
    static void UniversalPostfix(object __instance)
    {
        if (__instance is Component comp)
        {
            EnqueueForAddition(comp);
        }
    }

    // Adding components directly from the constructor scope is risky so we defer it till the next update
    static void EnqueueForAddition(Component comp)
    {
        var toAddComp = _patchMap[comp.GetType()];
        _toPatch_component[comp] = toAddComp;
    }


    private void Start()
    {
        foreach (var kvp in _sceneLoadPatchMap) _toPatch_name[kvp.Key] = kvp.Value;
    }
    
    private void FixedUpdate()
    {
        if (_toReactivate.Count > 0)
        {
            foreach (var go in _toReactivate.Where(go => go != null))
            {
                Debug.Log($"UIBehaviorPatcher: Reactivating {go.name}");
                go.SetActive(true);
            }
            
            _toReactivate.Clear();
        }
        
        foreach (var kvp in _toPatch_component)
        {
            Debug.Log($"UIBehaviorPatcher: Adding {kvp.Value.Name} to {kvp.Key.name} (component patch)");
            if (kvp.Key.name == "" || kvp.Key.name == null)
            {
                Debug.LogWarning($"Component not loaded fully?");
                return;
            }
            var comp = kvp.Key;
            var toAdd = kvp.Value;
            if (!comp.gameObject.TryGetComponent(toAdd, out Component _))
            {
                AddAndBounceIfActive(comp.gameObject, toAdd);
            }
        }
        _toPatch_component.Clear();


        if (_toPatch_name.Count > 0)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
            {
                
                var name = go.name;
                if (_toPatch_name.TryGetValue(name, out var toAdd))
                {
                    Debug.Log($"UIBehaviorPatcher: Adding {toAdd.Name} to {name} (name patch)");
                    if (!go.TryGetComponent(toAdd, out Component _)) AddAndBounceIfActive(go, toAdd);
                }
            }
            _toPatch_name.Clear();
        }
    }
    
    private static void AddAndBounceIfActive(GameObject go, Type toAdd)
    {
        var wasActive = go.activeInHierarchy;
        go.AddComponent(toAdd);

        if (!wasActive) return;
        
        Debug.Log($"UIBehaviorPatcher: Deactivating {go.name} for one frame to force lifecycle callbacks");
        go.SetActive(false);
        _toReactivate.Add(go);
    }
}
