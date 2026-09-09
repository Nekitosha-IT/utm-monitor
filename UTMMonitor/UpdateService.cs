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
                    if (!IsApplicationAsset(name)) continue;
                    url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(url)) break;
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

    static bool IsApplicationAsset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        var normalized = Path.GetFileNameWithoutExtension(name).Replace(".", "").Replace("-", "").Replace("_", "").Replace(" ", "");
        return normalized.Equals("UTMMonitor", StringComparison.OrdinalIgnoreCase);
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
            if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe)) return false;

            var script = Path.Combine(Path.GetTempPath(), $"utm-monitor-update-{Guid.NewGuid():N}.ps1");
            var pid = Environment.ProcessId;
            var escapedTemp = tempExe.Replace("'", "''");
            var escapedCurrent = currentExe.Replace("'", "''");
            var content = $"$ErrorActionPreference = 'Stop'\r\n$pidToWait = {pid}\r\n$temp = '{escapedTemp}'\r\n$target = '{escapedCurrent}'\r\nwhile (Get-Process -Id $pidToWait -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 500 }}\r\nCopy-Item -LiteralPath $temp -Destination $target -Force\r\nStart-Process -FilePath $target\r\nRemove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue\r\nRemove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue\r\n";
            await File.WriteAllTextAsync(script, content, ct);

            var psi = new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
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
