using System.Collections.Generic;
using System.Text;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeGameActionAdapter
{
    private const string TopLevelMainMenuPathFragment = "Prejoin menu/LeftPanel/Container/MenuButtonsPanel";
    private const int MaxButtonsToDescribe = 20;

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

    public event Action<NativeGameAction>? ActionInvoked;

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
        if (!IsTopLevelMainMenuAvailable)
        {
            Debug.LogWarning($"[NOVR] Native UI action '{action}' ignored because the original top-level main menu is not available.");
            return false;
        }

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
            ActionInvoked?.Invoke(action);
            return true;
        }

        Debug.LogWarning($"[NOVR] Native UI action '{action}' could not find an original menu button. Candidates: {string.Join(", ", candidateLabels)}. Available buttons: {DescribeButtons(buttons)}");
        return false;
    }

    public bool IsTopLevelMainMenuAvailable
    {
        get
        {
            if (_originalMainCanvas == null) return false;
            if (!_mainMenuActionLabels.TryGetValue(NativeGameAction.SinglePlayer, out var singlePlayerLabels)) return false;

            var buttons = GetButtons();
            for (var index = 0; index < buttons.Length; index++)
            {
                if (IsTopLevelMainMenuButton(buttons[index]) && ButtonMatches(buttons[index], singlePlayerLabels))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void QuitGame()
    {
        Debug.Log("[NOVR] Native UI quitting game.");
        Application.Quit();
    }

    public bool TryInvokeCurrentMenuButton(string actionName, params string[] candidateLabels)
    {
        if (_originalMainCanvas == null)
        {
            Debug.LogWarning($"[NOVR] Native UI current-menu action '{actionName}' ignored because the original MainCanvas is not available.");
            return false;
        }

        var buttons = GetButtons();
        for (var index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            if (!ButtonMatches(button, candidateLabels)) continue;

            Debug.Log($"[NOVR] Native UI current-menu action '{actionName}' delegated to original button '{GetGameObjectPath(button.gameObject)}'.");
            button.onClick.Invoke();
            return true;
        }

        Debug.LogWarning($"[NOVR] Native UI current-menu action '{actionName}' could not find an original menu button. Candidates: {string.Join(", ", candidateLabels)}. Available buttons: {DescribeButtons(buttons)}");
        return false;
    }

    public bool TryCloseSettingsMenu()
    {
        if (_originalMainCanvas == null)
        {
            Debug.LogWarning("[NOVR] Native UI settings close ignored because the original MainCanvas is not available.");
            return false;
        }

        var settingsMenus = _originalMainCanvas.GetComponentsInChildren<global::SettingsMenu>(true);
        for (var index = 0; index < settingsMenus.Length; index++)
        {
            var settingsMenu = settingsMenus[index];
            if (!settingsMenu.gameObject.activeInHierarchy) continue;

            Debug.Log($"[NOVR] Native UI closing original settings menu '{GetGameObjectPath(settingsMenu.gameObject)}'.");
            settingsMenu.CloseSettingsMenu();
            return true;
        }

        Debug.LogWarning("[NOVR] Native UI could not find an active original settings menu to close.");
        return false;
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

    private static bool IsTopLevelMainMenuButton(Button button)
    {
        return GetGameObjectPath(button.gameObject).Contains(TopLevelMainMenuPathFragment);
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
        var count = 0;
        foreach (var button in buttons)
        {
            count++;
            if (count > MaxButtonsToDescribe)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("; ");
            }

            builder.Append(GetGameObjectPath(button.gameObject));
            builder.Append(" [");
            builder.Append(string.Join(" | ", GetButtonDescriptors(button)));
            builder.Append(']');
        }

        if (count > MaxButtonsToDescribe)
        {
            builder.Append($"; ... {count - MaxButtonsToDescribe} more");
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
