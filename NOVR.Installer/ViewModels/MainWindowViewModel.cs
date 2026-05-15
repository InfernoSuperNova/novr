using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NOVR.Installer.Models;
using NOVR.Installer.Services;

namespace NOVR.Installer.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly GameLocator _gameLocator = new();
    private readonly GitHubReleaseClient _releaseClient = new();
    private readonly FileSystemInstaller _installer = new();
    private readonly ProtonPrefixService _protonPrefixService = new();

    private GameInstallInfo? _gameInfo;
    private string _gamePath = string.Empty;
    private string _status = "Ready.";
    private string _details = string.Empty;
    private string _primaryActionText = "Install";
    private bool _isBusy;
    private bool _isInstalledMode;
    private bool _removeBepInExOnFinish;
    private bool _showUninstallFinish;
    private Func<Task<string?>>? _browseForFolderAsync;

    public MainWindowViewModel()
    {
        PrimaryActionCommand = new AsyncCommand(PrimaryActionAsync, () => !IsBusy);
        RepairUpdateCommand = new AsyncCommand(RepairUpdateAsync, () => !IsBusy && GameInfo?.IsValid == true);
        UninstallCommand = new AsyncCommand(UninstallAsync, () => !IsBusy && GameInfo?.IsValid == true);
        FinishUninstallCommand = new AsyncCommand(FinishUninstallAsync, () => !IsBusy && GameInfo?.IsValid == true);
        RescanCommand = new AsyncCommand(ScanAsync, () => !IsBusy);
        BrowseCommand = new AsyncCommand(BrowseAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string GamePath
    {
        get => _gamePath;
        set
        {
            if (SetField(ref _gamePath, value))
            {
                _gameInfo = string.IsNullOrWhiteSpace(value) ? null : _gameLocator.Inspect(value);
                RefreshMode();
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public string Details
    {
        get => _details;
        private set => SetField(ref _details, value);
    }

    public string PrimaryActionText
    {
        get => _primaryActionText;
        private set => SetField(ref _primaryActionText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsInstalledMode
    {
        get => _isInstalledMode;
        private set => SetField(ref _isInstalledMode, value);
    }

    public bool IsInstallMode => !IsInstalledMode && !ShowUninstallFinish;

    public bool ShowUninstallFinish
    {
        get => _showUninstallFinish;
        private set
        {
            if (SetField(ref _showUninstallFinish, value))
            {
                OnPropertyChanged(nameof(IsInstallMode));
            }
        }
    }

    public bool RemoveBepInExOnFinish
    {
        get => _removeBepInExOnFinish;
        set => SetField(ref _removeBepInExOnFinish, value);
    }

    public GameInstallInfo? GameInfo
    {
        get => _gameInfo;
        private set
        {
            if (SetField(ref _gameInfo, value))
            {
                GamePath = value?.GameDir ?? GamePath;
                RefreshMode();
            }
        }
    }

    public ICommand PrimaryActionCommand { get; }
    public ICommand RepairUpdateCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand FinishUninstallCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand BrowseCommand { get; }

    public void SetFolderBrowser(Func<Task<string?>> browseForFolderAsync)
    {
        _browseForFolderAsync = browseForFolderAsync;
    }

    public async Task InitializeAsync()
    {
        await ScanAsync();
    }

    private Task ScanAsync()
    {
        return RunBusyAsync(() =>
        {
            ShowUninstallFinish = false;
            RemoveBepInExOnFinish = false;
            Status = "Searching for Nuclear Option...";
            var found = _gameLocator.FindGame();
            if (found is null)
            {
                GameInfo = null;
                GamePath = string.Empty;
                Status = "Nuclear Option was not found automatically.";
                Details = "Choose the game folder manually. It must contain NuclearOption_Data/Managed.";
                PrimaryActionText = "Install";
            }
            else
            {
                GameInfo = found;
                Status = found.ModState == InstallState.FullyInstalled
                    ? "NOVR is already installed."
                    : "Nuclear Option found.";
                Details = BuildDetails(found);
            }

            return Task.CompletedTask;
        });
    }

    private async Task BrowseAsync()
    {
        if (_browseForFolderAsync is null)
        {
            Status = "Folder picker is unavailable. Paste the Nuclear Option path into the box.";
            return;
        }

        var path = await _browseForFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            GamePath = path;
            Status = "Selected game folder.";
            Details = BuildDetails(GameInfo);
        }
    }

    private Task PrimaryActionAsync()
    {
        return InstallOrUpdateAsync(repair: false);
    }

    private Task RepairUpdateAsync()
    {
        return InstallOrUpdateAsync(repair: true);
    }

    private async Task InstallOrUpdateAsync(bool repair)
    {
        var info = ValidateSelectedGame();
        if (info is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var progress = new Progress<string>(message => Status = message);
            var tempDir = Path.Combine(Path.GetTempPath(), "novr-installer-" + Guid.NewGuid().ToString("N"));

            try
            {
                if (!info.HasBepInEx)
                {
                    var bepInExZip = await _releaseClient.DownloadLatestBepInEx5Async(tempDir, progress, CancellationToken.None);
                    await _installer.InstallBepInExAsync(info, bepInExZip, progress, CancellationToken.None);
                    info = _gameLocator.Inspect(info.GameDir);
                }

                var novrZip = await _releaseClient.DownloadLatestNovrReleaseAsync(tempDir, progress, CancellationToken.None);
                await _installer.InstallOrUpdateNovrAsync(info, novrZip, progress, CancellationToken.None);
                var protonMessage = await _protonPrefixService.TryConfigureWinHttpOverrideAsync(info, progress, CancellationToken.None);

                GameInfo = _gameLocator.Inspect(info.GameDir);
                Status = repair ? "Repair/update complete." : "Install complete.";
                Details = BuildDetails(GameInfo) + Environment.NewLine + protonMessage;
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        });
    }

    private async Task UninstallAsync()
    {
        var info = ValidateSelectedGame();
        if (info is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var progress = new Progress<string>(message => Status = message);
            await _installer.UninstallNovrAsync(info, progress, CancellationToken.None);
            GameInfo = _gameLocator.Inspect(info.GameDir);
            ShowUninstallFinish = true;
            IsInstalledMode = false;
            Status = "NOVR was uninstalled.";
            Details = "Optionally remove BepInEx, then click Finish to return to the install screen.";
        });
    }

    private async Task FinishUninstallAsync()
    {
        var info = ValidateSelectedGame();
        if (info is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (RemoveBepInExOnFinish)
            {
                var progress = new Progress<string>(message => Status = message);
                await _installer.UninstallBepInExAsync(info, progress, CancellationToken.None);
            }

            ShowUninstallFinish = false;
            GameInfo = _gameLocator.Inspect(info.GameDir);
            Status = "Ready to install.";
            Details = BuildDetails(GameInfo);
        });
    }

    private GameInstallInfo? ValidateSelectedGame()
    {
        if (string.IsNullOrWhiteSpace(GamePath))
        {
            Status = "Select your Nuclear Option folder first.";
            Details = "The selected path must contain NuclearOption_Data/Managed.";
            return null;
        }

        var info = _gameLocator.Inspect(GamePath);
        if (!info.IsValid)
        {
            Status = "Invalid Nuclear Option folder.";
            Details = info.Message ?? "The selected path must contain NuclearOption_Data/Managed.";
            GameInfo = info;
            return null;
        }

        GameInfo = info;
        return info;
    }

    private void RefreshMode()
    {
        if (ShowUninstallFinish)
        {
            OnPropertyChanged(nameof(IsInstallMode));
            return;
        }

        IsInstalledMode = GameInfo?.ModState == InstallState.FullyInstalled;
        PrimaryActionText = GameInfo?.ModState == InstallState.PartiallyInstalled ? "Repair Install" : "Install";
        OnPropertyChanged(nameof(IsInstallMode));
        RaiseCommandStates();
    }

    private static string BuildDetails(GameInstallInfo? info)
    {
        if (info is null)
        {
            return "No game folder selected.";
        }

        return $"Game: {info.GameDir}{Environment.NewLine}" +
               $"BepInEx: {(info.HasBepInEx ? "installed" : "not installed")}{Environment.NewLine}" +
               $"NOVR: {info.ModState}";
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            Status = "Operation failed.";
            Details = ex.Message; 
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandStates()
    {
        (PrimaryActionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RepairUpdateCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (UninstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (FinishUninstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RescanCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (BrowseCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static void TryDeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
