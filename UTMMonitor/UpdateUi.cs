using System.Diagnostics;

namespace UTMMonitor;

public enum UpdateMode
{
    Ask,
    Auto,
    Never
}

public static class UpdateUi
{
    static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UTMMonitor", "update.settings");

    public static void Install(Form form)
    {
        var service = new UpdateService();
        var updateButton = new Button
        {
            Text = "Обновить программу",
            AutoSize = true,
            Height = 32,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Padding = new Padding(10, 0, 10, 0)
        };
        var settingsButton = new Button
        {
            Text = "Настройки обновлений",
            AutoSize = true,
            Height = 32,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Padding = new Padding(10, 0, 10, 0)
        };

        form.Controls.Add(updateButton);
        form.Controls.Add(settingsButton);
        updateButton.BringToFront();
        settingsButton.BringToFront();

        void Position()
        {
            settingsButton.Location = new Point(form.ClientSize.Width - updateButton.Width - settingsButton.Width - 26, form.ClientSize.Height - updateButton.Height - 18);
            updateButton.Location = new Point(form.ClientSize.Width - updateButton.Width - 18, form.ClientSize.Height - updateButton.Height - 18);
            settingsButton.BringToFront();
            updateButton.BringToFront();
        }

        form.Resize += (_, _) => Position();
        form.Shown += (_, _) =>
        {
            Position();
            _ = CheckOnStartupAsync();
        };
        updateButton.Click += async (_, _) => await CheckAndInstallAsync(true);
        settingsButton.Click += (_, _) => ShowSettings(form);
        form.FormClosed += (_, _) => service.Dispose();

        async Task CheckOnStartupAsync()
        {
            var mode = LoadMode();
            if (mode == UpdateMode.Never) return;
            try
            {
                await Task.Delay(1200);
                if (form.IsDisposed) return;
                var info = await service.CheckAsync();
                if (form.IsDisposed) return;

                if (!info.Available)
                {
                    if (info.Message.StartsWith("Не удалось", StringComparison.OrdinalIgnoreCase) || info.Message.StartsWith("GitHub вернул", StringComparison.OrdinalIgnoreCase))
                        Debug.WriteLine("UTM Monitor updater: " + info.Message);
                    return;
                }

                if (mode == UpdateMode.Auto)
                {
                    await service.DownloadAndRestartAsync(info, form);
                    return;
                }

                if (MessageBox.Show(form,
                    $"Доступна новая версия UTM Monitor: {info.LatestVersion}.\r\n\r\nТекущая версия: {info.CurrentVersion}\r\n\r\nУстановить обновление сейчас?",
                    "Обновление UTM Monitor", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    await service.DownloadAndRestartAsync(info, form);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UTM Monitor updater startup error: " + ex);
            }
        }

        async Task CheckAndInstallAsync(bool interactive)
        {
            updateButton.Enabled = false;
            var old = updateButton.Text;
            updateButton.Text = "Проверяю…";
            try
            {
                var info = await service.CheckAsync();
                if (!info.Available)
                {
                    if (interactive) MessageBox.Show(form, info.Message, "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (MessageBox.Show(form,
                    $"Доступна новая версия {info.LatestVersion}.\r\nТекущая: {info.CurrentVersion}\r\n\r\nСкачать и установить?",
                    "Обновление UTM Monitor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    await service.DownloadAndRestartAsync(info, form);
            }
            finally
            {
                if (!form.IsDisposed)
                {
                    updateButton.Enabled = true;
                    updateButton.Text = old;
                    Position();
                }
            }
        }
    }

    static UpdateMode LoadMode()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return UpdateMode.Auto;
            var value = File.ReadAllText(SettingsPath).Trim();
            return Enum.TryParse<UpdateMode>(value, true, out var mode) ? mode : UpdateMode.Auto;
        }
        catch { return UpdateMode.Auto; }
    }

    static void SaveMode(UpdateMode mode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, mode.ToString());
    }

    static void ShowSettings(Form owner)
    {
        using var f = new UpdateSettingsForm(LoadMode());
        if (f.ShowDialog(owner) == DialogResult.OK) SaveMode(f.Mode);
    }
}

sealed class UpdateSettingsForm : Form
{
    readonly RadioButton ask = new() { Text = "Спрашивать перед установкой", AutoSize = true };
    readonly RadioButton auto = new() { Text = "Устанавливать автоматически", AutoSize = true };
    readonly RadioButton never = new() { Text = "Никогда не проверять обновления", AutoSize = true };
    public UpdateMode Mode => auto.Checked ? UpdateMode.Auto : never.Checked ? UpdateMode.Never : UpdateMode.Ask;

    public UpdateSettingsForm(UpdateMode mode)
    {
        Text = "Настройки обновлений";
        Width = 470;
        Height = 250;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new("Segoe UI", 9.5F);

        ask.Checked = mode == UpdateMode.Ask;
        auto.Checked = mode == UpdateMode.Auto;
        never.Checked = mode == UpdateMode.Never;

        var title = new Label
        {
            Text = "Как UTM Monitor должен устанавливать новые версии?",
            AutoSize = true,
            Font = new("Segoe UI Semibold", 10.5F),
            Location = new Point(20, 20)
        };
        ask.Location = new Point(24, 60);
        auto.Location = new Point(24, 92);
        never.Location = new Point(24, 124);

        var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Width = 110, Height = 34, Location = new Point(225, 165) };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 110, Height = 34, Location = new Point(340, 165) };
        Controls.AddRange(new Control[] { title, ask, auto, never, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
