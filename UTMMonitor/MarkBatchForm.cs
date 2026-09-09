using System.Text;
using System.Text.RegularExpressions;

namespace UTMMonitor;

public sealed class MarkBatchForm : Form
{
    readonly UtmDatabase db = new();
    readonly UtmClient api = new();
    readonly ComboBox utm = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    readonly TextBox input = new() { Multiline = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new("Consolas", 10F) };
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, RowHeadersVisible = false };
    readonly Label state = new() { AutoSize = true, Text = "Готово" };
    readonly ProgressBar progress = new() { Width = 260, Height = 24 };
    readonly CancellationTokenSource stop = new();
    readonly List<BatchMark> marks = [];

    sealed record BatchMark(string Raw, MarkParts Parts, string Status = "Не проверялась", string Response = "");

    public MarkBatchForm()
    {
        Text = "Массовая проверка акцизных марок — UTM Monitor";
        Width = 1250; Height = 800; MinimumSize = new(950, 650); StartPosition = FormStartPosition.CenterParent; Font = new("Segoe UI", 9.5F);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new(SizeType.Absolute, 50)); root.RowStyles.Add(new(SizeType.Percent, 42)); root.RowStyles.Add(new(SizeType.Percent, 58));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        top.Controls.Add(new Label { Text = "УТМ:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
        top.Controls.Add(utm);
        top.Controls.Add(Button("Загрузить TXT/CSV/MXL", LoadFile)); top.Controls.Add(Button("Разобрать список", ParseInput)); top.Controls.Add(Button("Очистить", ClearAll));
        root.Controls.Add(top, 0, 0);
        var inputBox = new GroupBox { Text = "Исходные данные — DataMatrix, список строк, TXT/CSV/MXL/XML", Dock = DockStyle.Fill, Padding = new Padding(8) }; inputBox.Controls.Add(input); root.Controls.Add(inputBox, 0, 1);
        var bottom = new Panel { Dock = DockStyle.Fill }; var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        actions.Controls.Add(Button("Проверить выбранные", async () => await CheckSelected())); actions.Controls.Add(Button("Проверить все", async () => await CheckAll())); actions.Controls.Add(Button("Остановить", () => stop.Cancel())); actions.Controls.Add(Button("Экспорт результата", Export)); actions.Controls.Add(progress); actions.Controls.Add(state);
        bottom.Controls.Add(grid); bottom.Controls.Add(actions); root.Controls.Add(bottom, 0, 2); Controls.Add(root);
        Load += (_, _) => LoadUtms(); FormClosed += (_, _) => { stop.Cancel(); api.Dispose(); };
    }
    void LoadUtms() { var list = db.All(); utm.DataSource = list; utm.DisplayMember = "Name"; utm.ValueMember = "Id"; }
    Utm? SelectedUtm() => utm.SelectedItem as Utm;
    void LoadFile()
    {
        using var d = new OpenFileDialog { Filter = "Файлы TXT/CSV/MXL/XML|*.txt;*.csv;*.mxl;*.xml|Все файлы|*.*" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        try { input.Text = File.ReadAllText(d.FileName); state.Text = $"Загружен: {Path.GetFileName(d.FileName)}"; ParseInput(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Ошибка чтения", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void ParseInput()
    {
        marks.Clear(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in ExtractCandidates(input.Text)) { var p = Parse(raw); if (p != null && seen.Add($"{p.Type}:{p.Rank}:{p.Number}")) marks.Add(new(raw, p)); }
        Render(); state.Text = $"Распознано уникальных марок: {marks.Count}";
    }
    static IEnumerable<string> ExtractCandidates(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (var line in text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)) { var value = line.Trim().Trim('"', '\''); if (Parse(value) != null) yield return value; }
        foreach (Match m in Regex.Matches(text, @"(?<!\d)\d{14}(?!\d)")) yield return m.Value;
        foreach (Match m in Regex.Matches(text, @"(?<!\d)\d{3}[^\d\r\n]{0,3}\d{3}[^\d\r\n]{0,3}\d{8}(?!\d)")) yield return m.Value;
    }
    static MarkParts? Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null; var s = raw.Replace("\u001d", "").Trim();
        var m = Regex.Match(s, @"(?<!\d)(?<t>\d{3})[^\d]{0,3}(?<r>\d{3})[^\d]{0,3}(?<n>\d{8})(?!\d)");
        if (m.Success) return new(m.Groups["t"].Value, m.Groups["r"].Value, m.Groups["n"].Value, raw);
        var digits = new string(s.Where(char.IsDigit).ToArray()); if (digits.Length < 14) return null;
        for (var i = 0; i <= digits.Length - 14; i++) { var x = digits.Substring(i, 14); return new(x[..3], x.Substring(3, 3), x.Substring(6, 8), raw); }
        return null;
    }
    void Render() => grid.DataSource = marks.Select((x, i) => new { Номер = i + 1, Марка = x.Raw, Type = x.Parts.Type, Rank = x.Parts.Rank, Code = x.Parts.Number, Состояние = x.Status, Ответ = Short(x.Response, 120) }).ToList();
    async Task CheckSelected() { var selected = grid.SelectedRows.Cast<DataGridViewRow>().Select(x => x.Index).Where(i => i >= 0 && i < marks.Count).Distinct().ToList(); await CheckIndexes(selected.Count == 0 ? Enumerable.Range(0, marks.Count) : selected); }
    async Task CheckAll() => await CheckIndexes(Enumerable.Range(0, marks.Count));
    async Task CheckIndexes(IEnumerable<int> indexes)
    {
        var u = SelectedUtm(); if (u == null) { MessageBox.Show(this, "Выберите УТМ.", "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var list = indexes.Distinct().Where(i => i >= 0 && i < marks.Count).ToList(); if (list.Count == 0) { MessageBox.Show(this, "Сначала загрузите марки.", "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        progress.Minimum = 0; progress.Maximum = list.Count; progress.Value = 0;
        for (var n = 0; n < list.Count; n++)
        {
            if (stop.IsCancellationRequested) break; var i = list[n]; var m = marks[i]; state.Text = $"Проверка {n + 1}/{list.Count}: {m.Parts.Number}";
            try
            {
                var q = await api.QueryBarcodeAsync(u, m.Parts, stop.Token);
                if (!q.Success) { marks[i] = m with { Status = "Ошибка отправки", Response = q.Raw }; db.SaveMarkResult(u.Id, null, m.Raw, m.Parts, "ERROR", q.Raw); }
                else if (q.Ticket == null) { marks[i] = m with { Status = "Запрос отправлен", Response = q.Raw }; db.SaveMarkResult(u.Id, null, m.Raw, m.Parts, "SENT", q.Raw); }
                else { var t = await api.PollTicketAsync(u, q.Ticket, stop.Token); var s = t.Success ? "Ответ получен" : "Таймаут"; marks[i] = m with { Status = s, Response = t.Raw }; db.SaveMarkResult(u.Id, null, m.Raw, m.Parts, t.Success ? "OK" : "TIMEOUT", t.Raw); db.Event(u.Id, t.Success ? "INFO" : "ERROR", $"Массовая проверка марки {m.Parts.Number}: {s}", t.Raw); }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { marks[i] = m with { Status = "Ошибка", Response = ex.Message }; db.SaveMarkResult(u.Id, null, m.Raw, m.Parts, "ERROR", ex.ToString()); }
            progress.Value = Math.Min(progress.Maximum, n + 1); Render();
        }
        state.Text = stop.IsCancellationRequested ? "Проверка остановлена" : "Проверка завершена";
    }
    void Export()
    {
        using var d = new SaveFileDialog { FileName = "marks-batch.csv", Filter = "CSV UTF-8|*.csv" }; if (d.ShowDialog(this) != DialogResult.OK) return;
        var sb = new StringBuilder(); sb.AppendLine("Raw;Type;Rank;Number;Status;Response"); foreach (var x in marks) sb.AppendLine(string.Join(';', new[] { x.Raw, x.Parts.Type, x.Parts.Rank, x.Parts.Number, x.Status, x.Response }.Select(Csv))); File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true)); state.Text = "Результат экспортирован";
    }
    void ClearAll() { marks.Clear(); input.Clear(); Render(); state.Text = "Готово"; }
    Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true, Height = 34, Padding = new Padding(10, 0, 10, 0) }; b.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error); } }; return b; }
    static string Short(string? s, int n) { s ??= ""; return s.Length <= n ? s : s[..n] + "…"; }
    static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
}
