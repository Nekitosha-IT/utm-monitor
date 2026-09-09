using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace UTMMonitor;

public sealed record UpdateInfo(bool Available, string CurrentVersion, string LatestVersion, string? DownloadUrl, string Message);

public sealed class UpdateService : IDisposable
{
    const string LatestReleaseApi = "https://api.github.com/repos/Nekitosha-IT/utm-monitor/releases/latest";
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public UpdateService()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("UTM-Monitor-Updater/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public string CurrentVersion => GetCurrentVersion();

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync(LatestReleaseApi, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return new(false, CurrentVersion, CurrentVersion, null, $"GitHub вернул HTTP {(int)response.StatusCode}: {Short(body, 180)}");

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
            var latest = Normalize(tag);
            string? url = null;
            string? assetName = null;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!IsApplicationAsset(name)) continue;
                    var candidate = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (string.IsNullOrWhiteSpace(candidate)) continue;
                    url = candidate;
                    assetName = name;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(latest))
                return new(false, CurrentVersion, CurrentVersion, null, "У релиза GitHub отсутствует корректный tag_name.");
            if (string.IsNullOrWhiteSpace(url))
                return new(false, CurrentVersion, latest, null, "В последнем релизе не найден EXE UTM Monitor.");

            var available = Compare(latest, CurrentVersion) > 0;
            return new(available, CurrentVersion, latest, url,
                available ? $"Доступна новая версия {latest} ({assetName})." : $"Установлена актуальная версия {CurrentVersion}.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new(false, CurrentVersion, CurrentVersion, null, $"Не удалось проверить обновления: {ex.Message}");
        }
    }

    public async Task<bool> DownloadAndRestartAsync(UpdateInfo info, Form owner, CancellationToken ct = default)
    {
        if (!info.Available || string.IsNullOrWhiteSpace(info.DownloadUrl)) return false;
        string? tempExe = null;
        string? script = null;
        try
        {
            tempExe = Path.Combine(Path.GetTempPath(), $"UTM-Monitor-update-{Guid.NewGuid():N}.exe");
            using (var response = await http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var output = new FileStream(tempExe, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output, ct);
            }

            var downloaded = new FileInfo(tempExe);
            if (!downloaded.Exists || downloaded.Length < 1024 * 1024)
                throw new InvalidOperationException($"Скачанный EXE имеет подозрительный размер: {downloaded.Length:N0} байт.");

            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
                throw new InvalidOperationException("Не удалось определить путь текущего EXE.");

            script = Path.Combine(Path.GetTempPath(), $"UTM-Monitor-updater-{Guid.NewGuid():N}.ps1");
            var pid = Environment.ProcessId;
            var escapedTemp = tempExe.Replace("'", "''");
            var escapedCurrent = currentExe.Replace("'", "''");
            var escapedScript = script.Replace("'", "''");
            var content = "$ErrorActionPreference = 'Stop'\r\n" +
                          $"$pidToWait = {pid}\r\n" +
                          $"$temp = '{escapedTemp}'\r\n" +
                          $"$target = '{escapedCurrent}'\r\n" +
                          $"$self = '{escapedScript}'\r\n" +
                          "for ($i = 0; $i -lt 120; $i++) { if (-not (Get-Process -Id $pidToWait -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 250 }\r\n" +
                          "if (Get-Process -Id $pidToWait -ErrorAction SilentlyContinue) { throw 'UTM Monitor не завершился вовремя.' }\r\n" +
                          "for ($i = 0; $i -lt 20; $i++) { try { Copy-Item -LiteralPath $temp -Destination $target -Force -ErrorAction Stop; break } catch { if ($i -eq 19) { throw }; Start-Sleep -Milliseconds 500 } }\r\n" +
                          "Start-Process -FilePath $target\r\n" +
                          "Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue\r\n" +
                          "Remove-Item -LiteralPath $self -Force -ErrorAction SilentlyContinue\r\n";
            await File.WriteAllTextAsync(script, content, ct);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            var targetDir = Path.GetDirectoryName(currentExe) ?? "";
            if (IsProtectedDirectory(targetDir)) psi.Verb = "runas";

            if (Process.Start(psi) is null)
                throw new InvalidOperationException("Windows не смог запустить установщик обновления.");

            owner.BeginInvoke(owner.Close);
            return true;
        }
        catch (Exception ex)
        {
            try { if (tempExe is not null) File.Delete(tempExe); } catch { }
            try { if (script is not null) File.Delete(script); } catch { }
            MessageBox.Show(owner, $"Не удалось установить обновление.\r\n\r\n{ex.Message}", "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    static bool IsApplicationAsset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        var normalized = Path.GetFileNameWithoutExtension(name).Replace(".", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        return normalized.Equals("UTMMonitor", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsProtectedDirectory(string directory)
    {
        var normalized = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalized.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase);
    }

    static string GetCurrentVersion()
    {
        try
        {
            var entry = Assembly.GetEntryAssembly();
            var version = entry?.GetName().Version;
            if (version is not null && version.Major != 0)
            {
                var v = Normalize(version.ToString(3));
                if (v != "1.0.0") return v;
            }
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                var fileVersion = FileVersionInfo.GetVersionInfo(path).FileVersion;
                if (!string.IsNullOrWhiteSpace(fileVersion)) return Normalize(fileVersion);
            }
        }
        catch { }
        return "1.0.0";
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

    static string Short(string? value, int length)
    {
        value ??= "";
        value = value.Replace("\r", " ").Replace("\n", " ");
        return value.Length <= length ? value : value[..length] + "…";
    }

    public void Dispose() => http.Dispose();
}
