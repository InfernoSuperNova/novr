using System.Collections.Generic;
using System.Text;
using System;
using NuclearOption.SavedMission;
using NuclearOption.Networking.Lobbies;
using NuclearOption.Workshop;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeGameActionAdapter
{
    private const string TopLevelMainMenuPathFragment = "Prejoin menu/LeftPanel/Container/MenuButtonsPanel";
    private const int MaxButtonsToDescribe = 20;
    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

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

        Debug.Log($"[NOVR] Native UI action adapter bound to original MainCanvas with {GetButtons(includeInactive: true).Length} buttons.");
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

        var buttons = GetButtons(includeInactive: false);
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

            var buttons = GetButtons(includeInactive: false);
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

        var buttons = GetButtons(includeInactive: false);
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

    public bool TryCloseWorkshopMenu()
    {
        if (_originalMainCanvas == null)
        {
            Debug.LogWarning("[NOVR] Native UI workshop close ignored because the original MainCanvas is not available.");
            return false;
        }

        var workshopMenus = _originalMainCanvas.GetComponentsInChildren<WorkshopMenu>(true);
        for (var index = 0; index < workshopMenus.Length; index++)
        {
            var workshopMenu = workshopMenus[index];
            if (!workshopMenu.gameObject.activeInHierarchy) continue;

            Debug.Log($"[NOVR] Native UI closing original workshop menu '{GetGameObjectPath(workshopMenu.gameObject)}'.");
            workshopMenu.CloseMenu();
            return true;
        }

        Debug.LogWarning("[NOVR] Native UI could not find an active original workshop menu to close.");
        return false;
    }

    public bool TrySelectOriginalMission(MissionKey missionKey)
    {
        if (_originalMainCanvas == null)
        {
            Debug.LogWarning("[NOVR] Native UI mission selection ignored because the original MainCanvas is not available.");
            return false;
        }

        var pickers = _originalMainCanvas.GetComponentsInChildren<global::MissionsPicker>(true);
        for (var index = 0; index < pickers.Length; index++)
        {
            var picker = pickers[index];
            if (!picker.gameObject.activeInHierarchy) continue;

            picker.SelectMission(missionKey);
            return true;
        }

        Debug.LogWarning("[NOVR] Native UI could not find an active original mission picker to synchronize mission selection.");
        return false;
    }

    public bool TryGetActiveLobbyList(out LobbyList? lobbyList)
    {
        lobbyList = null;
        if (_originalMainCanvas == null)
        {
            return false;
        }

        var lobbyLists = _originalMainCanvas.GetComponentsInChildren<LobbyList>(true);
        for (var index = 0; index < lobbyLists.Length; index++)
        {
            var candidate = lobbyLists[index];
            if (!candidate.gameObject.activeInHierarchy) continue;

            lobbyList = candidate;
            return true;
        }

        return false;
    }

    public bool TryRefreshMultiplayerLobbies()
    {
        if (!TryGetActiveLobbyList(out var lobbyList) || lobbyList == null)
        {
            Debug.LogWarning("[NOVR] Native UI multiplayer refresh ignored because the original lobby list is not active.");
            return false;
        }

        lobbyList.GetListOfLobbies();
        return true;
    }

    public bool TryOpenCreateLobby()
    {
        if (!TryGetActiveLobbyList(out var lobbyList) || lobbyList == null)
        {
            Debug.LogWarning("[NOVR] Native UI create lobby ignored because the original lobby list is not active.");
            return false;
        }

        var method = typeof(LobbyList).GetMethod("CreateLobbyClicked", PrivateInstanceFlags);
        if (method == null)
        {
            Debug.LogWarning("[NOVR] Native UI could not find LobbyList.CreateLobbyClicked.");
            return false;
        }

        method.Invoke(lobbyList, null);
        return true;
    }

    public bool TryJoinLobby(LobbyInstance lobby)
    {
        return TryJoinLobby(lobby, null, promptIfPasswordNeeded: true);
    }

    public bool TryJoinLobby(LobbyInstance lobby, string? password, bool promptIfPasswordNeeded)
    {
        if (SteamLobby.instance == null)
        {
            Debug.LogWarning("[NOVR] Native UI join lobby ignored because SteamLobby is not available.");
            return false;
        }

        SteamLobby.instance.TryJoinLobby(lobby, password, promptIfPasswordNeeded);
        return true;
    }

    public int GetTooManyPlayerLimit()
    {
        return TryGetActiveLobbyList(out var lobbyList) && lobbyList != null
            ? lobbyList.TooManyPlayerLimit
            : 16;
    }

    public bool TryJoinLobbyThroughOriginalPopup(LobbyInstance lobby)
    {
        if (!TryGetActiveLobbyList(out var lobbyList) || lobbyList == null)
        {
            Debug.LogWarning("[NOVR] Native UI join lobby ignored because the original lobby list is not active.");
            return false;
        }

        lobbyList.ShowLobbyPopup(lobby);
        var lobbyPopup = GetPrivateField<LobbyDetailsModal>(lobbyList, "lobbyPopup");
        if (lobbyPopup == null)
        {
            Debug.LogWarning("[NOVR] Native UI could not find LobbyList.lobbyPopup.");
            return false;
        }

        var joinMethod = typeof(LobbyDetailsModal).GetMethod("Join", PrivateInstanceFlags);
        if (joinMethod == null)
        {
            Debug.LogWarning("[NOVR] Native UI could not find LobbyDetailsModal.Join.");
            return false;
        }

        joinMethod.Invoke(lobbyPopup, null);
        return true;
    }

    private Button[] GetButtons(bool includeInactive)
    {
        return _originalMainCanvas != null
            ? _originalMainCanvas.GetComponentsInChildren<Button>(includeInactive)
            : new Button[0];
    }

    private static T? GetPrivateField<T>(object instance, string fieldName)
        where T : class
    {
        return instance.GetType().GetField(fieldName, PrivateInstanceFlags)?.GetValue(instance) as T;
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
