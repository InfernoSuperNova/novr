using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NuclearOption.ModScripts;
using NuclearOption.Workshop;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public class NativeWorkshopPanel : MonoBehaviour
{
    private const int PageSize = 10;

    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.92f);
    private static readonly Color PanelColor = new(0.05f, 0.06f, 0.065f, 0.94f);
    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);
    private static readonly Color ButtonSelectedColor = new(0.44f, 0.49f, 0.50f, 1f);
    private static readonly Color ButtonHoverColor = new(0.34f, 0.40f, 0.42f, 1f);
    private static readonly Color ButtonPressedColor = new(0.16f, 0.20f, 0.22f, 1f);
    private static readonly Color BackButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);
    private static readonly Color ActionButtonColor = new(0.12f, 0.34f, 0.20f, 0.96f);
    private static readonly Color DisabledButtonColor = new(0.16f, 0.18f, 0.19f, 0.55f);

    private readonly List<SteamWorkshopItem> _items = new();
    private readonly List<SteamWorkshopItem> _visibleItems = new();
    private readonly List<WorkshopRow> _rows = new();
    private readonly List<Button> _tabButtons = new();

    private NativeGameActionAdapter? _actions;
    private RectTransform? _container;
    private SteamWorkshop? _steamWorkshop;
    private Font? _font;
    private InputField? _searchInput;
    private Text? _statusText;
    private Text? _pageText;
    private Text? _orderText;
    private Text? _titleText;
    private Text? _ownerText;
    private Text? _descriptionText;
    private Text? _detailsStatusText;
    private Text? _subscribeText;
    private Image? _previewImage;
    private Button? _subscribeButton;
    private Button? _openLocalButton;
    private Button? _nextButton;
    private Button? _loadMoreButton;
    private WorkshopTab _tab = WorkshopTab.Missions;
    private OrderBy _orderBy = OrderBy.Trend30Days;
    private int _displayPage;
    private int _steamPage = 1;
    private int _selectedIndex = -1;
    private bool _hasMoreSteamPages;
    private bool _queryInProgress;
    private bool _itemActionInProgress;
    private bool _wasVisible;
    private SteamWorkshopItem? _selectedItem;
    private CancellationTokenSource? _previewCancellation;

    public void Initialize(NativeGameActionAdapter actions, RectTransform root)
    {
        _actions = actions;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _steamWorkshop = gameObject.GetComponent<SteamWorkshop>() ?? gameObject.AddComponent<SteamWorkshop>();
        BuildLayout(root);
    }

    public void SetVisible(bool visible)
    {
        if (_container == null) return;

        if (visible && !_wasVisible)
        {
            SetStatus("Refreshing Workshop.");
            RefreshFirstSteamPage();
        }

        _wasVisible = visible;
        if (_container.gameObject.activeSelf != visible)
        {
            _container.gameObject.SetActive(visible);
        }
    }

    private void BuildLayout(RectTransform root)
    {
        _container = CreateContainer("Native Workshop", root, root.sizeDelta);
        CreateImage("Background", _container, BackgroundColor, Vector2.zero, _container.sizeDelta);
        CreateText("Header", _container, "WORKSHOP", new Vector2(0f, 505f), new Vector2(1200f, 32f), 22, TextAnchor.MiddleCenter, Color.white);

        var listPanel = CreatePanel("Workshop List Panel", _container, PanelColor, new Vector2(-425f, -15f), new Vector2(1030f, 950f));
        CreateText("List Header", listPanel, "BROWSE ITEMS", new Vector2(0f, 420f), new Vector2(940f, 30f), 18, TextAnchor.MiddleCenter, Color.white);

        CreateTabButton(listPanel, WorkshopTab.Missions, "MISSIONS", new Vector2(-360f, 370f));
        CreateTabButton(listPanel, WorkshopTab.AircraftLiveries, "LIVERIES", new Vector2(-180f, 370f));
        CreateText("Order Label", listPanel, "ORDER", new Vector2(50f, 370f), new Vector2(90f, 26f), 11, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("<", listPanel, new Vector2(120f, 370f), new Vector2(46f, 32f), ButtonColor, () => CycleOrder(-1), 15);
        _orderText = CreateText("Order", listPanel, "", new Vector2(220f, 370f), new Vector2(140f, 32f), 12, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", listPanel, new Vector2(320f, 370f), new Vector2(46f, 32f), ButtonColor, () => CycleOrder(1), 15);

        CreateText("Search Label", listPanel, "SEARCH", new Vector2(-418f, 320f), new Vector2(90f, 26f), 11, TextAnchor.MiddleCenter, Color.white);
        _searchInput = CreateInputField("Workshop Search", listPanel, new Vector2(-190f, 320f), new Vector2(340f, 32f), "Name or author");
        _searchInput.onValueChanged.AddListener(_ => ApplySearchFromFirstPage());
        CreateMenuButton("CLEAR", listPanel, new Vector2(40f, 320f), new Vector2(86f, 32f), ButtonColor, ClearSearch, 11);
        CreateMenuButton("REFRESH", listPanel, new Vector2(200f, 320f), new Vector2(120f, 32f), ButtonColor, RefreshFirstSteamPage, 12);
        CreateMenuButton("OPEN STEAM", listPanel, new Vector2(365f, 320f), new Vector2(150f, 32f), ButtonColor, SteamWorkshop.OpenWorkshopPage, 12);

        CreateText("Name Header", listPanel, "NAME", new Vector2(-215f, 270f), new Vector2(420f, 28f), 12, TextAnchor.MiddleLeft, Color.white);
        CreateText("Author Header", listPanel, "AUTHOR", new Vector2(185f, 270f), new Vector2(180f, 28f), 12, TextAnchor.MiddleLeft, Color.white);
        CreateText("State Header", listPanel, "STATE", new Vector2(365f, 270f), new Vector2(120f, 28f), 12, TextAnchor.MiddleCenter, Color.white);

        for (var index = 0; index < PageSize; index++)
        {
            var rowIndex = index;
            _rows.Add(CreateWorkshopRow(listPanel, index, new Vector2(0f, 224f - index * 50f), () => SelectItem(_displayPage * PageSize + rowIndex)));
        }

        CreateMenuButton("<", listPanel, new Vector2(-130f, -420f), new Vector2(62f, 34f), ButtonColor, PreviousPage, 16);
        _pageText = CreateText("Page", listPanel, "", new Vector2(0f, -420f), new Vector2(210f, 34f), 13, TextAnchor.MiddleCenter, Color.white);
        _nextButton = CreateMenuButton(">", listPanel, new Vector2(130f, -420f), new Vector2(62f, 34f), ButtonColor, NextPage, 16);
        _loadMoreButton = CreateMenuButton("MORE", listPanel, new Vector2(360f, -420f), new Vector2(130f, 34f), ButtonColor, LoadNextSteamPage, 13);

        var detailsPanel = CreatePanel("Workshop Details Panel", _container, PanelColor, new Vector2(595f, -15f), new Vector2(760f, 950f));
        CreateText("Details Header", detailsPanel, "ITEM DETAILS", new Vector2(0f, 420f), new Vector2(700f, 30f), 18, TextAnchor.MiddleCenter, Color.white);
        _previewImage = CreateImage("Preview", detailsPanel, new Color(0.12f, 0.14f, 0.15f, 1f), new Vector2(0f, 245f), new Vector2(660f, 270f)).GetComponent<Image>();
        _titleText = CreateText("Item Title", detailsPanel, "", new Vector2(0f, 65f), new Vector2(680f, 70f), 20, TextAnchor.MiddleCenter, Color.white);
        _ownerText = CreateText("Item Owner", detailsPanel, "", new Vector2(0f, 5f), new Vector2(680f, 34f), 14, TextAnchor.MiddleCenter, new Color(0.82f, 0.86f, 0.72f, 1f));
        _descriptionText = CreateText("Item Description", detailsPanel, "", new Vector2(0f, -180f), new Vector2(680f, 270f), 13, TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.90f, 1f));
        _detailsStatusText = CreateText("Item Status", detailsPanel, "", new Vector2(0f, -340f), new Vector2(680f, 55f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));

        _subscribeButton = CreateMenuButton("SUBSCRIBE", detailsPanel, new Vector2(-240f, -410f), new Vector2(170f, 38f), ActionButtonColor, ToggleSubscribe, 14);
        _subscribeText = _subscribeButton.GetComponentInChildren<Text>();
        CreateMenuButton("STEAM PAGE", detailsPanel, new Vector2(0f, -410f), new Vector2(170f, 38f), ButtonColor, OpenSelectedSteamPage, 13);
        _openLocalButton = CreateMenuButton("LOCAL FILES", detailsPanel, new Vector2(240f, -410f), new Vector2(170f, 38f), ButtonColor, OpenSelectedLocalFiles, 13);

        CreateMenuButton("BACK", _container, new Vector2(-860f, -520f), new Vector2(190f, 44f), BackButtonColor, BackToMainMenu, 15);
        CreateMenuButton("UPDATE ALL", _container, new Vector2(860f, -520f), new Vector2(190f, 44f), ActionButtonColor, UpdateAllSubscribed, 14);

        _statusText = CreateText("Status", _container, "", new Vector2(0f, -520f), new Vector2(960f, 42f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));
        RefreshStaticLabels();
        SelectItem(-1);
        _container.gameObject.SetActive(false);
    }

    private void CreateTabButton(RectTransform parent, WorkshopTab tab, string label, Vector2 anchoredPosition)
    {
        var button = CreateMenuButton(label, parent, anchoredPosition, new Vector2(150f, 34f), ButtonColor, () => SelectTab(tab), 13);
        _tabButtons.Add(button);
    }

    private WorkshopRow CreateWorkshopRow(RectTransform parent, int index, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        var button = CreateButton($"Workshop Row {index}", parent, anchoredPosition, new Vector2(960f, 40f), ButtonColor, onClick);
        var rowTransform = (RectTransform)button.transform;
        return new WorkshopRow(
            button,
            CreateText("Name", rowTransform, "", new Vector2(-215f, 0f), new Vector2(420f, 32f), 13, TextAnchor.MiddleLeft, Color.white),
            CreateText("Owner", rowTransform, "", new Vector2(185f, 0f), new Vector2(180f, 32f), 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.86f, 0.72f, 1f)),
            CreateText("State", rowTransform, "", new Vector2(365f, 0f), new Vector2(120f, 32f), 12, TextAnchor.MiddleCenter, Color.white));
    }

    private void SelectTab(WorkshopTab tab)
    {
        if (_tab == tab) return;

        _tab = tab;
        RefreshFirstSteamPage();
    }

    private void CycleOrder(int direction)
    {
        var values = new[] { OrderBy.Trend30Days, OrderBy.TopAllTime, OrderBy.New };
        var index = Array.IndexOf(values, _orderBy);
        if (index < 0) index = 0;

        _orderBy = values[WrapIndex(index + direction, values.Length)];
        RefreshFirstSteamPage();
    }

    private void RefreshFirstSteamPage()
    {
        if (_queryInProgress) return;

        _steamPage = 1;
        _displayPage = 0;
        QuerySteamPageAsync(clearExisting: true).Forget();
    }

    private void LoadNextSteamPage()
    {
        if (_queryInProgress || !_hasMoreSteamPages) return;

        _steamPage++;
        _displayPage = 0;
        QuerySteamPageAsync(clearExisting: true).Forget();
    }

    private async UniTaskVoid QuerySteamPageAsync(bool clearExisting)
    {
        if (_steamWorkshop == null || _queryInProgress) return;

        _queryInProgress = true;
        SetStatus($"Loading Workshop {_tab} page {_steamPage}.");
        RefreshButtons();

        try
        {
            if (clearExisting)
            {
                _items.Clear();
                _visibleItems.Clear();
                SelectItem(-1);
                RefreshRows();
            }

            _hasMoreSteamPages = await _steamWorkshop.RefreshItems(_orderBy, GetCurrentTag(), _items, (uint)_steamPage);
            ApplySearchFromCurrentPage();
            SetStatus($"Loaded {_items.Count} Workshop items from page {_steamPage}.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native Workshop refresh failed: {exception}");
            _items.Clear();
            _visibleItems.Clear();
            _hasMoreSteamPages = false;
            SelectItem(-1);
            RefreshRows();
            SetStatus("Workshop refresh failed. Check the BepInEx log.");
        }
        finally
        {
            _queryInProgress = false;
            RefreshButtons();
        }
    }

    private void ApplySearchFromFirstPage()
    {
        _displayPage = 0;
        ApplySearchFromCurrentPage();
    }

    private void ApplySearchFromCurrentPage()
    {
        _visibleItems.Clear();
        var searchText = _searchInput?.text ?? "";

        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            if (!MatchesSearch(item, searchText)) continue;

            _visibleItems.Add(item);
        }

        _displayPage = Mathf.Clamp(_displayPage, 0, GetLastDisplayPage());
        RefreshRows();
        SelectItem(_visibleItems.Count > 0 ? Mathf.Clamp(_selectedIndex, 0, _visibleItems.Count - 1) : -1);
    }

    private static bool MatchesSearch(SteamWorkshopItem item, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText)) return true;

        var terms = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < terms.Length; index++)
        {
            var term = terms[index];
            if (!ContainsSearchTerm(item.Name, term) &&
                !ContainsSearchTerm(item.OwnerName, term) &&
                !ContainsSearchTerm(item.Description, term))
            {
                return false;
            }
        }

        return true;
    }

    private void RefreshRows()
    {
        RefreshStaticLabels();

        for (var index = 0; index < _rows.Count; index++)
        {
            var itemIndex = _displayPage * PageSize + index;
            var hasItem = itemIndex >= 0 && itemIndex < _visibleItems.Count;
            var row = _rows[index];
            row.Button.gameObject.SetActive(hasItem);
            if (!hasItem) continue;

            var item = _visibleItems[itemIndex];
            row.NameText.text = "  " + Shorten(item.Name, 54);
            row.OwnerText.text = Shorten(item.OwnerName, 22);
            row.StateText.text = item.Subscribed ? "INSTALLED" : "";
            SetButtonColor(row.Button, itemIndex == _selectedIndex ? ButtonSelectedColor : ButtonColor);
        }

        if (_pageText != null)
        {
            _pageText.text = $"{_displayPage + 1} / {GetLastDisplayPage() + 1}   Steam {_steamPage}";
        }
    }

    private void SelectItem(int index)
    {
        if (_selectedItem != null)
        {
            _selectedItem.OwnerNameChanged -= OnSelectedOwnerNameChanged;
        }

        if (index < 0 || index >= _visibleItems.Count)
        {
            _selectedIndex = -1;
            _selectedItem = null;
            if (_titleText != null) _titleText.text = "";
            if (_ownerText != null) _ownerText.text = "";
            if (_descriptionText != null) _descriptionText.text = "";
            if (_detailsStatusText != null) _detailsStatusText.text = "Select a Workshop item.";
            if (_previewImage != null)
            {
                _previewImage.sprite = null;
                _previewImage.color = new Color(0.12f, 0.14f, 0.15f, 1f);
            }

            RefreshButtons();
            RefreshRows();
            return;
        }

        _selectedIndex = index;
        _selectedItem = _visibleItems[index];
        _selectedItem.OwnerNameChanged += OnSelectedOwnerNameChanged;

        if (_titleText != null) _titleText.text = _selectedItem.Name;
        if (_ownerText != null) _ownerText.text = GetOwnerText(_selectedItem);
        if (_descriptionText != null) _descriptionText.text = _selectedItem.Description ?? "";
        if (_detailsStatusText != null) _detailsStatusText.text = _selectedItem.Subscribed ? "Installed locally." : "Not installed.";
        if (_previewImage != null)
        {
            _selectedItem.SetPreviewImageAsync(_previewImage, ref _previewCancellation).Forget();
        }

        RefreshButtons();
        RefreshRows();
    }

    private void OnSelectedOwnerNameChanged()
    {
        if (_selectedItem == null || _ownerText == null) return;

        _ownerText.text = GetOwnerText(_selectedItem);
        RefreshRows();
    }

    private static string GetOwnerText(SteamWorkshopItem item)
    {
        return string.IsNullOrWhiteSpace(item.OwnerName)
            ? "Author unknown"
            : $"By {item.OwnerName}";
    }

    private void ToggleSubscribe()
    {
        if (_selectedItem == null || _steamWorkshop == null || _itemActionInProgress) return;

        ToggleSubscribeAsync(_selectedItem).Forget();
    }

    private async UniTaskVoid ToggleSubscribeAsync(SteamWorkshopItem item)
    {
        if (_steamWorkshop == null) return;

        _itemActionInProgress = true;
        RefreshButtons();

        try
        {
            if (item.Subscribed)
            {
                SetDetailsStatus("Unsubscribing.");
                await _steamWorkshop.Unsubscribe(item);
                SetDetailsStatus("Unsubscribed.");
            }
            else
            {
                SetDetailsStatus("Subscribing and downloading.");
                await _steamWorkshop.DownloadItem(item);
                SetDetailsStatus("Installed locally.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native Workshop subscribe action failed: {exception}");
            SetDetailsStatus("Workshop item action failed. Check the BepInEx log.");
        }
        finally
        {
            _itemActionInProgress = false;
            RefreshButtons();
            RefreshRows();
        }
    }

    private void UpdateAllSubscribed()
    {
        if (_itemActionInProgress) return;

        UpdateAllSubscribedAsync().Forget();
    }

    private async UniTaskVoid UpdateAllSubscribedAsync()
    {
        _itemActionInProgress = true;
        RefreshButtons();
        SetStatus("Updating subscribed Workshop items.");

        try
        {
            await SteamWorkshop.UpdateAllSubscribedItems();
            SetStatus("Subscribed Workshop items are up to date.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native Workshop update-all failed: {exception}");
            SetStatus("Workshop update-all failed. Check the BepInEx log.");
        }
        finally
        {
            _itemActionInProgress = false;
            RefreshButtons();
        }
    }

    private void OpenSelectedSteamPage()
    {
        _selectedItem?.OpenSteamPage();
    }

    private void OpenSelectedLocalFiles()
    {
        if (_selectedItem == null || !_selectedItem.Subscribed) return;

        _selectedItem.OpenLocalContent();
    }

    private void PreviousPage()
    {
        if (_displayPage <= 0) return;

        _displayPage--;
        SelectItem(_displayPage * PageSize);
    }

    private void NextPage()
    {
        if ((_displayPage + 1) * PageSize >= _visibleItems.Count) return;

        _displayPage++;
        SelectItem(_displayPage * PageSize);
    }

    private void ClearSearch()
    {
        if (_searchInput == null) return;

        if (string.IsNullOrEmpty(_searchInput.text))
        {
            ApplySearchFromFirstPage();
            return;
        }

        _searchInput.text = "";
    }

    private void BackToMainMenu()
    {
        if (_actions?.TryCloseWorkshopMenu() != true)
        {
            _actions?.TryInvokeCurrentMenuButton("Back", "< BACK", "BACK", "MenuExit_Button");
        }
    }

    private void RefreshStaticLabels()
    {
        if (_orderText != null)
        {
            _orderText.text = GetOrderLabel(_orderBy);
        }

        for (var index = 0; index < _tabButtons.Count; index++)
        {
            var selected = (WorkshopTab)index == _tab;
            SetButtonColor(_tabButtons[index], selected ? ButtonSelectedColor : ButtonColor);
        }

        RefreshButtons();
    }

    private void RefreshButtons()
    {
        if (_subscribeButton != null)
        {
            var canSubscribe = _selectedItem != null && !_itemActionInProgress;
            _subscribeButton.interactable = canSubscribe;
            SetButtonColor(_subscribeButton, canSubscribe ? ActionButtonColor : DisabledButtonColor);
        }

        if (_subscribeText != null)
        {
            _subscribeText.text = _selectedItem?.Subscribed == true ? "UNSUBSCRIBE" : "SUBSCRIBE";
        }

        if (_openLocalButton != null)
        {
            var canOpenLocal = _selectedItem?.Subscribed == true;
            _openLocalButton.interactable = canOpenLocal;
            SetButtonColor(_openLocalButton, canOpenLocal ? ButtonColor : DisabledButtonColor);
        }

        if (_nextButton != null)
        {
            _nextButton.interactable = (_displayPage + 1) * PageSize < _visibleItems.Count;
        }

        if (_loadMoreButton != null)
        {
            _loadMoreButton.interactable = _hasMoreSteamPages && !_queryInProgress;
            SetButtonColor(_loadMoreButton, _loadMoreButton.interactable ? ButtonColor : DisabledButtonColor);
        }
    }

    private void SetStatus(string status)
    {
        if (_statusText != null)
        {
            _statusText.text = status;
        }
    }

    private void SetDetailsStatus(string status)
    {
        if (_detailsStatusText != null)
        {
            _detailsStatusText.text = status;
        }
    }

    private string GetCurrentTag()
    {
        return _tab == WorkshopTab.AircraftLiveries
            ? ModTypes.AircraftLivery.Tag
            : ModTypes.Missions.Tag;
    }

    private static string GetOrderLabel(OrderBy orderBy)
    {
        return orderBy switch
        {
            OrderBy.TopAllTime => "TOP",
            OrderBy.New => "NEW",
            _ => "TRENDING"
        };
    }

    private int GetLastDisplayPage()
    {
        return Mathf.Max(0, Mathf.CeilToInt(_visibleItems.Count / (float)PageSize) - 1);
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

    private InputField CreateInputField(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, string placeholder)
    {
        var rectTransform = CreateImage(name, parent, new Color(0.12f, 0.15f, 0.16f, 0.96f), anchoredPosition, size);
        var inputField = rectTransform.gameObject.AddComponent<InputField>();
        inputField.targetGraphic = rectTransform.GetComponent<Image>();
        inputField.contentType = InputField.ContentType.Standard;
        inputField.lineType = InputField.LineType.SingleLine;

        var text = CreateText($"{name} Text", rectTransform, "", Vector2.zero, new Vector2(size.x - 18f, size.y - 6f), 14, TextAnchor.MiddleLeft, Color.white);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        var placeholderText = CreateText($"{name} Placeholder", rectTransform, placeholder, Vector2.zero, new Vector2(size.x - 18f, size.y - 6f), 14, TextAnchor.MiddleLeft, new Color(0.72f, 0.78f, 0.80f, 0.65f));
        placeholderText.fontStyle = FontStyle.Italic;

        inputField.textComponent = text;
        inputField.placeholder = placeholderText;
        return inputField;
    }

    private Button CreateMenuButton(string label, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 15)
    {
        var button = CreateButton(label, parent, anchoredPosition, size, color, onClick);
        CreateText($"{label} Text", (RectTransform)button.transform, label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white);
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
        colors.disabledColor = DisabledButtonColor;
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

    private static bool ContainsSearchTerm(string value, string term)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0) return 0;

        var wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
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

    private enum WorkshopTab
    {
        Missions,
        AircraftLiveries
    }

    private readonly struct WorkshopRow
    {
        public WorkshopRow(Button button, Text nameText, Text ownerText, Text stateText)
        {
            Button = button;
            NameText = nameText;
            OwnerText = ownerText;
            StateText = stateText;
        }

        public Button Button { get; }
        public Text NameText { get; }
        public Text OwnerText { get; }
        public Text StateText { get; }
    }
}
