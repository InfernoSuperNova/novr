using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NOVR.Installer.Services;

public sealed class GitHubReleaseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public GitHubReleaseClient()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(InstallerConstants.UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<string> DownloadLatestNovrReleaseAsync(string tempDir, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Checking latest NOVR release...");
        var release = await GetLatestReleaseAsync(InstallerConstants.GitHubOwner, InstallerConstants.GitHubRepo, cancellationToken);
        var asset = release.Assets.FirstOrDefault(asset =>
            asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            !asset.Name.Contains("bepinex", StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            throw new InvalidOperationException("Latest NOVR release does not contain a ZIP asset.");
        }

        return await DownloadAssetAsync(asset, tempDir, progress, cancellationToken);
    }

    public async Task<string> DownloadLatestBepInEx5Async(string tempDir, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Checking latest BepInEx 5 release...");
        var release = await GetLatestBepInEx5ReleaseAsync(cancellationToken);
        var asset = release.Assets.FirstOrDefault(asset =>
            asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.Contains("BepInEx", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.Contains("win_x64", StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            throw new InvalidOperationException("Latest BepInEx 5 release does not contain a Windows x64 ZIP asset.");
        }

        return await DownloadAssetAsync(asset, tempDir, progress, cancellationToken);
    }

    private async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        await using var stream = await _httpClient.GetStreamAsync(url, cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken);
        return release ?? throw new InvalidOperationException($"Could not read latest release for {owner}/{repo}.");
    }

    private async Task<GitHubRelease> GetLatestBepInEx5ReleaseAsync(CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{InstallerConstants.BepInExOwner}/{InstallerConstants.BepInExRepo}/releases?per_page=30";
        await using var stream = await _httpClient.GetStreamAsync(url, cancellationToken);
        var releases = await JsonSerializer.DeserializeAsync<GitHubRelease[]>(stream, JsonOptions, cancellationToken);
        var release = releases?.FirstOrDefault(release => release.TagName.StartsWith("v5.", StringComparison.OrdinalIgnoreCase));
        return release ?? throw new InvalidOperationException("Could not find a BepInEx 5 release.");
    }

    private async Task<string> DownloadAssetAsync(GitHubAsset asset, string tempDir, IProgress<string> progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(tempDir);
        var destination = Path.Combine(tempDir, asset.Name);
        progress.Report($"Downloading {asset.Name}...");

        await using var remote = await _httpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken);
        await using var local = File.Create(destination);
        await remote.CopyToAsync(local, cancellationToken);
        return destination;
    }

    private sealed record GitHubRelease(
        [property: System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        string TagName,
        GitHubAsset[] Assets);

    private sealed record GitHubAsset(
        string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        string BrowserDownloadUrl);
}
