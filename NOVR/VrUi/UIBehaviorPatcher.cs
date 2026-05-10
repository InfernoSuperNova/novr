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
        { typeof(MessageUI), typeof(NOVRGameplayUIBehaviour) }
    };

    private static Dictionary<string, Type> _sceneLoadPatchMap = new() // We patch gameobjects by name the first time a scene is loaded (yes we iterate the tree recursively)
    {
        { "MainCanvas", typeof(NOVRMainMenuBehavior) },
        { "MenuCanvas", typeof(NOVRGameplayUIBehaviour) },
        { "BlackoutCanvas", typeof(NOVRBlackoutCanvasBehavior)}, //Template.ForCanvas("BlackoutCanvas", UiTranslationSpace.ScreenSpace, GameUiRegion.Absolute),
        { "SceneEssentials", typeof(PositionZeroBehavior)},
        { "Canvas", typeof(PositionZeroBehavior)},
    };


    private static Dictionary<Component, Type> _toAdd = new();
    private static Dictionary<string, Type> _toPatch = new();
    

    static UIBehaviorPatcher()
    {
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private static void SceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        Debug.Log("UIBehaviorPatcher: Scene loaded");
        foreach (var kvp in _sceneLoadPatchMap) _toPatch[kvp.Key] = kvp.Value;
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
        _toAdd[comp] = toAddComp;
    }


    private void Start()
    {
        foreach (var kvp in _sceneLoadPatchMap) _toPatch[kvp.Key] = kvp.Value;
    }
    
    private void Update()
    {
        
        foreach (var kvp in _toAdd)
        {
            var comp = kvp.Key;
            if (comp == null) continue;
            var toAdd = kvp.Value;
            if (!comp.gameObject.TryGetComponent(toAdd, out Component _))  comp.gameObject.AddComponent(toAdd);
        }
        _toAdd.Clear();


        if (_toPatch.Count > 0)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
            {
                var name = go.name;
                if (_toPatch.TryGetValue(name, out var toAdd))
                {
                    Debug.Log("UIBehaviorPatcher: SHITFUCK");
                    if (!go.TryGetComponent(toAdd, out Component _)) go.AddComponent(toAdd);
                }
            }
            _toPatch.Clear();
        }
    }
}