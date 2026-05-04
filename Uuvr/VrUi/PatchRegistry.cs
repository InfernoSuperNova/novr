using System;
using System.Collections.Generic;
using UnityEngine;
using Uuvr.VrUi.SpecialBehavior;

namespace Uuvr.VrUi;

public enum UiTranslationSpace
{
    InFront, // In front of camera origin (Artificial horizon, main menu, compass etc)
    WorldPoint, // Any arbitrary position in the world, up pointing to camera original up
    ScreenSpace, // Screen space but VR
    CockpitScreen, // Render to a cockpit screen by index
}

public enum GameUiRegion
{
    Auto,
    CockpitHUD, // In front of everything
    OutsideCockpit, // Renders outside the cockpit but in front of the world
    Absolute, // Render as an object in the world
}

public enum PatchTargetKind
{
    GameObject,
    Canvas,
}

public static class PatchRegistry
{
    public static readonly Template[] Data =
    {
        Template.ForCanvas("MainCanvas", UiTranslationSpace.InFront, behaviour: typeof(MainMenuBehaviour)),
        Template.ForCanvas("ChatCanvas", UiTranslationSpace.InFront, behaviour: typeof(GameplayUIBehaviour)),
        Template.ForCanvas("HudCanvas", UiTranslationSpace.InFront, behaviour: typeof(HUDBehavior)),
        Template.ForCanvas("MenuCanvas", UiTranslationSpace.InFront, behaviour: typeof(GameplayUIBehaviour)),
        Template.ForCanvas("GameplayUICanvas", UiTranslationSpace.InFront, behaviour: typeof(GameplayUIBehaviour)),
        Template.ForCanvas("BlackoutCanvas", UiTranslationSpace.ScreenSpace, GameUiRegion.Absolute),
        Template.ForGameObject("SceneEssentials", behaviour: typeof(PositionZeroBehavior)),
        Template.ForGameObject("Canvas", behaviour: typeof(PositionZeroBehavior)),
        Template.ForGameObject("HUDCenter", behaviour: typeof(HUDBehavior)),
        Template.ForGameObject("HMDCenter", behaviour: typeof(HMDBehavior)),
        Template.ForGameObject("targetDesignator", behaviour: typeof(HMDBehavior))
    };

    public readonly struct Template
    {
        private Template(
            string objectName,
            PatchTargetKind targetKind,
            UiTranslationSpace translationSpace,
            GameUiRegion region,
            float order,
            bool depthFix,
            Type? behaviour)
        {
            ObjectName = objectName;
            TargetKind = targetKind;
            TranslationSpace = translationSpace;
            Region = ResolveRegion(translationSpace, region);
            Order = order;
            DepthFix = depthFix;
            Behaviour = behaviour;
        }

        public string ObjectName { get; }
        public PatchTargetKind TargetKind { get; }
        public UiTranslationSpace TranslationSpace { get; }
        public GameUiRegion Region { get; }
        public float Order { get; }
        public bool DepthFix { get; }
        public Type? Behaviour { get; }

        public static Template ForCanvas(
            string objectName,
            UiTranslationSpace translationSpace = UiTranslationSpace.InFront,
            GameUiRegion region = GameUiRegion.Auto,
            float order = 0,
            bool depthFix = false,
            Type? behaviour = null)
        {
            return new Template(objectName, PatchTargetKind.Canvas, translationSpace, region, order, depthFix, behaviour);
        }

        public static Template ForGameObject(
            string objectName,
            Type behaviour)
        {
            return new Template(
                objectName,
                PatchTargetKind.GameObject,
                UiTranslationSpace.WorldPoint,
                GameUiRegion.Absolute,
                0,
                false,
                behaviour);
        }

        private static GameUiRegion ResolveRegion(UiTranslationSpace translationSpace, GameUiRegion region)
        {
            if (region != GameUiRegion.Auto)
            {
                return region;
            }

            switch (translationSpace)
            {
                case UiTranslationSpace.CockpitScreen:
                    return GameUiRegion.Absolute;
                default:
                    return GameUiRegion.CockpitHUD;
            }
        }
    }
}
