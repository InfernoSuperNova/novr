using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NuclearOption.Networking;
using NuclearOption.Networking.Lobbies;
using NuclearOption.SavedMission;
using NuclearOption.SceneLoading;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeMultiplayerPanel : MonoBehaviour
{
    private const int PageSize = 10;
    private const int HostMissionPageSize = 9;
    private const float SourceRefreshIntervalSeconds = 1f;
    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo? LobbyItemShownField = typeof(LobbyListItem).GetField("shown", PrivateInstanceFlags);
    private static readonly int?[] MaxPingFilters = { null, 75, 150, 250, 400 };
    private static readonly PingDistanceFilter[] DistanceFilters =
    {
        PingDistanceFilter.Any,
        PingDistanceFilter.Nearby,
        PingDistanceFilter.Regional,
        PingDistanceFilter.Worldwide
    };

    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.92f);
    private static readonly Color PanelColor = new(0.05f, 0.06f, 0.065f, 0.94f);
    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);
    private static readonly Color ButtonSelectedColor = new(0.44f, 0.49f, 0.50f, 1f);
    private static readonly Color ButtonHoverColor = new(0.34f, 0.40f, 0.42f, 1f);
    private static readonly Color ButtonPressedColor = new(0.16f, 0.20f, 0.22f, 1f);
    private static readonly Color BackButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);
    private static readonly Color JoinButtonColor = new(0.12f, 0.34f, 0.20f, 0.96f);
    private static readonly Color FilterDisabledColor = new(0.12f, 0.14f, 0.15f, 0.92f);

    private readonly List<NativeLobbyEntry> _lobbies = new();
    private readonly List<NativeLobbyEntry> _visibleLobbies = new();
    private readonly List<NativeLobbyRow> _lobbyRows = new();
    private readonly List<SortHeaderBinding> _sortHeaders = new();
    private readonly List<FilterToggleBinding> _filterToggles = new();
    private readonly List<HostMissionEntry> _hostMissions = new();
    private readonly List<Button> _hostMissionButtons = new();
    private readonly List<Text> _hostMissionButtonTexts = new();

    private NativeGameActionAdapter? _actions;
    private RectTransform? _container;
    private RectTransform? _browserListPanel;
    private RectTransform? _detailsPanel;
    private RectTransform? _hostPanel;
    private RectTransform? _passwordPanel;
    private Font? _font;
    private Text? _titleText;
    private Text? _summaryText;
    private Text? _descriptionText;
    private Text? _statusText;
    private Text? _pageText;
    private Text? _hostMissionTitleText;
    private Text? _hostMissionDescriptionText;
    private Text? _hostMissionPageText;
    private Text? _hostPlayersText;
    private Text? _hostVisibilityText;
    private Text? _hostPasswordToggleText;
    private Text? _hostStatusText;
    private Text? _passwordTitleText;
    private Text? _passwordStatusText;
    private Text? _maxPingFilterText;
    private Text? _distanceFilterText;
    private InputField? _searchInput;
    private InputField? _hostNameInput;
    private InputField? _hostPasswordInput;
    private InputField? _joinPasswordInput;
    private Button? _joinButton;
    private int _selectedIndex = -1;
    private int _page;
    private int _hostSelectedMissionIndex;
    private int _hostMissionPage;
    private int _hostMaxPlayers = 8;
    private LobbySortColumn _sortColumn = LobbySortColumn.Lobby;
    private HostLobbyVisibility _hostVisibility = HostLobbyVisibility.Public;
    private bool _sortAscending = true;
    private bool _showPve = true;
    private bool _showPvp = true;
    private bool _showOpenLobbies = true;
    private bool _showPasswordedLobbies = true;
    private bool _showVanillaLobbies = true;
    private bool _showModdedLobbies = true;
    private bool _showDedicatedServers = true;
    private bool _showPlayerHostedServers = true;
    private bool _hideFullLobbies;
    private bool _hideEmptyLobbies;
    private int _maxPingFilterIndex;
    private PingDistanceFilter _distanceFilter = PingDistanceFilter.Any;
    private bool _hostPasswordEnabled;
    private bool _hostMissionsLoaded;
    private bool _isHosting;
    private NativeLobbyEntry? _pendingPasswordLobby;
    private float _nextSourceRefreshTime;
    private bool _wasVisible;

    public void Initialize(NativeGameActionAdapter actions, RectTransform root)
    {
        _actions = actions;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildLayout(root);
    }

    public void SetVisible(bool visible)
    {
        if (_container == null) return;

        if (visible && !_wasVisible)
        {
            _selectedIndex = -1;
            _page = 0;
            SetStatus("Refreshing multiplayer lobbies.");
            _actions?.TryRefreshMultiplayerLobbies();
            RefreshFromOriginalLobbyList();
            _nextSourceRefreshTime = Time.unscaledTime + SourceRefreshIntervalSeconds;
        }

        _wasVisible = visible;
        if (_container.gameObject.activeSelf != visible)
        {
            _container.gameObject.SetActive(visible);
        }
    }

    private void Update()
    {
        if (!_wasVisible || Time.unscaledTime < _nextSourceRefreshTime) return;

        _nextSourceRefreshTime = Time.unscaledTime + SourceRefreshIntervalSeconds;
        RefreshFromOriginalLobbyList();
    }

    private void BuildLayout(RectTransform root)
    {
        _container = CreateContainer("Native Multiplayer", root, root.sizeDelta);
        CreateImage("Background", _container, BackgroundColor, Vector2.zero, _container.sizeDelta);
        CreateText("Header", _container, "MULTIPLAYER", new Vector2(0f, 505f), new Vector2(1200f, 32f), 22, TextAnchor.MiddleCenter, Color.white);

        var listPanel = CreatePanel("Lobby List Panel", _container, PanelColor, new Vector2(-330f, -15f), new Vector2(1220f, 950f));
        _browserListPanel = listPanel;
        CreateText("List Header", listPanel, "SERVER BROWSER", new Vector2(0f, 430f), new Vector2(1160f, 30f), 18, TextAnchor.MiddleCenter, Color.white);
        BuildFilterControls(listPanel);
        BuildColumnHeaders(listPanel);

        for (var index = 0; index < PageSize; index++)
        {
            var rowIndex = index;
            _lobbyRows.Add(CreateLobbyRow(listPanel, index, new Vector2(0f, 212f - index * 45f), () => SelectLobby(_page * PageSize + rowIndex)));
        }

        CreateMenuButton("REFRESH", listPanel, new Vector2(-430f, -420f), new Vector2(170f, 34f), ButtonColor, RefreshClicked, 13);
        CreateMenuButton("<", listPanel, new Vector2(-105f, -420f), new Vector2(62f, 34f), ButtonColor, PreviousPage, 16);
        _pageText = CreateText("Page", listPanel, "", new Vector2(0f, -420f), new Vector2(140f, 34f), 13, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", listPanel, new Vector2(105f, -420f), new Vector2(62f, 34f), ButtonColor, NextPage, 16);
        CreateMenuButton("CREATE LOBBY", listPanel, new Vector2(430f, -420f), new Vector2(190f, 34f), ButtonColor, CreateLobbyClicked, 13);

        var detailsPanel = CreatePanel("Lobby Details Panel", _container, PanelColor, new Vector2(630f, -15f), new Vector2(690f, 950f));
        _detailsPanel = detailsPanel;
        _titleText = CreateText("Lobby Title", detailsPanel, "", new Vector2(0f, 395f), new Vector2(630f, 46f), 21, TextAnchor.MiddleCenter, Color.white);
        _summaryText = CreateText("Lobby Summary", detailsPanel, "", new Vector2(0f, 330f), new Vector2(630f, 62f), 14, TextAnchor.MiddleCenter, new Color(0.82f, 0.86f, 0.72f, 1f));
        _descriptionText = CreateText("Lobby Description", detailsPanel, "", new Vector2(0f, 75f), new Vector2(630f, 450f), 14, TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.90f, 1f));
        _statusText = CreateText("Lobby Status", detailsPanel, "", new Vector2(0f, -345f), new Vector2(630f, 76f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));

        CreateMenuButton("BACK", _container, new Vector2(-860f, -520f), new Vector2(190f, 44f), BackButtonColor, BackToMainMenu, 15);
        _joinButton = CreateMenuButton("JOIN", _container, new Vector2(860f, -520f), new Vector2(190f, 44f), JoinButtonColor, JoinSelectedLobby, 15);
        _joinButton.interactable = false;

        BuildHostSetupPanel();
        BuildPasswordPanel();
        _container.gameObject.SetActive(false);
    }

    private void BuildFilterControls(RectTransform listPanel)
    {
        var labelColor = new Color(0.78f, 0.84f, 0.86f, 1f);
        CreateText("Search Filter Label", listPanel, "SEARCH", new Vector2(-545f, 392f), new Vector2(92f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        _searchInput = CreateInputField("Lobby Search", listPanel, new Vector2(-330f, 392f), new Vector2(330f, 30f), "Lobby, mission, map");
        _searchInput.onValueChanged.AddListener(_ => ReapplyFiltersFromFirstPage());
        CreateMenuButton("CLEAR", listPanel, new Vector2(-120f, 392f), new Vector2(82f, 30f), ButtonColor, ClearSearch, 11);

        CreateText("Max Ping Filter Label", listPanel, "MAX PING", new Vector2(25f, 392f), new Vector2(104f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateMenuButton("<", listPanel, new Vector2(105f, 392f), new Vector2(42f, 30f), ButtonColor, () => CycleMaxPingFilter(-1), 14);
        _maxPingFilterText = CreateText("Max Ping Filter", listPanel, "", new Vector2(178f, 392f), new Vector2(96f, 30f), 11, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", listPanel, new Vector2(250f, 392f), new Vector2(42f, 30f), ButtonColor, () => CycleMaxPingFilter(1), 14);

        CreateText("Distance Filter Label", listPanel, "DISTANCE", new Vector2(355f, 392f), new Vector2(104f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateMenuButton("<", listPanel, new Vector2(440f, 392f), new Vector2(42f, 30f), ButtonColor, () => CycleDistanceFilter(-1), 14);
        _distanceFilterText = CreateText("Distance Filter", listPanel, "", new Vector2(520f, 392f), new Vector2(112f, 30f), 11, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", listPanel, new Vector2(595f, 392f), new Vector2(42f, 30f), ButtonColor, () => CycleDistanceFilter(1), 14);

        CreateText("Mission Filter Label", listPanel, "MISSION", new Vector2(-545f, 350f), new Vector2(92f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "PvE", new Vector2(-460f, 350f), new Vector2(82f, 28f), () => _showPve, value => _showPve = value);
        CreateFilterToggle(listPanel, "PvP", new Vector2(-370f, 350f), new Vector2(82f, 28f), () => _showPvp, value => _showPvp = value);

        CreateText("Access Filter Label", listPanel, "ACCESS", new Vector2(-160f, 350f), new Vector2(92f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "Open", new Vector2(-68f, 350f), new Vector2(94f, 28f), () => _showOpenLobbies, value => _showOpenLobbies = value);
        CreateFilterToggle(listPanel, "Pwd", new Vector2(35f, 350f), new Vector2(90f, 28f), () => _showPasswordedLobbies, value => _showPasswordedLobbies = value);

        CreateText("Mods Filter Label", listPanel, "MODS", new Vector2(160f, 350f), new Vector2(76f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "Vanilla", new Vector2(270f, 350f), new Vector2(118f, 28f), () => _showVanillaLobbies, value => _showVanillaLobbies = value);
        CreateFilterToggle(listPanel, "Modded", new Vector2(405f, 350f), new Vector2(118f, 28f), () => _showModdedLobbies, value => _showModdedLobbies = value);

        CreateText("Server Filter Label", listPanel, "SERVER", new Vector2(-545f, 310f), new Vector2(92f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "Dedicated", new Vector2(-430f, 310f), new Vector2(132f, 28f), () => _showDedicatedServers, value => _showDedicatedServers = value);
        CreateFilterToggle(listPanel, "Player Host", new Vector2(-275f, 310f), new Vector2(142f, 28f), () => _showPlayerHostedServers, value => _showPlayerHostedServers = value);

        CreateText("Availability Filter Label", listPanel, "AVAIL", new Vector2(-105f, 310f), new Vector2(76f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "Hide Full", new Vector2(20f, 310f), new Vector2(122f, 28f), () => _hideFullLobbies, value => _hideFullLobbies = value);
        CreateFilterToggle(listPanel, "Hide Empty", new Vector2(160f, 310f), new Vector2(134f, 28f), () => _hideEmptyLobbies, value => _hideEmptyLobbies = value);
    }

    private void BuildColumnHeaders(RectTransform listPanel)
    {
        CreateSortHeaderButton(listPanel, LobbySortColumn.Type, "TYPE", new Vector2(-548f, 260f), new Vector2(64f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Password, "LOCK", new Vector2(-475f, 260f), new Vector2(66f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Mods, "MODS", new Vector2(-395f, 260f), new Vector2(78f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Server, "HOST", new Vector2(-298f, 260f), new Vector2(100f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Lobby, "LOBBY", new Vector2(-69f, 260f), new Vector2(342f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Mission, "MISSION", new Vector2(263f, 260f), new Vector2(306f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Players, "PLAYERS", new Vector2(465f, 260f), new Vector2(82f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Ping, "PING", new Vector2(547f, 260f), new Vector2(66f, 30f));
    }

    private void CreateSortHeaderButton(RectTransform parent, LobbySortColumn column, string label, Vector2 anchoredPosition, Vector2 size)
    {
        var button = CreateMenuButton($"Sort {label}", parent, anchoredPosition, size, ButtonColor, () => SetSortColumn(column), 11);
        var text = button.GetComponentInChildren<Text>();
        _sortHeaders.Add(new SortHeaderBinding(column, label, button, text));
    }

    private void CreateFilterToggle(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size, Func<bool> getValue, Action<bool> setValue)
    {
        var button = CreateMenuButton($"Filter {label}", parent, anchoredPosition, size, ButtonColor, () =>
        {
            setValue(!getValue());
            ReapplyFiltersFromFirstPage();
        }, 11, TextAnchor.MiddleCenter);
        var text = button.GetComponentInChildren<Text>();
        _filterToggles.Add(new FilterToggleBinding(label, button, text, getValue));
    }

    private NativeLobbyRow CreateLobbyRow(RectTransform parent, int index, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        var button = CreateButton($"Lobby Row {index}", parent, anchoredPosition, new Vector2(1180f, 38f), ButtonColor, onClick);
        var rowTransform = (RectTransform)button.transform;
        return new NativeLobbyRow(
            button,
            CreateColumnText("Type", rowTransform, new Vector2(-548f, 0f), new Vector2(64f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Lock", rowTransform, new Vector2(-475f, 0f), new Vector2(66f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Mods", rowTransform, new Vector2(-395f, 0f), new Vector2(78f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Host", rowTransform, new Vector2(-298f, 0f), new Vector2(100f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Lobby", rowTransform, new Vector2(-69f, 0f), new Vector2(342f, 30f), TextAnchor.MiddleLeft),
            CreateColumnText("Mission", rowTransform, new Vector2(263f, 0f), new Vector2(306f, 30f), TextAnchor.MiddleLeft),
            CreateColumnText("Players", rowTransform, new Vector2(465f, 0f), new Vector2(82f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Ping", rowTransform, new Vector2(547f, 0f), new Vector2(66f, 30f), TextAnchor.MiddleCenter));
    }

    private Text CreateColumnText(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
    {
        var text = CreateText(name, parent, "", anchoredPosition, size, 12, alignment, Color.white);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private void BuildHostSetupPanel()
    {
        if (_container == null) return;

        _hostPanel = CreatePanel("Host Setup Panel", _container, PanelColor, new Vector2(0f, -15f), new Vector2(1600f, 950f));
        CreateText("Host Header", _hostPanel, "CREATE MULTIPLAYER LOBBY", new Vector2(0f, 420f), new Vector2(1000f, 34f), 22, TextAnchor.MiddleCenter, Color.white);

        var missionPanel = CreatePanel("Host Mission Panel", _hostPanel, new Color(0.035f, 0.043f, 0.048f, 0.95f), new Vector2(-420f, -10f), new Vector2(700f, 780f));
        CreateText("Host Mission Header", missionPanel, "MISSION", new Vector2(0f, 345f), new Vector2(640f, 30f), 18, TextAnchor.MiddleCenter, Color.white);
        for (var index = 0; index < HostMissionPageSize; index++)
        {
            var rowIndex = index;
            var button = CreateMenuButton(
                $"Host Mission {index}",
                missionPanel,
                new Vector2(0f, 280f - index * 55f),
                new Vector2(640f, 42f),
                ButtonColor,
                () => SelectHostMission(_hostMissionPage * HostMissionPageSize + rowIndex),
                13,
                TextAnchor.MiddleLeft);
            _hostMissionButtons.Add(button);
            _hostMissionButtonTexts.Add(button.GetComponentInChildren<Text>());
        }

        CreateMenuButton("<", missionPanel, new Vector2(-95f, -330f), new Vector2(68f, 34f), ButtonColor, PreviousHostMissionPage, 16);
        _hostMissionPageText = CreateText("Host Mission Page", missionPanel, "", new Vector2(0f, -330f), new Vector2(130f, 34f), 13, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", missionPanel, new Vector2(95f, -330f), new Vector2(68f, 34f), ButtonColor, NextHostMissionPage, 16);

        var settingsPanel = CreatePanel("Host Settings Panel", _hostPanel, new Color(0.035f, 0.043f, 0.048f, 0.95f), new Vector2(410f, -10f), new Vector2(760f, 780f));
        _hostMissionTitleText = CreateText("Host Mission Title", settingsPanel, "", new Vector2(0f, 330f), new Vector2(700f, 36f), 20, TextAnchor.MiddleCenter, Color.white);
        _hostMissionDescriptionText = CreateText("Host Mission Description", settingsPanel, "", new Vector2(0f, 230f), new Vector2(700f, 140f), 13, TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.90f, 1f));

        CreateText("Lobby Name Label", settingsPanel, "LOBBY NAME", new Vector2(-250f, 115f), new Vector2(180f, 28f), 13, TextAnchor.MiddleLeft, Color.white);
        _hostNameInput = CreateInputField("Host Lobby Name", settingsPanel, new Vector2(105f, 115f), new Vector2(440f, 36f), "Lobby name");

        CreateText("Players Label", settingsPanel, "MAX PLAYERS", new Vector2(-250f, 60f), new Vector2(180f, 28f), 13, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("-", settingsPanel, new Vector2(20f, 60f), new Vector2(48f, 32f), ButtonColor, () => ChangeHostMaxPlayers(-1), 16);
        _hostPlayersText = CreateText("Host Players", settingsPanel, "", new Vector2(105f, 60f), new Vector2(90f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("+", settingsPanel, new Vector2(190f, 60f), new Vector2(48f, 32f), ButtonColor, () => ChangeHostMaxPlayers(1), 16);

        CreateText("Visibility Label", settingsPanel, "LOBBY TYPE", new Vector2(-250f, 5f), new Vector2(180f, 28f), 13, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("<", settingsPanel, new Vector2(20f, 5f), new Vector2(48f, 32f), ButtonColor, () => CycleHostVisibility(-1), 16);
        _hostVisibilityText = CreateText("Host Visibility", settingsPanel, "", new Vector2(120f, 5f), new Vector2(150f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", settingsPanel, new Vector2(220f, 5f), new Vector2(48f, 32f), ButtonColor, () => CycleHostVisibility(1), 16);

        CreateText("Password Label", settingsPanel, "PASSWORD", new Vector2(-250f, -50f), new Vector2(180f, 28f), 13, TextAnchor.MiddleLeft, Color.white);
        var passwordToggle = CreateMenuButton("Password Toggle", settingsPanel, new Vector2(20f, -50f), new Vector2(120f, 32f), ButtonColor, ToggleHostPassword, 12);
        _hostPasswordToggleText = passwordToggle.GetComponentInChildren<Text>();
        _hostPasswordInput = CreateInputField("Host Password", settingsPanel, new Vector2(210f, -50f), new Vector2(250f, 36f), "Password", InputField.ContentType.Password);

        _hostStatusText = CreateText("Host Status", settingsPanel, "", new Vector2(0f, -200f), new Vector2(700f, 90f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));

        CreateMenuButton("CANCEL", _hostPanel, new Vector2(-500f, -420f), new Vector2(180f, 42f), BackButtonColor, HideHostSetup, 15);
        CreateMenuButton("HOST LOBBY", _hostPanel, new Vector2(500f, -420f), new Vector2(220f, 42f), JoinButtonColor, HostLobbyClicked, 15);
        _hostPanel.gameObject.SetActive(false);
    }

    private void BuildPasswordPanel()
    {
        if (_container == null) return;

        _passwordPanel = CreatePanel("Join Password Panel", _container, new Color(0.02f, 0.026f, 0.032f, 0.98f), new Vector2(0f, -20f), new Vector2(640f, 320f));
        _passwordTitleText = CreateText("Join Password Title", _passwordPanel, "PASSWORD REQUIRED", new Vector2(0f, 110f), new Vector2(560f, 34f), 20, TextAnchor.MiddleCenter, Color.white);
        _joinPasswordInput = CreateInputField("Join Password Input", _passwordPanel, new Vector2(0f, 40f), new Vector2(460f, 40f), "Server password", InputField.ContentType.Password);
        _passwordStatusText = CreateText("Join Password Status", _passwordPanel, "", new Vector2(0f, -28f), new Vector2(560f, 44f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));
        CreateMenuButton("CANCEL", _passwordPanel, new Vector2(-150f, -105f), new Vector2(150f, 38f), BackButtonColor, HidePasswordPanel, 14);
        CreateMenuButton("JOIN", _passwordPanel, new Vector2(150f, -105f), new Vector2(150f, 38f), JoinButtonColor, SubmitJoinPassword, 14);
        _passwordPanel.gameObject.SetActive(false);
    }

    private void RefreshFromOriginalLobbyList()
    {
        _lobbies.Clear();

        if (_actions?.TryGetActiveLobbyList(out var lobbyList) != true || lobbyList == null)
        {
            SetStatus("Waiting for the multiplayer lobby list.");
            _visibleLobbies.Clear();
            RefreshList();
            SelectLobby(-1);
            return;
        }

        var lobbyItems = lobbyList.GetComponentsInChildren<LobbyListItem>(true);
        for (var index = 0; index < lobbyItems.Length; index++)
        {
            var item = lobbyItems[index];
            if (item == null || !IsLobbyItemShown(item) || item.lobby == null) continue;

            _lobbies.Add(new NativeLobbyEntry(item));
        }

        ApplyFiltersAndSort();
    }

    private void ApplyFiltersAndSort()
    {
        _visibleLobbies.Clear();
        for (var index = 0; index < _lobbies.Count; index++)
        {
            var lobby = _lobbies[index];
            if (!MatchesFilters(lobby)) continue;

            _visibleLobbies.Add(lobby);
        }

        _visibleLobbies.Sort(CompareLobbies);
        _page = Mathf.Clamp(_page, 0, GetLastPage());
        if (_selectedIndex >= _visibleLobbies.Count)
        {
            _selectedIndex = _visibleLobbies.Count - 1;
        }

        UpdateFilterLabels();
        RefreshList();
        if (_selectedIndex >= 0)
        {
            SelectLobby(_selectedIndex);
        }
        else
        {
            SelectLobby(_visibleLobbies.Count > 0 ? 0 : -1);
        }

        if (_statusText != null)
        {
            _statusText.text = _visibleLobbies.Count == 0
                ? $"No lobbies match current filters. Total lobbies visible in game: {_lobbies.Count}."
                : $"{_visibleLobbies.Count} lobbies shown. Click a column header to sort; click again to reverse.";
        }
    }

    private bool MatchesFilters(NativeLobbyEntry lobby)
    {
        var missionMatches = lobby.MissionType switch
        {
            MissionPvpType.Pve => _showPve,
            MissionPvpType.Pvp => _showPvp,
            _ => _showPve || _showPvp
        };
        if (!missionMatches) return false;

        if (lobby.HasPassword && !_showPasswordedLobbies) return false;
        if (!lobby.HasPassword && !_showOpenLobbies) return false;

        if (lobby.IsModded && !_showModdedLobbies) return false;
        if (!lobby.IsModded && !_showVanillaLobbies) return false;

        if (lobby.IsDedicated && !_showDedicatedServers) return false;
        if (!lobby.IsDedicated && !_showPlayerHostedServers) return false;
        if (_hideFullLobbies && lobby.IsFull) return false;
        if (_hideEmptyLobbies && lobby.CurrentPlayers <= 0) return false;
        if (!MatchesSearch(lobby)) return false;

        var pingLimit = GetActivePingLimit();
        if (pingLimit.HasValue && (!lobby.Ping.HasValue || lobby.Ping.Value > pingLimit.Value))
        {
            return false;
        }

        return true;
    }

    private bool MatchesSearch(NativeLobbyEntry lobby)
    {
        var searchText = _searchInput?.text ?? "";
        if (string.IsNullOrWhiteSpace(searchText)) return true;

        var terms = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < terms.Length; index++)
        {
            var term = terms[index];
            if (!ContainsSearchTerm(lobby.LobbyName, term) &&
                !ContainsSearchTerm(lobby.MissionName, term) &&
                !ContainsSearchTerm(lobby.MapName, term) &&
                !ContainsSearchTerm(lobby.Description, term) &&
                !ContainsSearchTerm(lobby.ServerTypeText, term) &&
                !ContainsSearchTerm(lobby.StatusText, term))
            {
                return false;
            }
        }

        return true;
    }

    private int CompareLobbies(NativeLobbyEntry left, NativeLobbyEntry right)
    {
        var comparison = _sortColumn switch
        {
            LobbySortColumn.Type => CompareText(left.MissionTypeText, right.MissionTypeText),
            LobbySortColumn.Password => CompareText(left.AccessText, right.AccessText),
            LobbySortColumn.Mods => CompareText(left.ModsText, right.ModsText),
            LobbySortColumn.Server => CompareText(left.ServerColumnText, right.ServerColumnText),
            LobbySortColumn.Lobby => CompareText(left.LobbyName, right.LobbyName),
            LobbySortColumn.Mission => CompareText(left.MissionName, right.MissionName),
            LobbySortColumn.Players => left.CurrentPlayers.CompareTo(right.CurrentPlayers),
            LobbySortColumn.Ping => NullableIntSortKey(left.Ping).CompareTo(NullableIntSortKey(right.Ping)),
            _ => CompareText(left.LobbyName, right.LobbyName)
        };

        if (!_sortAscending)
        {
            comparison = -comparison;
        }

        return comparison != 0 ? comparison : CompareText(left.LobbyName, right.LobbyName);
    }

    private void SetSortColumn(LobbySortColumn column)
    {
        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = column != LobbySortColumn.Players;
        }

        ReapplyFiltersFromFirstPage();
    }

    private void ReapplyFiltersFromFirstPage()
    {
        _page = 0;
        _selectedIndex = -1;
        ApplyFiltersAndSort();
    }

    private void UpdateFilterLabels()
    {
        for (var index = 0; index < _filterToggles.Count; index++)
        {
            var toggle = _filterToggles[index];
            var enabled = toggle.IsEnabled();
            toggle.Text.text = $"{(enabled ? "[X]" : "[ ]")} {toggle.Label}";
            SetButtonColor(toggle.Button, enabled ? ButtonSelectedColor : FilterDisabledColor);
        }

        for (var index = 0; index < _sortHeaders.Count; index++)
        {
            var header = _sortHeaders[index];
            var selected = header.Column == _sortColumn;
            var direction = selected ? (_sortAscending ? " ^" : " v") : "";
            header.Text.text = header.Label + direction;
            SetButtonColor(header.Button, selected ? ButtonSelectedColor : ButtonColor);
        }

        if (_maxPingFilterText != null)
        {
            _maxPingFilterText.text = GetMaxPingFilterLabel();
        }

        if (_distanceFilterText != null)
        {
            _distanceFilterText.text = GetDistanceFilterLabel();
        }
    }

    private void ClearSearch()
    {
        if (_searchInput == null) return;

        if (string.IsNullOrEmpty(_searchInput.text))
        {
            ReapplyFiltersFromFirstPage();
            return;
        }

        _searchInput.text = "";
    }

    private void CycleMaxPingFilter(int direction)
    {
        _maxPingFilterIndex = WrapIndex(_maxPingFilterIndex + direction, MaxPingFilters.Length);
        ReapplyFiltersFromFirstPage();
    }

    private void CycleDistanceFilter(int direction)
    {
        var currentIndex = Array.IndexOf(DistanceFilters, _distanceFilter);
        if (currentIndex < 0) currentIndex = 0;

        _distanceFilter = DistanceFilters[WrapIndex(currentIndex + direction, DistanceFilters.Length)];
        ReapplyFiltersFromFirstPage();
    }

    private int? GetActivePingLimit()
    {
        var maxPingLimit = MaxPingFilters[Mathf.Clamp(_maxPingFilterIndex, 0, MaxPingFilters.Length - 1)];
        var distanceLimit = GetDistancePingLimit(_distanceFilter);
        if (!maxPingLimit.HasValue) return distanceLimit;
        if (!distanceLimit.HasValue) return maxPingLimit;

        return Math.Min(maxPingLimit.Value, distanceLimit.Value);
    }

    private string GetMaxPingFilterLabel()
    {
        var limit = MaxPingFilters[Mathf.Clamp(_maxPingFilterIndex, 0, MaxPingFilters.Length - 1)];
        return limit.HasValue ? $"{limit.Value} ms" : "ANY";
    }

    private static int? GetDistancePingLimit(PingDistanceFilter filter)
    {
        return filter switch
        {
            PingDistanceFilter.Nearby => 75,
            PingDistanceFilter.Regional => 150,
            PingDistanceFilter.Worldwide => 300,
            _ => null
        };
    }

    private string GetDistanceFilterLabel()
    {
        return _distanceFilter switch
        {
            PingDistanceFilter.Nearby => "NEAR",
            PingDistanceFilter.Regional => "REGION",
            PingDistanceFilter.Worldwide => "WORLD",
            _ => "ANY"
        };
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0) return 0;

        var wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    private void RefreshList()
    {
        for (var index = 0; index < PageSize; index++)
        {
            var lobbyIndex = _page * PageSize + index;
            var hasLobby = lobbyIndex >= 0 && lobbyIndex < _visibleLobbies.Count;
            var row = _lobbyRows[index];
            row.Button.gameObject.SetActive(hasLobby);
            if (!hasLobby) continue;

            var lobby = _visibleLobbies[lobbyIndex];
            row.TypeText.text = lobby.MissionTypeText;
            row.LockText.text = lobby.AccessText;
            row.ModsText.text = lobby.ModsText;
            row.HostText.text = lobby.ServerColumnText;
            row.LobbyText.text = Shorten(lobby.LobbyName, 44);
            row.MissionText.text = Shorten(lobby.MissionName, 40);
            row.PlayersText.text = lobby.PlayerCountText;
            row.PingText.text = lobby.PingText;
            SetButtonColor(row.Button, lobbyIndex == _selectedIndex ? ButtonSelectedColor : ButtonColor);
        }

        if (_pageText != null)
        {
            _pageText.text = $"{_page + 1} / {GetLastPage() + 1}";
        }
    }

    private void SelectLobby(int index)
    {
        if (index < 0 || index >= _visibleLobbies.Count)
        {
            _selectedIndex = -1;
            if (_titleText != null) _titleText.text = "";
            if (_summaryText != null) _summaryText.text = "";
            if (_descriptionText != null) _descriptionText.text = "";
            if (_joinButton != null) _joinButton.interactable = false;
            RefreshList();
            return;
        }

        _selectedIndex = index;
        var lobby = _visibleLobbies[_selectedIndex];
        if (_titleText != null) _titleText.text = lobby.LobbyName;
        if (_summaryText != null)
        {
            _summaryText.text = $"{lobby.MissionName}\n{lobby.MapName}   {lobby.PlayerCountText}   {lobby.PingText}   {lobby.StatusText}";
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = lobby.Description;
        }

        if (_joinButton != null)
        {
            _joinButton.interactable = !lobby.IsFull;
        }

        RefreshList();
    }

    private void RefreshClicked()
    {
        SetStatus("Refreshing lobby list.");
        _actions?.TryRefreshMultiplayerLobbies();
        _nextSourceRefreshTime = 0f;
    }

    private void CreateLobbyClicked()
    {
        ShowHostSetup();
    }

    private void ShowHostSetup()
    {
        _browserListPanel?.gameObject.SetActive(false);
        _detailsPanel?.gameObject.SetActive(false);
        _hostPanel?.gameObject.SetActive(true);
        _passwordPanel?.gameObject.SetActive(false);
        if (!_hostMissionsLoaded)
        {
            LoadHostMissions();
        }

        RefreshHostControls();
    }

    private void HideHostSetup()
    {
        _browserListPanel?.gameObject.SetActive(true);
        _detailsPanel?.gameObject.SetActive(true);
        _hostPanel?.gameObject.SetActive(false);
        RefreshFromOriginalLobbyList();
    }

    private void LoadHostMissions()
    {
        _hostMissionsLoaded = true;
        _hostMissions.Clear();
        _hostMissionPage = 0;
        _hostSelectedMissionIndex = 0;

        try
        {
            MissionGroup.Init();
            var entries = MissionSaveLoad.QuickLoadMany(MissionGroup.All.GetMissions());
            foreach (var entry in entries)
            {
                if (!HasTag(entry.mission, MissionTag.Multiplayer)) continue;

                _hostMissions.Add(new HostMissionEntry(entry.key, entry.mission));
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Failed to load native multiplayer host mission list: {exception}");
            if (_hostStatusText != null)
            {
                _hostStatusText.text = "Failed to load multiplayer missions.";
            }
        }

        SelectHostMission(_hostMissions.Count > 0 ? 0 : -1);
    }

    private static bool HasTag(MissionQuickLoad mission, MissionTag tag)
    {
        var tags = mission.missionSettings.Tags;
        if (tags == null) return false;

        foreach (var existing in tags)
        {
            if (existing.Equals(tag))
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshHostControls()
    {
        RefreshHostMissionList();
        if (_hostPlayersText != null)
        {
            _hostPlayersText.text = _hostMaxPlayers.ToString();
        }

        if (_hostVisibilityText != null)
        {
            _hostVisibilityText.text = GetHostVisibilityLabel();
        }

        if (_hostPasswordToggleText != null)
        {
            _hostPasswordToggleText.text = _hostPasswordEnabled ? "[X] Enabled" : "[ ] Disabled";
        }

        if (_hostPasswordInput != null)
        {
            _hostPasswordInput.interactable = _hostPasswordEnabled;
        }
    }

    private void RefreshHostMissionList()
    {
        for (var index = 0; index < HostMissionPageSize; index++)
        {
            var missionIndex = _hostMissionPage * HostMissionPageSize + index;
            var hasMission = missionIndex >= 0 && missionIndex < _hostMissions.Count;
            var button = _hostMissionButtons[index];
            var text = _hostMissionButtonTexts[index];
            button.gameObject.SetActive(hasMission);
            if (!hasMission) continue;

            var mission = _hostMissions[missionIndex];
            text.text = $"  {Shorten(mission.Key.Name, 72)}";
            SetButtonColor(button, missionIndex == _hostSelectedMissionIndex ? ButtonSelectedColor : ButtonColor);
        }

        if (_hostMissionPageText != null)
        {
            _hostMissionPageText.text = $"{_hostMissionPage + 1} / {GetLastHostMissionPage() + 1}";
        }
    }

    private void SelectHostMission(int index)
    {
        if (index < 0 || index >= _hostMissions.Count)
        {
            _hostSelectedMissionIndex = -1;
            if (_hostMissionTitleText != null) _hostMissionTitleText.text = "";
            if (_hostMissionDescriptionText != null) _hostMissionDescriptionText.text = "";
            RefreshHostMissionList();
            return;
        }

        _hostSelectedMissionIndex = index;
        var mission = _hostMissions[index];
        if (_hostMissionTitleText != null) _hostMissionTitleText.text = mission.Key.Name;
        if (_hostMissionDescriptionText != null) _hostMissionDescriptionText.text = mission.Mission.missionSettings.description ?? "";
        if (_hostNameInput != null && string.IsNullOrWhiteSpace(_hostNameInput.text))
        {
            _hostNameInput.text = $"{mission.Key.Name} [Hosted by {PlayerSettings.playerName_Unsanitized}]";
        }

        RefreshHostMissionList();
    }

    private void PreviousHostMissionPage()
    {
        if (_hostMissionPage <= 0) return;

        _hostMissionPage--;
        SelectHostMission(Mathf.Clamp(_hostSelectedMissionIndex, _hostMissionPage * HostMissionPageSize, Mathf.Min(_hostMissions.Count - 1, (_hostMissionPage + 1) * HostMissionPageSize - 1)));
    }

    private void NextHostMissionPage()
    {
        if ((_hostMissionPage + 1) * HostMissionPageSize >= _hostMissions.Count) return;

        _hostMissionPage++;
        SelectHostMission(_hostMissionPage * HostMissionPageSize);
    }

    private int GetLastHostMissionPage()
    {
        return Mathf.Max(0, Mathf.CeilToInt(_hostMissions.Count / (float)HostMissionPageSize) - 1);
    }

    private void ChangeHostMaxPlayers(int delta)
    {
        var limit = _actions?.GetTooManyPlayerLimit() ?? 16;
        _hostMaxPlayers = Mathf.Clamp(_hostMaxPlayers + delta, 2, Mathf.Max(2, limit));
        RefreshHostControls();
    }

    private void CycleHostVisibility(int direction)
    {
        var values = new[] { HostLobbyVisibility.Public, HostLobbyVisibility.FriendsOnly, HostLobbyVisibility.Private };
        var currentIndex = Array.IndexOf(values, _hostVisibility);
        if (currentIndex < 0) currentIndex = 0;

        var nextIndex = (currentIndex + direction) % values.Length;
        if (nextIndex < 0) nextIndex += values.Length;

        _hostVisibility = values[nextIndex];
        RefreshHostControls();
    }

    private void ToggleHostPassword()
    {
        _hostPasswordEnabled = !_hostPasswordEnabled;
        RefreshHostControls();
    }

    private string GetHostVisibilityLabel()
    {
        return _hostVisibility switch
        {
            HostLobbyVisibility.FriendsOnly => "FRIENDS",
            HostLobbyVisibility.Private => "PRIVATE",
            _ => "PUBLIC"
        };
    }

    private void HostLobbyClicked()
    {
        if (_isHosting) return;

        if (_hostSelectedMissionIndex < 0 || _hostSelectedMissionIndex >= _hostMissions.Count)
        {
            SetHostStatus("Select a multiplayer mission first.");
            return;
        }

        var password = _hostPasswordEnabled ? _hostPasswordInput?.text?.Trim() : null;
        if (_hostPasswordEnabled && string.IsNullOrEmpty(password))
        {
            SetHostStatus("Enter a password or disable password hosting.");
            return;
        }

        var entry = _hostMissions[_hostSelectedMissionIndex];
        var lobbyName = _hostNameInput?.text;
        if (string.IsNullOrWhiteSpace(lobbyName))
        {
            lobbyName = $"{entry.Key.Name} [Hosted by {PlayerSettings.playerName_Unsanitized}]";
        }

        HostLobbyAsync(entry, lobbyName ?? entry.Key.Name, _hostMaxPlayers, _hostVisibility, password).Forget();
    }

    private async UniTaskVoid HostLobbyAsync(HostMissionEntry entry, string lobbyName, int maxPlayers, HostLobbyVisibility visibility, string? password)
    {
        _isHosting = true;
        SetHostStatus("Creating Steam lobby.");

        try
        {
            if (!entry.Key.TryLoad(out var mission, out var error))
            {
                SetHostStatus(error);
                Debug.LogWarning($"[NOVR] Native multiplayer failed to load host mission '{entry.Key}': {error}");
                return;
            }

            if (SteamLobby.instance == null)
            {
                SetHostStatus("Steam lobby service is not available.");
                return;
            }

            MissionManager.SetMission(mission, checkIfSame: false);
            SteamLobby.instance.CheckRelayLocationTask();
            var hostedLobby = await SteamLobby.instance.HostLobby(maxPlayers, GetSteamLobbyType(visibility));
            if (!hostedLobby.HasValue)
            {
                SetHostStatus("Steam lobby creation failed.");
                return;
            }

            var lobby = hostedLobby.Value;
            SteamLobby.instance.CurrentLobbyName = lobbyName;
            lobby.SetData("name", lobbyName.SanitizeRichText(128));
            lobby.SetData("mission_name", mission.Name.SanitizeRichText(128));
            lobby.SetData("mission_description", mission.missionSettings.description.SanitizeRichText(1000));
            lobby.SetData("mission_pvp_type", MissionTag.GetPvpTypeLobbyString(mission));
            if (TryGetMapName(mission.MapKey, out var mapName))
            {
                lobby.SetData("map_name", mapName);
            }

            var publishedFileId = mission.LoadKey?.WorkshopId;
            if (publishedFileId.HasValue)
            {
                lobby.SetData("mission_workshop_id", publishedFileId.Value.m_PublishedFileId.ToString("X"));
            }

            var hasPassword = !string.IsNullOrEmpty(password);
            if (hasPassword)
            {
                lobby.SetData("short_password", LobbyPassword.GetShortPassword(password));
            }

            var moddedServer = NetworkManagerNuclearOption.ModdedServer == true;
            var allowsEventContent = mission.missionSettings?.allowEventContent == true;
            lobby.SetData("modded_server", LobbyInstance.BoolToTag(moddedServer || allowsEventContent));

            SetHostStatus("Starting host.");
            var options = new HostOptions(SocketType.Steam, GameState.Multiplayer, mission.MapKey)
            {
                MaxConnections = maxPlayers - 1,
                Password = hasPassword ? password : null
            };
            await NetworkManagerNuclearOption.i.StartHostAsync(options);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native multiplayer host failed: {exception}");
            SetHostStatus("Host failed. Check the BepInEx log for details.");
        }
        finally
        {
            _isHosting = false;
        }
    }

    private static ELobbyType GetSteamLobbyType(HostLobbyVisibility visibility)
    {
        return visibility switch
        {
            HostLobbyVisibility.FriendsOnly => ELobbyType.k_ELobbyTypeFriendsOnly,
            HostLobbyVisibility.Private => ELobbyType.k_ELobbyTypePrivate,
            _ => ELobbyType.k_ELobbyTypePublic
        };
    }

    private static bool TryGetMapName(MapKey mapKey, out string mapName)
    {
        var mapLoaders = Resources.FindObjectsOfTypeAll<MapLoader>();
        for (var index = 0; index < mapLoaders.Length; index++)
        {
            if (mapLoaders[index] != null && mapLoaders[index].TryGetMapName(mapKey, out mapName))
            {
                return true;
            }
        }

        mapName = "";
        return false;
    }

    private void SetHostStatus(string status)
    {
        if (_hostStatusText != null)
        {
            _hostStatusText.text = status;
        }
    }

    private void JoinSelectedLobby()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _visibleLobbies.Count) return;

        var lobby = _visibleLobbies[_selectedIndex];
        if (lobby.HasPassword)
        {
            ShowPasswordPanel(lobby);
            return;
        }

        if (_actions?.TryJoinLobby(lobby.Lobby, null, promptIfPasswordNeeded: false) == true)
        {
            SetStatus($"Joining {lobby.LobbyName}.");
        }
        else
        {
            SetStatus($"Could not join {lobby.LobbyName}.");
        }
    }

    private void ShowPasswordPanel(NativeLobbyEntry lobby)
    {
        _pendingPasswordLobby = lobby;
        if (_passwordTitleText != null)
        {
            _passwordTitleText.text = $"PASSWORD REQUIRED\n{Shorten(lobby.LobbyName, 42)}";
        }

        if (_passwordStatusText != null)
        {
            _passwordStatusText.text = "";
        }

        if (_joinPasswordInput != null)
        {
            _joinPasswordInput.text = "";
            _joinPasswordInput.ActivateInputField();
        }

        if (_passwordPanel != null)
        {
            _passwordPanel.gameObject.SetActive(true);
            _passwordPanel.SetAsLastSibling();
        }
    }

    private void HidePasswordPanel()
    {
        _pendingPasswordLobby = null;
        if (_passwordPanel != null)
        {
            _passwordPanel.gameObject.SetActive(false);
        }
    }

    private void SubmitJoinPassword()
    {
        if (!_pendingPasswordLobby.HasValue) return;

        var lobby = _pendingPasswordLobby.Value;
        var password = _joinPasswordInput?.text?.Trim();
        if (string.IsNullOrEmpty(password))
        {
            if (_passwordStatusText != null)
            {
                _passwordStatusText.text = "Enter the server password.";
            }

            return;
        }

        if (!LobbyPassword.TestShortPassword(lobby.Lobby, password))
        {
            if (_passwordStatusText != null)
            {
                _passwordStatusText.text = "Password incorrect.";
            }

            return;
        }

        HidePasswordPanel();
        if (_actions?.TryJoinLobby(lobby.Lobby, password, promptIfPasswordNeeded: false) == true)
        {
            SetStatus($"Joining {lobby.LobbyName}.");
        }
        else
        {
            SetStatus($"Could not join {lobby.LobbyName}.");
        }
    }

    private void PreviousPage()
    {
        if (_page <= 0) return;

        _page--;
        SelectLobby(Mathf.Clamp(_selectedIndex, _page * PageSize, Mathf.Min(_visibleLobbies.Count - 1, (_page + 1) * PageSize - 1)));
    }

    private void NextPage()
    {
        if ((_page + 1) * PageSize >= _visibleLobbies.Count) return;

        _page++;
        SelectLobby(_page * PageSize);
    }

    private void BackToMainMenu()
    {
        _actions?.TryInvokeCurrentMenuButton("Main Menu", "MAIN MENU", "BACK", "< BACK", "MenuExit_Button");
    }

    private void SetStatus(string status)
    {
        if (_statusText != null)
        {
            _statusText.text = status;
        }
    }

    private int GetLastPage()
    {
        return Mathf.Max(0, Mathf.CeilToInt(_visibleLobbies.Count / (float)PageSize) - 1);
    }

    private static bool IsLobbyItemShown(LobbyListItem item)
    {
        if (LobbyItemShownField?.GetValue(item) is bool shown)
        {
            return shown;
        }

        return item.gameObject.activeInHierarchy;
    }

    private static int CompareText(string left, string right)
    {
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSearchTerm(string value, string term)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int NullableIntSortKey(int? value)
    {
        return value.GetValueOrDefault(int.MaxValue);
    }

    private RectTransform CreateContainer(string name, RectTransform parent, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;
        return rectTransform;
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        return CreateImage(name, parent, color, anchoredPosition, size);
    }

    private RectTransform CreateImage(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var image = gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }

    private Text CreateText(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var textComponent = gameObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = _font;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private InputField CreateInputField(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, string placeholder, InputField.ContentType contentType = InputField.ContentType.Standard)
    {
        var rectTransform = CreateImage(name, parent, new Color(0.12f, 0.15f, 0.16f, 0.96f), anchoredPosition, size);
        var inputField = rectTransform.gameObject.AddComponent<InputField>();
        inputField.targetGraphic = rectTransform.GetComponent<Image>();
        inputField.contentType = contentType;
        inputField.lineType = InputField.LineType.SingleLine;

        var text = CreateText($"{name} Text", rectTransform, "", Vector2.zero, new Vector2(size.x - 18f, size.y - 6f), 14, TextAnchor.MiddleLeft, Color.white);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        var placeholderText = CreateText($"{name} Placeholder", rectTransform, placeholder, Vector2.zero, new Vector2(size.x - 18f, size.y - 6f), 14, TextAnchor.MiddleLeft, new Color(0.72f, 0.78f, 0.80f, 0.65f));
        placeholderText.fontStyle = FontStyle.Italic;

        inputField.textComponent = text;
        inputField.placeholder = placeholderText;
        return inputField;
    }

    private Button CreateMenuButton(string label, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 15, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var button = CreateButton(label, parent, anchoredPosition, size, color, onClick);
        CreateText($"{label} Text", (RectTransform)button.transform, label, Vector2.zero, size, fontSize, alignment, Color.white);
        return button;
    }

    private Button CreateButton(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var rectTransform = CreateImage(name, parent, color, anchoredPosition, size);
        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = rectTransform.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        colors.selectedColor = ButtonHoverColor;
        colors.disabledColor = new Color(0.16f, 0.18f, 0.19f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        return button;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        var image = button.targetGraphic;
        if (image != null)
        {
            image.color = color;
        }

        var colors = button.colors;
        colors.normalColor = color;
        button.colors = colors;
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
    }

    private enum LobbySortColumn
    {
        Type,
        Password,
        Mods,
        Server,
        Lobby,
        Mission,
        Players,
        Ping
    }

    private enum HostLobbyVisibility
    {
        Public,
        FriendsOnly,
        Private
    }

    private enum PingDistanceFilter
    {
        Any,
        Nearby,
        Regional,
        Worldwide
    }

    private sealed class NativeLobbyRow
    {
        public NativeLobbyRow(Button button, Text typeText, Text lockText, Text modsText, Text hostText, Text lobbyText, Text missionText, Text playersText, Text pingText)
        {
            Button = button;
            TypeText = typeText;
            LockText = lockText;
            ModsText = modsText;
            HostText = hostText;
            LobbyText = lobbyText;
            MissionText = missionText;
            PlayersText = playersText;
            PingText = pingText;
        }

        public Button Button { get; }
        public Text TypeText { get; }
        public Text LockText { get; }
        public Text ModsText { get; }
        public Text HostText { get; }
        public Text LobbyText { get; }
        public Text MissionText { get; }
        public Text PlayersText { get; }
        public Text PingText { get; }
    }

    private readonly struct SortHeaderBinding
    {
        public SortHeaderBinding(LobbySortColumn column, string label, Button button, Text text)
        {
            Column = column;
            Label = label;
            Button = button;
            Text = text;
        }

        public LobbySortColumn Column { get; }
        public string Label { get; }
        public Button Button { get; }
        public Text Text { get; }
    }

    private readonly struct FilterToggleBinding
    {
        public FilterToggleBinding(string label, Button button, Text text, Func<bool> isEnabled)
        {
            Label = label;
            Button = button;
            Text = text;
            IsEnabled = isEnabled;
        }

        public string Label { get; }
        public Button Button { get; }
        public Text Text { get; }
        public Func<bool> IsEnabled { get; }
    }

    private readonly struct HostMissionEntry
    {
        public HostMissionEntry(MissionKey key, MissionQuickLoad mission)
        {
            Key = key;
            Mission = mission;
        }

        public MissionKey Key { get; }
        public MissionQuickLoad Mission { get; }
    }

    private readonly struct NativeLobbyEntry
    {
        public NativeLobbyEntry(LobbyListItem item)
        {
            Lobby = item.lobby;
            LobbyName = string.IsNullOrWhiteSpace(item.LobbyName) ? "Unnamed Lobby" : item.LobbyName;
            MissionName = string.IsNullOrWhiteSpace(item.MissionName) ? "Unknown Mission" : item.MissionName;
            MapName = string.IsNullOrWhiteSpace(item.MapName) ? "Unknown Map" : item.MapName;
            Ping = item.Ping;
            PingText = Ping.HasValue ? $"{Ping.Value} ms" : "--";
            IsFull = item.IsFull;
            IsDedicated = item.lobby.DedicatedServer || item.IsServer;
            ServerTypeText = IsDedicated ? "Dedicated" : "Player Host";
            ServerColumnText = IsDedicated ? "DED" : "PLAYER";
            IsModded = item.lobby.ModdedServer;
            ModsText = IsModded ? "MOD" : "VANILLA";
            HasPassword = item.lobby.IsPasswordProtected(out _);
            AccessText = HasPassword ? "PWD" : "OPEN";
            MissionType = item.lobby.MissionPvpType;
            MissionTypeText = MissionTypeLabel(MissionType);
            Description = string.IsNullOrWhiteSpace(item.lobby.MissionDescriptionSanitized)
                ? "No mission description available."
                : item.lobby.MissionDescriptionSanitized;

            if (item.lobby.GetPlayerCounts(out var currentPlayers, out var maxPlayers))
            {
                CurrentPlayers = currentPlayers;
                MaxPlayers = maxPlayers;
                PlayerCountText = $"{currentPlayers}/{maxPlayers}";
            }
            else
            {
                CurrentPlayers = item.PlayerCount;
                MaxPlayers = 0;
                PlayerCountText = $"{item.PlayerCount}";
            }

            FlagsText = BuildFlagsText(HasPassword, IsModded, IsFull, IsDedicated, MissionType);
            StatusText = $"{ServerTypeText}   {MissionTypeLabel(MissionType)}   {(HasPassword ? "Password" : "Open")}   {(IsModded ? "Modded" : "Vanilla")}";
            if (IsFull)
            {
                StatusText += "   Full";
            }
        }

        public LobbyInstance Lobby { get; }
        public string LobbyName { get; }
        public string MissionName { get; }
        public string MapName { get; }
        public string FlagsText { get; }
        public string PlayerCountText { get; }
        public int CurrentPlayers { get; }
        public int MaxPlayers { get; }
        public int? Ping { get; }
        public string PingText { get; }
        public bool IsFull { get; }
        public bool IsDedicated { get; }
        public bool IsModded { get; }
        public bool HasPassword { get; }
        public MissionPvpType MissionType { get; }
        public string MissionTypeText { get; }
        public string AccessText { get; }
        public string ModsText { get; }
        public string ServerColumnText { get; }
        public string ServerTypeText { get; }
        public string StatusText { get; }
        public string Description { get; }

        private static string BuildFlagsText(bool hasPassword, bool isModded, bool isFull, bool isDedicated, MissionPvpType missionType)
        {
            var flags = new List<string>();
            flags.Add(MissionTypeLabel(missionType));
            if (hasPassword) flags.Add("PWD");
            if (isModded) flags.Add("MOD");
            if (isFull) flags.Add("FULL");
            if (isDedicated) flags.Add("DED");

            return string.Join(" ", flags);
        }

        private static string MissionTypeLabel(MissionPvpType missionType)
        {
            return missionType switch
            {
                MissionPvpType.Pve => "PvE",
                MissionPvpType.Pvp => "PvP",
                _ => "Any"
            };
        }
    }
}
