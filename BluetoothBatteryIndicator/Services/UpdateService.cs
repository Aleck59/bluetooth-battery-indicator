using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace BluetoothBatteryIndicator.Services;

public sealed class UpdateInfo
{
    public UpdateInfo(string tag, string downloadUrl)
    {
        Tag = tag;
        DownloadUrl = downloadUrl;
    }

    public string Tag { get; }
    public string DownloadUrl { get; }
}

/// <summary>Progress UI for an update download; implemented by the WPF update window.</summary>
public interface IUpdateProgress
{
    void Show();
    void Hide();
    void SetStatus(string status);
    void SetProgress(double percent);
}

public class UpdateService
{
    private const string RepoApiUrl = "https://api.github.com/repos/zonnedev/bluetooth-battery-indicator/releases/latest";
    private const string InstallerName = "BluetoothBatteryIndicator_Setup.exe";

    /// <summary>Returns the newer release, or null when up to date or the check fails.</summary>
    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            // Skip if running as MSIX (Store handles updates)
            if (IsPackagedApp())
            {
                Log.Info("Running as MSIX package, skipping self-update (Store handles it)");
                return null;
            }

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("BluetoothBatteryIndicator");

            Log.Info($"Checking for updates at {RepoApiUrl}");
            var json = await http.GetStringAsync(RepoApiUrl);
            var release = JsonDocument.Parse(json).RootElement;

            var tagName = release.GetProperty("tag_name").GetString() ?? "";
            var remoteVersion = ParseVersion(tagName.TrimStart('v'));
            var currentVersion = typeof(UpdateService).Assembly.GetName().Version;

            Log.Info($"Current version: {currentVersion}, Remote version: {remoteVersion} ({tagName})");

            if (currentVersion == null || remoteVersion == null || remoteVersion <= currentVersion)
            {
                Log.Info("Already up to date");
                return null;
            }

            var downloadUrl = FindAssetUrl(release);
            if (downloadUrl == null)
            {
                Log.Info("No installer asset found in release");
                return null;
            }

            return new UpdateInfo(tagName, downloadUrl);
        }
        catch (Exception ex)
        {
            Log.Info($"Update check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads the installer and starts it silently. Returns true if the installer was launched
    /// (the app should exit so it can be replaced).
    /// </summary>
    public async Task<bool> DownloadAndRunAsync(UpdateInfo update, IUpdateProgress progress)
    {
        try
        {
            Log.Info($"Update available! Downloading from {update.DownloadUrl}");
            progress.Show();
            progress.SetStatus($"Updating to {update.Tag}...");
            progress.SetProgress(0);

            var tempPath = Path.Combine(Path.GetTempPath(), InstallerName);

            using var downloadHttp = new HttpClient();
            downloadHttp.Timeout = TimeSpan.FromSeconds(60);
            downloadHttp.DefaultRequestHeaders.UserAgent.ParseAdd("BluetoothBatteryIndicator");
            await DownloadWithProgress(downloadHttp, update.DownloadUrl, tempPath, progress);

            Log.Info($"Download complete, launching installer: {tempPath}");
            progress.SetStatus("Installing...");
            progress.SetProgress(100);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Log.Info($"Update failed: {ex.Message}");
            progress.Hide();
            return false;
        }
    }

    private static async Task DownloadWithProgress(HttpClient http, string url, string destPath, IUpdateProgress progress)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        using var stream = await response.Content.ReadAsStreamAsync();
        using var fileStream = File.Create(destPath);

        var buffer = new byte[8192];
        long downloaded = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            downloaded += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (double)downloaded / totalBytes * 100;
                progress.SetProgress(percent);
                progress.SetStatus($"Downloading update... ({downloaded / 1024:0} KB / {totalBytes / 1024:0} KB)");
            }
        }
    }

    private static string? FindAssetUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets)) return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name == InstallerName)
                return asset.GetProperty("browser_download_url").GetString();
        }
        return null;
    }

    private static Version? ParseVersion(string version)
    {
        // Handle "0.1.0" or "0.1.0-alpha.1"
        var dashIndex = version.IndexOf('-');
        if (dashIndex > 0) version = version.Substring(0, dashIndex);
        return Version.TryParse(version, out var v) ? v : null;
    }

    private static bool IsPackagedApp()
    {
        try { return Windows.ApplicationModel.Package.Current != null; }
        catch { return false; }
    }
}
