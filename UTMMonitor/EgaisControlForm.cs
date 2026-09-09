using System.Text;

namespace UTMMonitor;

public sealed class EgaisControlForm : Form
{
    readonly UtmDatabase db = new();
    readonly UtmClient api = new();
    readonly ComboBox utm = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White, RowHeadersVisible = false };
    readonly TextBox report = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new("Consolas", 10F) };
    readonly Label state = new() { AutoSize = true, Text = "Готово" };
    CancellationTokenSource? cts;

    public EgaisControlForm()
    {
        Text = "ЕГАИС — Центр контроля УТМ";
        Width = 1350; Height = 850; MinimumSize = new(1050, 700); StartPosition = FormStartPosition.CenterParent; Font = new("Segoe UI", 9.5F);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new(SizeType.Absolute, 52)); root.RowStyles.Add(new(SizeType.Percent, 62)); root.RowStyles.Add(new(SizeType.Percent, 38));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        top.Controls.Add(new Label { Text = "УТМ:", AutoSize = true, Padding = new Padding(0, 8, 5, 0) }); top.Controls.Add(utm);
        top.Controls.Add(Button("Провести контроль", async () => await RunControl()));
        top.Controls.Add(Button("Синхронизировать и проверить", async () => await SyncAndControl()));
        top.Controls.Add(Button("Экспорт отчёта", Export)); top.Controls.Add(state);
        root.Controls.Add(top, 0, 0);
        root.Controls.Add(grid, 0, 1);
        root.Controls.Add(report, 0, 2);
        Controls.Add(root);
        Load += (_, _) => LoadUtms();
        FormClosed += (_, _) => { cts?.Cancel(); api.Dispose(); };
    }

    void LoadUtms()
    {
        var list = db.All(); utm.DataSource = list; utm.DisplayMember = "Name"; utm.ValueMember = "Id";
    }
    Utm? Selected() => utm.SelectedItem as Utm;

    async Task SyncAndControl()
    {
        var u = Selected(); if (u == null) { MessageBox.Show(this, "Выберите УТМ."); return; }
        state.Text = "Синхронизация…";
        try { cts?.Cancel(); cts = new(); await api.SyncAsync(u, cts.Token); await RunControl(); }
        catch (OperationCanceledException) { state.Text = "Остановлено"; }
        catch (Exception ex) { state.Text = "Ошибка"; report.Text = ex.ToString(); }
    }

    async Task RunControl()
    {
        var u = Selected(); if (u == null) { MessageBox.Show(this, "Выберите УТМ."); return; }
        cts?.Cancel(); cts = new(); state.Text = "Проверяю ЕГАИС…";
        try
        {
            var status = await api.StatusAsync(u, cts.Token);
            var certs = await api.CertificatesAsync(u, cts.Token);
            var docs = db.Documents(u.Id);
            var marks = db.Marks(u.Id);
            var notices = new List<ControlRow>();
            Add(notices, status.Online, status.Online ? "OK" : "ERROR", "Связь с УТМ", status.Online ? $"HTTP {status.HttpCode}, {status.ResponseMs} мс" : status.Error ?? "Нет ответа");
            Add(notices, !string.IsNullOrWhiteSpace(status.Version), "INFO", "Версия УТМ", status.Version ?? "Не определена");
            Add(notices, !string.IsNullOrWhiteSpace(status.OwnerId), "INFO", "FSRAR_ID", status.OwnerId ?? "Не определён");
            Add(notices, docs.Count > 0, docs.Count == 0 ? "WARNING" : "OK", "Документы в локальной базе", docs.Count.ToString());
            var withoutNumber = docs.Count(x => string.IsNullOrWhiteSpace(x.Number));
            Add(notices, withoutNumber == 0, withoutNumber == 0 ? "OK" : "WARNING", "Документы без номера", withoutNumber.ToString());
            var withoutDate = docs.Count(x => string.IsNullOrWhiteSpace(x.Date));
            Add(notices, withoutDate == 0, withoutDate == 0 ? "OK" : "WARNING", "Документы без даты", withoutDate.ToString());
            var unknownMarks = marks.Count(x => string.IsNullOrWhiteSpace(x.Type) || string.IsNullOrWhiteSpace(x.Rank) || string.IsNullOrWhiteSpace(x.Number));
            Add(notices, unknownMarks == 0, unknownMarks == 0 ? "OK" : "WARNING", "Марки без разобранного Type/Rank/Number", unknownMarks.ToString());
            var checkedErrors = marks.Count(x => x.Status.Equals("ERROR", StringComparison.OrdinalIgnoreCase) || x.Status.Equals("TIMEOUT", StringComparison.OrdinalIgnoreCase));
            Add(notices, checkedErrors == 0, checkedErrors == 0 ? "OK" : "WARNING", "Ошибки проверки марок", checkedErrors.ToString());
            var duplicateNumbers = docs.Where(x => !string.IsNullOrWhiteSpace(x.Number)).GroupBy(x => x.Number.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
            Add(notices, duplicateNumbers.Count == 0, duplicateNumbers.Count == 0 ? "OK" : "WARNING", "Дубликаты номеров документов", duplicateNumbers.Count == 0 ? "Нет" : string.Join(", ", duplicateNumbers.Take(20).Select(x => x.Key)));
            var expired = certs.Count(x => !x.Valid);
            Add(notices, expired == 0, expired == 0 ? "OK" : "ERROR", "Просроченные сертификаты", expired.ToString());
            var soon = certs.Count(x => DateTime.TryParse(x.NotAfter, out var d) && d >= DateTime.Now && (d - DateTime.Now).TotalDays <= 30);
            Add(notices, soon == 0, soon == 0 ? "OK" : "WARNING", "Сертификаты истекают ≤30 дней", soon.ToString());

            grid.DataSource = notices.Select(x => new { Состояние = x.Level, Проверка = x.Title, Результат = x.Value }).ToList();
            var errors = notices.Count(x => x.Level == "ERROR"); var warnings = notices.Count(x => x.Level == "WARNING");
            var sb = new StringBuilder(); sb.AppendLine("ОТЧЁТ КОНТРОЛЯ ЕГАИС"); sb.AppendLine(new string('=', 70)); sb.AppendLine($"УТМ: {u.Name} ({u.Host}:{u.Port})"); sb.AppendLine($"Время: {DateTime.Now:dd.MM.yyyy HH:mm:ss}"); sb.AppendLine($"Версия: {status.Version ?? "—"}"); sb.AppendLine($"FSRAR_ID: {status.OwnerId ?? "—"}"); sb.AppendLine(); sb.AppendLine($"Документы: {docs.Count} | Марки: {marks.Count} | Сертификаты: {certs.Count}"); sb.AppendLine($"Ошибки: {errors} | Предупреждения: {warnings}"); sb.AppendLine(); foreach (var n in notices) sb.AppendLine($"[{n.Level}] {n.Title}: {n.Value}"); report.Text = sb.ToString(); state.Text = errors > 0 ? $"Контроль завершён: ошибок {errors}" : warnings > 0 ? $"Контроль завершён: предупреждений {warnings}" : "Контроль завершён: всё OK";
        }
        catch (OperationCanceledException) { state.Text = "Остановлено"; }
        catch (Exception ex) { state.Text = "Ошибка"; report.Text = ex.ToString(); }
    }

    static void Add(List<ControlRow> a, bool ok, string level, string title, string value) => a.Add(new(level, title, value));
    sealed record ControlRow(string Level, string Title, string Value);
    Button Button(string text, Func<Task> action) { var b = new Button { Text = text, AutoSize = true, Height = 34, Padding = new Padding(10, 0, 10, 0) }; b.Click += async (_, _) => { try { await action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error); } }; return b; }
    Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true, Height = 34, Padding = new Padding(10, 0, 10, 0) }; b.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error); } }; return b; }
    void Export()
    {
        using var d = new SaveFileDialog { FileName = "egais-control.txt", Filter = "TXT UTF-8|*.txt" }; if (d.ShowDialog(this) != DialogResult.OK) return; File.WriteAllText(d.FileName, report.Text, new UTF8Encoding(true)); state.Text = "Отчёт экспортирован";
    }
}