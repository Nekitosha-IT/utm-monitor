using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace UTMMonitor;

public sealed record UpdateInfo(bool Available, string CurrentVersion, string LatestVersion, string? DownloadUrl, string Message);

public sealed class UpdateService
{
    const string LatestReleaseApi = "https://api.github.com/repos/Nekitosha-IT/utm-monitor/releases/latest";
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public UpdateService()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("UTM-Monitor-Updater/1.0");
    }

    public string CurrentVersion => Normalize(Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0");

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync(LatestReleaseApi, ct);
            if (!response.IsSuccessStatusCode)
                return new(false, CurrentVersion, CurrentVersion, null, $"Сервер обновлений вернул HTTP {(int)response.StatusCode}.");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
            var latest = Normalize(tag);
            string? url = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!string.Equals(name, "UTM Monitor.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    break;
                }
            }

            var available = Compare(latest, CurrentVersion) > 0 && !string.IsNullOrWhiteSpace(url);
            return new(available, CurrentVersion, latest, url,
                available ? $"Доступна новая версия {latest}." : $"Установлена актуальная версия {CurrentVersion}.");
        }
        catch (Exception ex)
        {
            return new(false, CurrentVersion, CurrentVersion, null, $"Не удалось проверить обновления: {ex.Message}");
        }
    }

    public async Task<bool> DownloadAndRestartAsync(UpdateInfo info, Form owner, CancellationToken ct = default)
    {
        if (!info.Available || string.IsNullOrWhiteSpace(info.DownloadUrl)) return false;
        try
        {
            var tempExe = Path.Combine(Path.GetTempPath(), $"UTM-Monitor-{Guid.NewGuid():N}.exe");
            using (var response = await http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var output = File.Create(tempExe);
                await input.CopyToAsync(output, ct);
            }

            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExe)) return false;

            var script = Path.Combine(Path.GetTempPath(), $"utm-monitor-update-{Guid.NewGuid():N}.cmd");
            var pid = Environment.ProcessId;
            var content = $"@echo off\r\nsetlocal\r\n:wait\r\ntimeout /t 2 /nobreak >nul\r\ntasklist /FI \"PID eq {pid}\" | find \"{pid}\" >nul\r\nif not errorlevel 1 goto wait\r\ncopy /Y \"{tempExe}\" \"{currentExe}\" >nul\r\nstart \"\" \"{currentExe}\"\r\ndel \"{tempExe}\" >nul 2>&1\r\ndel \"%~f0\" >nul 2>&1\r\n";
            await File.WriteAllTextAsync(script, content, ct);
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c start \"\" \"{script}\"") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            owner.BeginInvoke(owner.Close);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"Не удалось установить обновление.\r\n\r\n{ex.Message}", "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    static string Normalize(string value)
    {
        value = value.Trim().TrimStart('v', 'V');
        var dash = value.IndexOf('-');
        if (dash >= 0) value = value[..dash];
        return value;
    }

    static int Compare(string a, string b)
    {
        if (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb)) return va.CompareTo(vb);
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => http.Dispose();
}
