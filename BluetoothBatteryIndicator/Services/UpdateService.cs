using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace BluetoothBatteryIndicator.Services;

public class UpdateService
{
    private const string RepoApiUrl = "https://api.github.com/repos/zonnedev/bluetooth-battery-indicator/releases/latest";
    private const string InstallerName = "BluetoothBatteryIndicator_Setup.exe";

    private readonly UpdateWindow _window;

    public UpdateService(UpdateWindow window)
    {
        _window = window;
    }

    /// <summary>
    /// Returns true if an update was triggered (app should exit).
    /// </summary>
    public async Task<bool> CheckAndUpdate()
    {
        try
        {
            // Skip if running as MSIX (Store handles updates)
            if (IsPackagedApp())
            {
                Log.Info("Running as MSIX package, skipping self-update (Store handles it)");
                return false;
            }

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(3);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("BluetoothBatteryIndicator");

            Log.Info($"Checking for updates at {RepoApiUrl}");
            var json = await http.GetStringAsync(RepoApiUrl);
            var release = JsonDocument.Parse(json).RootElement;

            var tagName = release.GetProperty("tag_name").GetString() ?? "";
            var remoteVersion = ParseVersion(tagName.TrimStart('v'));
            var currentVersion = typeof(App).Assembly.GetName().Version;

            Log.Info($"Current version: {currentVersion}, Remote version: {remoteVersion} ({tagName})");

            if (currentVersion == null || remoteVersion == null || remoteVersion <= currentVersion)
            {
                Log.Info("Already up to date");
                return false;
            }

            // Find installer asset
            var downloadUrl = FindAssetUrl(release);
            if (downloadUrl == null)
            {
                Log.Info("No installer asset found in release");
                return false;
            }

            Log.Info($"Update available! Downloading from {downloadUrl}");

            // Show update window and download
            _window.Show();
            _window.SetStatus($"Updating to {tagName}...");
            _window.SetProgress(0);

            var tempPath = Path.Combine(Path.GetTempPath(), InstallerName);
            await DownloadWithProgress(http, downloadUrl, tempPath);

            // Install
            Log.Info($"Download complete, launching installer: {tempPath}");
            _window.SetStatus("Installing...");
            _window.SetProgress(100);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES",
                UseShellExecute = true
            });

            Application.Current.Shutdown();
            return true;
        }
        catch (Exception ex)
        {
            Log.Info($"Update check failed: {ex.Message}");
            return false;
        }
    }

    private async Task DownloadWithProgress(HttpClient http, string url, string destPath)
    {
        http.Timeout = TimeSpan.FromSeconds(60);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        using var stream = await response.Content.ReadAsStreamAsync();
        using var fileStream = File.Create(destPath);

        var buffer = new byte[8192];
        long downloaded = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            downloaded += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (double)downloaded / totalBytes * 100;
                _window.SetProgress(percent);
                _window.SetStatus($"Downloading update... ({downloaded / 1024:0} KB / {totalBytes / 1024:0} KB)");
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
        if (dashIndex > 0) version = version[..dashIndex];
        return Version.TryParse(version, out var v) ? v : null;
    }

    private static bool IsPackagedApp()
    {
        try { return Windows.ApplicationModel.Package.Current != null; }
        catch { return false; }
    }
}
