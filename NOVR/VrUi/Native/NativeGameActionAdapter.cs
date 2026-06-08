using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeGameActionAdapter
{
    private readonly Dictionary<NativeGameAction, string[]> _mainMenuActionLabels = new()
    {
        { NativeGameAction.SinglePlayer, new[] { "SINGLE PLAYER", "SINGLEPLAYER" } },
        { NativeGameAction.Multiplayer, new[] { "MULTIPLAYER" } },
        { NativeGameAction.MissionEditor, new[] { "MISSION EDITOR", "MISSIONEDITOR" } },
        { NativeGameAction.Settings, new[] { "SETTINGS", "OPTIONS" } },
        { NativeGameAction.Encyclopedia, new[] { "ENCYCLOPEDIA" } },
        { NativeGameAction.Workshop, new[] { "WORKSHOP" } },
        { NativeGameAction.ChangeLog, new[] { "CHANGE LOG", "CHANGELOG" } },
        { NativeGameAction.ControlChanges, new[] { "CONTROL CHANGES" } },
        { NativeGameAction.DevelopmentRoadmap, new[] { "DEVELOPMENT ROADMAP", "ROADMAP" } },
        { NativeGameAction.Community, new[] { "JOIN OUR COMMUNITY", "COMMUNITY", "DISCORD" } },
        { NativeGameAction.ExitGame, new[] { "EXIT GAME", "EXITGAME", "QUIT" } }
    };

    private GameObject? _originalMainCanvas;

    public void SetOriginalMainCanvas(GameObject? originalMainCanvas)
    {
        if (_originalMainCanvas == originalMainCanvas) return;

        _originalMainCanvas = originalMainCanvas;
        if (_originalMainCanvas == null)
        {
            Debug.Log("[NOVR] Native UI action adapter cleared original MainCanvas reference.");
            return;
        }

        Debug.Log($"[NOVR] Native UI action adapter bound to original MainCanvas with {GetButtons().Length} buttons.");
    }

    public bool TryInvoke(NativeGameAction action)
    {
        if (!_mainMenuActionLabels.TryGetValue(action, out var candidateLabels))
        {
            Debug.LogWarning($"[NOVR] Native UI action '{action}' is not mapped to an original menu action.");
            return false;
        }

        if (_originalMainCanvas == null)
        {
            Debug.LogWarning($"[NOVR] Native UI action '{action}' ignored because the original MainCanvas is not available.");
            return false;
        }

        var buttons = GetButtons();
        for (var index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            if (!ButtonMatches(button, candidateLabels)) continue;

            Debug.Log($"[NOVR] Native UI action '{action}' delegated to original button '{GetGameObjectPath(button.gameObject)}'.");
            button.onClick.Invoke();
            return true;
        }

        Debug.LogWarning($"[NOVR] Native UI action '{action}' could not find an original menu button. Candidates: {string.Join(", ", candidateLabels)}. Available buttons: {DescribeButtons(buttons)}");
        return false;
    }

    public void QuitGame()
    {
        if (!TryInvoke(NativeGameAction.ExitGame))
        {
            Debug.Log("[NOVR] Native UI falling back to Application.Quit for ExitGame.");
            Application.Quit();
        }
    }

    private Button[] GetButtons()
    {
        return _originalMainCanvas != null
            ? _originalMainCanvas.GetComponentsInChildren<Button>(true)
            : new Button[0];
    }

    private static bool ButtonMatches(Button button, IEnumerable<string> candidateLabels)
    {
        foreach (var descriptor in GetButtonDescriptors(button))
        {
            var normalizedDescriptor = NormalizeLabel(descriptor);
            foreach (var candidateLabel in candidateLabels)
            {
                if (normalizedDescriptor == NormalizeLabel(candidateLabel))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> GetButtonDescriptors(Button button)
    {
        yield return button.name;

        var legacyTexts = button.GetComponentsInChildren<Text>(true);
        for (var index = 0; index < legacyTexts.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(legacyTexts[index].text))
            {
                yield return legacyTexts[index].text;
            }
        }

        var tmpTexts = button.GetComponentsInChildren<TMP_Text>(true);
        for (var index = 0; index < tmpTexts.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(tmpTexts[index].text))
            {
                yield return tmpTexts[index].text;
            }
        }
    }

    private static string DescribeButtons(IEnumerable<Button> buttons)
    {
        var builder = new StringBuilder();
        foreach (var button in buttons)
        {
            if (builder.Length > 0)
            {
                builder.Append("; ");
            }

            builder.Append(GetGameObjectPath(button.gameObject));
            builder.Append(" [");
            builder.Append(string.Join(" | ", GetButtonDescriptors(button)));
            builder.Append(']');
        }

        return builder.Length > 0 ? builder.ToString() : "none";
    }

    private static string NormalizeLabel(string label)
    {
        return label
            .Replace(" ", string.Empty)
            .Replace("\n", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\t", string.Empty)
            .ToUpperInvariant();
    }

    private static string GetGameObjectPath(GameObject gameObject)
    {
        var path = gameObject.name;
        var parent = gameObject.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
