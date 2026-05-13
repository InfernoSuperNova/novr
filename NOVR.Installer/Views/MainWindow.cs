using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NOVR.Installer.ViewModels;

namespace NOVR.Installer.Views;

public sealed class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        Title = InstallerConstants.AppName + " Installer";
        Width = 780;
        Height = 560;
        MinWidth = 680;
        MinHeight = 500;
        DataContext = _viewModel;
        _viewModel.SetFolderBrowser(BrowseForFolderAsync);
        Content = BuildContent();
        Opened += async (_, _) => await _viewModel.InitializeAsync();
    }

    private async Task<string?> BrowseForFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select Nuclear Option folder",
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.Path.LocalPath;
    }

    private Control BuildContent()
    {
        var root = new StackPanel
        {
            Spacing = 18,
            Margin = new Avalonia.Thickness(28)
        };

        root.Children.Add(new TextBlock
        {
            Text = "Nuclear Option VR Installer",
            FontSize = 28,
            FontWeight = FontWeight.Bold
        });

        root.Children.Add(new TextBlock
        {
            Text = "Install, update, repair, or remove NOVR. BepInEx 5 will be installed automatically if needed.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85
        });

        var pathPanel = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        var pathBox = new TextBox
        {
            Watermark = "Nuclear Option folder",
            MinHeight = 38
        };
        pathBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.GamePath)) { Mode = Avalonia.Data.BindingMode.TwoWay });

        var browseButton = new Button
        {
            Content = "Browse...",
            Margin = new Avalonia.Thickness(10, 0, 0, 0),
            MinHeight = 38
        };
        browseButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.BrowseCommand)));
        Grid.SetColumn(browseButton, 1);

        pathPanel.Children.Add(pathBox);
        pathPanel.Children.Add(browseButton);
        root.Children.Add(pathPanel);

        var status = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        status.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.Status)));
        root.Children.Add(status);

        var details = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = FontFamily.Parse("monospace"),
            Opacity = 0.9
        };
        details.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.Details)));
        root.Children.Add(details);

        var progress = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 6
        };
        progress.Bind(ProgressBar.IsVisibleProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.IsBusy)));
        root.Children.Add(progress);

        root.Children.Add(BuildInstallActions());
        root.Children.Add(BuildInstalledActions());
        root.Children.Add(BuildUninstallFinishActions());

        return root;
    }

    private Control BuildInstallActions()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        panel.Bind(IsVisibleProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.IsInstallMode)));

        var install = new Button
        {
            MinWidth = 150,
            MinHeight = 42
        };
        install.Bind(ContentControl.ContentProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.PrimaryActionText)));
        install.Bind(Button.CommandProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.PrimaryActionCommand)));

        var rescan = new Button
        {
            Content = "Rescan",
            MinWidth = 110,
            MinHeight = 42
        };
        rescan.Bind(Button.CommandProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.RescanCommand)));

        panel.Children.Add(install);
        panel.Children.Add(rescan);
        return panel;
    }

    private Control BuildInstalledActions()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        panel.Bind(IsVisibleProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.IsInstalledMode)));

        var repair = new Button
        {
            Content = "Repair / Update",
            MinWidth = 150,
            MinHeight = 42
        };
        repair.Bind(Button.CommandProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.RepairUpdateCommand)));

        var uninstall = new Button
        {
            Content = "Uninstall NOVR",
            MinWidth = 150,
            MinHeight = 42
        };
        uninstall.Bind(Button.CommandProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.UninstallCommand)));

        var rescan = new Button
        {
            Content = "Rescan",
            MinWidth = 110,
            MinHeight = 42
        };
        rescan.Bind(Button.CommandProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.RescanCommand)));

        panel.Children.Add(repair);
        panel.Children.Add(uninstall);
        panel.Children.Add(rescan);
        return panel;
    }

    private Control BuildUninstallFinishActions()
    {
        var panel = new StackPanel
        {
            Spacing = 12
        };
        panel.Bind(IsVisibleProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.ShowUninstallFinish)));

        var checkbox = new CheckBox
        {
            Content = "Also remove BepInEx from the game folder"
        };
        checkbox.Bind(CheckBox.IsCheckedProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.RemoveBepInExOnFinish)) { Mode = Avalonia.Data.BindingMode.TwoWay });

        var finish = new Button
        {
            Content = "Finish",
            MinWidth = 120,
            MinHeight = 42,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        finish.Bind(Button.CommandProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.FinishUninstallCommand)));

        panel.Children.Add(checkbox);
        panel.Children.Add(finish);
        return panel;
    }
}
