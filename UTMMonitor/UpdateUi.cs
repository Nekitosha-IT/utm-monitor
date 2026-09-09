using System.Diagnostics;

namespace UTMMonitor;

public static class UpdateUi
{
    public static void Install(Form form)
    {
        var service = new UpdateService();
        var button = new Button
        {
            Text = "Обновить программу",
            AutoSize = true,
            Height = 32,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Padding = new Padding(10, 0, 10, 0),
            BackColor = System.Drawing.SystemColors.Control
        };
        form.Controls.Add(button);
        button.BringToFront();

        void Position()
        {
            button.Location = new Point(form.ClientSize.Width - button.Width - 18, form.ClientSize.Height - button.Height - 18);
            button.BringToFront();
        }
        form.Resize += (_, _) => Position();
        form.Shown += (_, _) =>
        {
            Position();
            _ = CheckSilentlyAsync();
        };
        button.Click += async (_, _) => await CheckAndInstallAsync(true);
        form.FormClosed += (_, _) => service.Dispose();

        async Task CheckSilentlyAsync()
        {
            try
            {
                var info = await service.CheckAsync();
                if (!info.Available || form.IsDisposed) return;
                if (MessageBox.Show(form,
                    $"Доступна новая версия UTM Monitor: {info.LatestVersion}.\r\n\r\nТекущая версия: {info.CurrentVersion}\r\n\r\nУстановить обновление сейчас?",
                    "Обновление UTM Monitor", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    await service.DownloadAndRestartAsync(info, form);
            }
            catch { }
        }

        async Task CheckAndInstallAsync(bool interactive)
        {
            button.Enabled = false;
            var old = button.Text;
            button.Text = "Проверяю…";
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
                    button.Enabled = true;
                    button.Text = old;
                    Position();
                }
            }
        }
    }
}
