using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOVR.McpBridge.Tools;

public static class SceneTools
{
    [McpTool("get_scene_hierarchy", "Returns the full GameObject tree of all loaded scenes.")]
    public static string GetSceneHierarchy(
        [McpParam(Name = "maxDepth", Description = "Maximum hierarchy depth to traverse (default: 0 = unlimited)")]
        int maxDepth = 0)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            sb.AppendLine($"Scene: {scene.name} (index={scene.buildIndex}, path={scene.path})");
            foreach (var root in scene.GetRootGameObjects())
            {
                AppendGameObject(sb, root, 1, maxDepth);
            }
        }

        return sb.ToString();
    }

    private static void AppendGameObject(StringBuilder sb, GameObject go, int depth, int maxDepth)
    {
        if (maxDepth > 0 && depth > maxDepth) return;

        var indent = new string(' ', depth * 2);
        var components = string.Join(", ", go.GetComponents<Component>()
            .Where(c => c != null)
            .Select(c => c.GetType().Name));

        var active = go.activeInHierarchy ? "" : " (inactive)";
        sb.AppendLine($"{indent}- {go.name}{active} [{components}]");

        for (var i = 0; i < go.transform.childCount; i++)
        {
            AppendGameObject(sb, go.transform.GetChild(i).gameObject, depth + 1, maxDepth);
        }
    }

    [McpTool("inspect_gameobject", "Returns detailed component data for a GameObject by name path.")]
    public static string InspectGameObject(
        [McpParam(Name = "name", Description = "GameObject name (substring match)")]
        string name,
        [McpParam(Name = "includeComponents", Description = "Include component field values", Required = false)]
        bool includeComponents = false)
    {
        var go = FindGameObjectByName(name);

        if (go == null)
            return $"GameObject '{name}' not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"GameObject: {go.name}");
        sb.AppendLine($"  Tag: {go.tag}");
        sb.AppendLine($"  Layer: {go.layer} ({LayerMask.LayerToName(go.layer)})");
        sb.AppendLine($"  Active: {go.activeInHierarchy}");
        sb.AppendLine($"  Scene: {go.scene.name}");
        sb.AppendLine($"  Position: {go.transform.position}");
        sb.AppendLine($"  Rotation: {go.transform.rotation.eulerAngles}");
        sb.AppendLine($"  Scale: {go.transform.localScale}");
        sb.AppendLine($"  Children: {go.transform.childCount}");

        if (!includeComponents) return sb.ToString();

        foreach (var component in go.GetComponents<Component>())
        {
            if (component == null) continue;
            sb.AppendLine($"  [{component.GetType().Name}]");
            AppendCuratedProperties(sb, component);
            var fields = component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(component);
                    sb.AppendLine($"    {field.Name} = {value ?? "null"}");
                }
                catch { sb.AppendLine($"    {field.Name} = <error>"); }
            }
        }

        return sb.ToString();
    }

    private static GameObject? FindGameObjectByName(string name)
    {
        return GameObject.Find(name) ?? Resources.FindObjectsOfTypeAll(typeof(GameObject))
            .Cast<GameObject>()
            .FirstOrDefault(o => o.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static readonly Dictionary<Type, PropertyInfo[]> CuratedProperties = BuildCuratedProperties();

    private static Dictionary<Type, PropertyInfo[]> BuildCuratedProperties()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance;
        var byName = (Type t, string n) => t.GetProperty(n, flags) ?? throw new InvalidOperationException($"Curated property {t.Name}.{n} not found");
        return new Dictionary<Type, PropertyInfo[]>
        {
            [typeof(UnityEngine.UI.Image)] = new[]
            {
                byName(typeof(UnityEngine.UI.Image), "color"),
                byName(typeof(UnityEngine.UI.Image), "sprite"),
                byName(typeof(UnityEngine.UI.Image), "raycastTarget"),
                byName(typeof(UnityEngine.UI.Image), "fillAmount"),
                byName(typeof(UnityEngine.UI.Image), "material"),
            },
            [typeof(UnityEngine.UI.RawImage)] = new[]
            {
                byName(typeof(UnityEngine.UI.RawImage), "color"),
                byName(typeof(UnityEngine.UI.RawImage), "texture"),
                byName(typeof(UnityEngine.UI.RawImage), "uvRect"),
                byName(typeof(UnityEngine.UI.RawImage), "raycastTarget"),
            },
            [typeof(UnityEngine.CanvasGroup)] = new[]
            {
                byName(typeof(UnityEngine.CanvasGroup), "alpha"),
                byName(typeof(UnityEngine.CanvasGroup), "ignoreParentGroups"),
                byName(typeof(UnityEngine.CanvasGroup), "blocksRaycasts"),
                byName(typeof(UnityEngine.CanvasGroup), "interactable"),
            },
            [typeof(UnityEngine.RectTransform)] = new[]
            {
                byName(typeof(UnityEngine.RectTransform), "sizeDelta"),
                byName(typeof(UnityEngine.RectTransform), "anchoredPosition"),
                byName(typeof(UnityEngine.RectTransform), "localScale"),
                byName(typeof(UnityEngine.RectTransform), "localEulerAngles"),
            },
        };
    }

    private static string FormatPropertyValue(object? value)
    {
        if (value == null) return "null";
        switch (value)
        {
            case Color c:
                return $"({c.r:F2}, {c.g:F2}, {c.b:F2}, a={c.a:F2})";
            case Sprite s:
                return $"\"{s.name}\" {s.rect.width}x{s.rect.height} ({(s.texture != null ? s.texture.name + " " + s.texture.width + "x" + s.texture.height : "no texture")})";
            case Texture2D t2d:
                return $"\"{t2d.name}\" {t2d.width}x{t2d.height}";
            case Texture tex:
                return $"\"{tex.name}\"";
            case Material mat:
                return $"\"{mat.name}\"";
            case Rect r:
                return $"(x={r.x:F1}, y={r.y:F1}, w={r.width:F1}, h={r.height:F1})";
            case Vector2 v2:
                return $"({v2.x:F2}, {v2.y:F2})";
            case Vector3 v3:
                return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";
            case Vector4 v4:
                return $"({v4.x:F2}, {v4.y:F2}, {v4.z:F2}, {v4.w:F2})";
            case bool b:
                return b ? "True" : "False";
            case float f:
                return $"{f:F2}";
            default:
                return value.ToString() ?? "null";
        }
    }

    private static void AppendCuratedProperties(StringBuilder sb, Component component)
    {
        if (!CuratedProperties.TryGetValue(component.GetType(), out var props)) return;
        foreach (var prop in props)
        {
            try
            {
                var value = prop.GetValue(component);
                sb.AppendLine($"    {prop.Name} = {FormatPropertyValue(value)}");
            }
            catch { sb.AppendLine($"    {prop.Name} = <error>"); }
        }
    }

    [McpTool("find_objects_by_type", "Finds all GameObject paths for a given Unity component type.")]
    public static string FindObjectsByType(
        [McpParam(Name = "typeName", Description = "Type name (e.g. Camera, MeshRenderer, Rigidbody)")]
        string typeName)
    {
        var sb = new StringBuilder();
        var count = 0;

        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(typeName, false, true))
            .FirstOrDefault(t => t != null);

        if (type == null) return $"Type '{typeName}' not found in any loaded assembly.";

        foreach (var obj in Resources.FindObjectsOfTypeAll(type))
        {
            if (obj is not Component component) continue;
            if (!component.gameObject.scene.isLoaded) continue;

            sb.AppendLine($"- {component.GetFullPath()}");
            count++;
        }

        return $"Found {count} object(s) of type '{typeName}':\n{sb}";
    }

    private static string GetFullPath(this Component component)
    {
        var sb = new StringBuilder(component.gameObject.name);
        var parent = component.transform.parent;
        while (parent != null)
        {
            sb.Insert(0, "/");
            sb.Insert(0, parent.name);
            parent = parent.parent;
        }
        return sb.ToString();
    }

    [McpTool("get_canvas_group_chain", "Walks a GameObject's parent chain and reports every CanvasGroup with alpha and ignoreParentGroups, plus cumulative effective alpha.")]
    public static string GetCanvasGroupChain(
        [McpParam(Name = "name", Description = "GameObject name (substring match)")]
        string name)
    {
        var go = FindGameObjectByName(name);
        if (go == null) return $"GameObject '{name}' not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"CanvasGroup chain for '{go.name}' (scene={go.scene.name}):");
        var current = go.transform;
        var depth = 0;
        var effectiveAlpha = 1.0f;
        var multiplying = true;
        while (current != null)
        {
            var cg = current.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                var indent = new string(' ', depth * 2 + 2);
                sb.AppendLine($"{indent}- {current.name} [CanvasGroup]");
                sb.AppendLine($"{indent}    alpha = {cg.alpha:F2}");
                sb.AppendLine($"{indent}    ignoreParentGroups = {(cg.ignoreParentGroups ? "True" : "False")}");
                sb.AppendLine($"{indent}    blocksRaycasts = {(cg.blocksRaycasts ? "True" : "False")}");
                sb.AppendLine($"{indent}    interactable = {(cg.interactable ? "True" : "False")}");
                if (multiplying && !cg.ignoreParentGroups)
                {
                    effectiveAlpha *= cg.alpha;
                }
                if (cg.ignoreParentGroups)
                {
                    multiplying = false;
                    sb.AppendLine($"{indent}    (above this point, ancestor alphas are ignored)");
                }
            }
            current = current.parent;
            depth++;
        }
        sb.AppendLine();
        sb.AppendLine($"Effective cumulative alpha (down to target): {effectiveAlpha:F2}");
        return sb.ToString();
    }
}