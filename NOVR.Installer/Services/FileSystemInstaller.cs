using System.IO.Compression;
using NOVR.Installer.Models;

namespace NOVR.Installer.Services;

public sealed class FileSystemInstaller
{
    public async Task InstallBepInExAsync(GameInstallInfo game, string bepInExZip, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Installing BepInEx...");
        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(bepInExZip, game.GameDir, overwriteFiles: true);
        }, cancellationToken);
    }

    public async Task InstallOrUpdateNovrAsync(GameInstallInfo game, string novrZip, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Installing NOVR...");
        var tempExtract = Path.Combine(Path.GetTempPath(), "novr-installer-" + Guid.NewGuid().ToString("N"));

        try
        {
            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(novrZip, tempExtract);
                var pluginsSource = Path.Combine(tempExtract, InstallerConstants.PluginsFolderName);
                var patchersSource = Path.Combine(tempExtract, InstallerConstants.PatchersFolderName);

                if (!Directory.Exists(pluginsSource) || !Directory.Exists(patchersSource))
                {
                    throw new InvalidOperationException("NOVR ZIP must contain top-level plugins and patchers folders.");
                }

                ReplaceDirectory(game.PluginDir, pluginsSource);
                ReplaceDirectory(game.PatcherDir, patchersSource);
            }, cancellationToken);
        }
        finally
        {
            TryDeleteDirectory(tempExtract);
        }
    }

    public async Task UninstallNovrAsync(GameInstallInfo game, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Removing NOVR files...");
        await Task.Run(() =>
        {
            TryDeleteDirectory(game.PluginDir);
            TryDeleteDirectory(game.PatcherDir);
        }, cancellationToken);
    }

    public async Task UninstallBepInExAsync(GameInstallInfo game, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Removing BepInEx...");
        await Task.Run(() =>
        {
            TryDeleteDirectory(game.BepInExDir);
            TryDeleteFile(Path.Combine(game.GameDir, "winhttp.dll"));
            TryDeleteFile(Path.Combine(game.GameDir, "doorstop_config.ini"));
        }, cancellationToken);
    }

    private static void ReplaceDirectory(string destination, string source)
    {
        TryDeleteDirectory(destination);
        Directory.CreateDirectory(destination);
        CopyDirectory(source, destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destinationFile = file.Replace(source, destination, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }

    private static void TryDeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
