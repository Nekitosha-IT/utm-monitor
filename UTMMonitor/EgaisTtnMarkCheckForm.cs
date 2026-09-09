using System.Text.RegularExpressions;

namespace UTMMonitor;

public sealed class EgaisTtnMarkCheckForm : Form
{
    readonly UtmDatabase db = new();
    readonly UtmClient client = new();
    readonly ComboBox utm = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly ComboBox document = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    readonly Label state = new() { AutoSize = true, Text = "Выберите УТМ и ТТН" };
    readonly ProgressBar progress = new() { Width = 220, Minimum = 0, Maximum = 100 };
    CancellationTokenSource stop = new();
    List<DocumentRow> docs = [];

    public EgaisTtnMarkCheckForm()
    {
        Text = "ЕГАИС — Проверка марок ТТН через УТМ";
        Width = 1450; Height = 850; MinimumSize = new(1050, 650); StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new(SizeType.Absolute, 82)); root.RowStyles.Add(new(SizeType.Percent, 100));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
        top.Controls.Add(new Label { Text = "УТМ:", AutoSize = true, Padding = new Padding(0, 7, 4, 0) });
        top.Controls.Add(utm);
        top.Controls.Add(new Label { Text = "ТТН:", AutoSize = true, Padding = new Padding(14, 7, 4, 0) });
        top.Controls.Add(document);
        top.Controls.Add(Button("Загрузить ТТН", LoadDocuments));
        top.Controls.Add(Button("Извлечь марки", ExtractMarks));
        top.Controls.Add(Button("Проверить все", CheckAllAsync));
        top.Controls.Add(Button("Остановить", Stop));
        top.Controls.Add(progress); top.Controls.Add(state);
        root.Controls.Add(top, 0, 0); root.Controls.Add(grid, 0, 1); Controls.Add(root);
        Load += (_, _) => LoadUtms();
        utm.SelectedIndexChanged += (_, _) => LoadDocuments();
        document.SelectedIndexChanged += (_, _) => ExtractMarks();
        FormClosed += (_, _) => { stop.Cancel(); stop.Dispose(); client.Dispose(); };
    }

    void LoadUtms()
    {
        var list = db.All(); utm.DataSource = list; utm.DisplayMember = "Name"; utm.ValueMember = "Id";
        if (list.Count > 0) utm.SelectedIndex = 0;
    }

    void LoadDocuments()
    {
        var u = utm.SelectedItem as Utm; if (u == null) return;
        docs = db.Documents(u.Id);
        document.DataSource = docs;
        document.DisplayMember = nameof(DocumentRow.Number);
        document.ValueMember = nameof(DocumentRow.Id);
        if (docs.Count > 0) document.SelectedIndex = 0;
        state.Text = $"ТТН: {docs.Count}";
    }

    DocumentRow? SelectedDocument() => document.SelectedItem as DocumentRow;
    Utm? SelectedUtm() => utm.SelectedItem as Utm;

    void ExtractMarks()
    {
        var d = SelectedDocument(); if (d == null) { grid.DataSource = null; return; }
        var rows = Extract(d.RawJson).Select((raw, i) => { var p = Parse(raw); return new MarkCheckRow(i + 1, raw, p.Type, p.Rank, p.Number, "Не проверена", ""); }).ToList();
        grid.DataSource = rows; state.Text = $"ТТН {d.Number}: найдено марок {rows.Count}"; progress.Value = 0;
    }

    async void CheckAllAsync()
    {
        var u = SelectedUtm(); var d = SelectedDocument();
        if (u == null || d == null) { MessageBox.Show(this, "Выберите УТМ и ТТН."); return; }
        var marks = Extract(d.RawJson).Distinct(StringComparer.Ordinal).ToList();
        if (marks.Count == 0) { MessageBox.Show(this, "В исходных данных ТТН не найдены акцизные марки."); return; }
        stop.Dispose(); stop = new CancellationTokenSource();
        var rows = marks.Select((raw, i) => { var p = Parse(raw); return new MarkCheckRow(i + 1, raw, p.Type, p.Rank, p.Number, "В очереди", ""); }).ToList();
        grid.DataSource = rows; progress.Maximum = rows.Count; progress.Value = 0;
        var ok = 0; var errors = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (stop.IsCancellationRequested) break;
            var row = rows[i];
            try
            {
                row.Status = "Отправка..."; grid.Refresh();
                var p = new MarkParts(row.Type, row.Rank, row.Number, row.Raw);
                if (string.IsNullOrWhiteSpace(p.Type) || string.IsNullOrWhiteSpace(p.Rank) || string.IsNullOrWhiteSpace(p.Number))
                {
                    row.Status = "Некорректная марка"; row.Response = "Не удалось выделить Type/Rank/Number."; errors++;
                    db.SaveMarkResult(u.Id, d.Id, row.Raw, p, "ERROR", row.Response);
                }
                else
                {
                    var sent = await client.QueryBarcodeAsync(u, p, stop.Token);
                    if (!sent.Success) { row.Status = "Ошибка отправки"; row.Response = sent.Message + " " + Short(sent.Raw, 500); errors++; db.SaveMarkResult(u.Id, d.Id, row.Raw, p, "ERROR", row.Response); }
                    else if (string.IsNullOrWhiteSpace(sent.Ticket)) { row.Status = "Отправлено"; row.Response = Short(sent.Raw, 500); db.SaveMarkResult(u.Id, d.Id, row.Raw, p, "SUCCESS", row.Response); ok++; }
                    else
                    {
                        var result = await client.PollTicketAsync(u, sent.Ticket!, stop.Token);
                        var bad = !result.Success || IsError(result.Raw);
                        row.Status = bad ? "Ошибка ЕГАИС" : "OK"; row.Response = Short(result.Raw, 700);
                        db.SaveMarkResult(u.Id, d.Id, row.Raw, p, bad ? "ERROR" : "OK", row.Response); if (bad) errors++; else ok++;
                    }
                }
            }
            catch (OperationCanceledException) { row.Status = "Остановлено"; row.Response = "Проверка остановлена пользователем."; break; }
            catch (Exception ex) { row.Status = "Ошибка"; row.Response = ex.Message; errors++; }
            progress.Value = i + 1; state.Text = $"Проверено: {i + 1}/{rows.Count} | OK: {ok} | Ошибки: {errors}"; grid.Refresh();
        }
        state.Text = stop.IsCancellationRequested ? $"Остановлено. OK: {ok}, ошибок: {errors}" : $"Готово. OK: {ok}, ошибок: {errors}";
    }

    void Stop() => stop.Cancel();

    static bool IsError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var s = raw.ToLowerInvariant();
        return s.Contains("error") || s.Contains("ошиб") || s.Contains("reject") || s.Contains("отказ") || s.Contains("invalid") || s.Contains("не найден");
    }

    static IEnumerable<string> Extract(string raw)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(raw ?? "", @"(?<!\d)\d{14}(?!\d)")) found.Add(m.Value);
        foreach (Match m in Regex.Matches(raw ?? "", @"(?<!\d)\d{3}[\x1D\x1E\x1F\x04]\d{3}[\x1D\x1E\x1F\x04]\d{8}(?!\d)"))
        { var digits = Regex.Replace(m.Value, @"\D", ""); if (digits.Length == 14) found.Add(digits); }
        return found;
    }

    static MarkParts Parse(string raw)
    {
        var d = Regex.Replace(raw ?? "", @"\D", "");
        if (d.Length >= 14) return new(d[..3], d.Substring(3, 3), d.Substring(6, 8), raw);
        return new("", "", "", raw);
    }

    static string Short(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n] + "…";
    Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true, Height = 30 }; b.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "ЕГАИС", MessageBoxButtons.OK, MessageBoxIcon.Error); } }; return b; }

    sealed class MarkCheckRow
    {
        public int RowNumber { get; }
        public string Raw { get; }
        public string Type { get; }
        public string Rank { get; }
        public string Number { get; }
        public string Status { get; set; }
        public string Response { get; set; }
        public MarkCheckRow(int n, string raw, string type, string rank, string number, string status, string response) { RowNumber = n; Raw = raw; Type = type; Rank = rank; Number = number; Status = status; Response = response; }
    }
}
