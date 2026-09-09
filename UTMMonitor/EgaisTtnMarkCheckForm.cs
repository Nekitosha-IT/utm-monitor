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
    readonly int? initialUtmId;
    readonly string? initialDocumentId;
    CancellationTokenSource stop = new();
    List<DocumentRow> docs = [];

    public EgaisTtnMarkCheckForm(Utm? initialUtm = null, DocumentRow? initialDocument = null)
    {
        initialUtmId = initialUtm?.Id;
        initialDocumentId = initialDocument?.Id;
        Text = "ЕГАИС — Проверка марок ТТН через УТМ";
        Width = 1450; Height = 850; MinimumSize = new(1050, 650); StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new(SizeType.Absolute, 82)); root.RowStyles.Add(new(SizeType.Percent, 100));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
        top.Controls.Add(new Label { Text = "УТМ:", AutoSize = true, Padding = new Padding(0, 7, 4, 0) }); top.Controls.Add(utm);
        top.Controls.Add(new Label { Text = "ТТН:", AutoSize = true, Padding = new Padding(14, 7, 4, 0) }); top.Controls.Add(document);
        top.Controls.Add(Button("Загрузить ТТН", LoadDocuments)); top.Controls.Add(Button("Извлечь марки", ExtractMarks));
        top.Controls.Add(Button("Проверить все", CheckAllAsync)); top.Controls.Add(Button("Остановить", Stop)); top.Controls.Add(progress); top.Controls.Add(state);
        root.Controls.Add(top, 0, 0); root.Controls.Add(grid, 0, 1); Controls.Add(root);
        Load += (_, _) => LoadUtms();
        utm.SelectedIndexChanged += (_, _) => LoadDocuments();
        document.SelectedIndexChanged += (_, _) => ExtractMarks();
        FormClosed += (_, _) => { stop.Cancel(); stop.Dispose(); client.Dispose(); };
    }

    void LoadUtms()
    {
        var list = db.All(); utm.DataSource = list; utm.DisplayMember = "Name"; utm.ValueMember = "Id";
        if (initialUtmId.HasValue) utm.SelectedItem = list.FirstOrDefault(x => x.Id == initialUtmId.Value);
        if (utm.SelectedIndex < 0 && list.Count > 0) utm.SelectedIndex = 0;
    }

    void LoadDocuments()
    {
        var u = SelectedUtm(); if (u == null) return;
        docs = db.Documents(u.Id); document.DataSource = docs; document.DisplayMember = nameof(DocumentRow.Number); document.ValueMember = nameof(DocumentRow.Id);
        if (!string.IsNullOrWhiteSpace(initialDocumentId)) document.SelectedItem = docs.FirstOrDefault(x => x.Id.Equals(initialDocumentId, StringComparison.OrdinalIgnoreCase));
        if (document.SelectedIndex < 0 && docs.Count > 0) document.SelectedIndex = 0;
        state.Text = $"ТТН: {docs.Count}";
    }

    DocumentRow? SelectedDocument() => document.SelectedItem as DocumentRow;
    Utm? SelectedUtm() => utm.SelectedItem as Utm;

    void ExtractMarks()
    {
        var d = SelectedDocument(); if (d == null) { grid.DataSource = null; return; }
        var marks = EgaisMarkAnalyzer.ExtractMarks(d.RawJson);
        var rows = marks.Select((m, i) => new MarkCheckRow(i + 1, m.Raw, m.Parts.Type, m.Parts.Rank, m.Parts.Number, m.Duplicate ? "Дубликат" : "Не проверена", m.Duplicate ? "Та же Type/Rank/Number уже найдена в ТТН." : "")).ToList();
        grid.DataSource = rows; progress.Value = 0; state.Text = $"ТТН {d.Number}: уникальных марок {rows.Count(x => x.Status != "Дубликат")}, дублей {rows.Count(x => x.Status == "Дубликат")}";
    }

    async void CheckAllAsync()
    {
        var u = SelectedUtm(); var d = SelectedDocument(); if (u == null || d == null) { MessageBox.Show(this, "Выберите УТМ и ТТН."); return; }
        var marks = EgaisMarkAnalyzer.ExtractMarks(d.RawJson).Where(x => !x.Duplicate).ToList();
        if (marks.Count == 0) { MessageBox.Show(this, "В исходных данных ТТН не найдены уникальные акцизные марки."); return; }
        stop.Dispose(); stop = new CancellationTokenSource();
        var rows = marks.Select((m, i) => new MarkCheckRow(i + 1, m.Raw, m.Parts.Type, m.Parts.Rank, m.Parts.Number, "В очереди", "")).ToList();
        grid.DataSource = rows; progress.Maximum = rows.Count; progress.Value = 0;
        var ok = 0; var errors = 0; var unknown = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (stop.IsCancellationRequested) break;
            var row = rows[i];
            try
            {
                row.Status = "Отправка..."; grid.Refresh();
                var p = new MarkParts(row.Type, row.Rank, row.Number, row.Raw);
                var sent = await client.QueryBarcodeAsync(u, p, stop.Token);
                if (!sent.Success)
                {
                    row.Status = "Ошибка УТМ"; row.Response = Short(sent.Message + " " + sent.Raw, 700); errors++;
                    db.SaveMarkResult(u.Id, d.Id, row.Raw, p, "ERROR", row.Response);
                }
                else if (string.IsNullOrWhiteSpace(sent.Ticket))
                {
                    var a = EgaisMarkAnalyzer.AnalyzeResponse(sent.Raw);
                    row.Status = DisplayStatus(a.Status); row.Response = Short(a.Message + " | " + sent.Raw, 700);
                    SaveStatus(u, d, row, p, a.Status, row.Response, ref ok, ref errors, ref unknown);
                }
                else
                {
                    row.Status = "Ожидание ЕГАИС..."; grid.Refresh();
                    var result = await client.PollTicketAsync(u, sent.Ticket, stop.Token);
                    if (!result.Success)
                    {
                        row.Status = "Таймаут"; row.Response = Short(result.Status + " " + result.Raw, 700); errors++;
                        db.SaveMarkResult(u.Id, d.Id, row.Raw, p, "TIMEOUT", row.Response);
                    }
                    else
                    {
                        var a = EgaisMarkAnalyzer.AnalyzeResponse(result.Raw);
                        row.Status = DisplayStatus(a.Status); row.Response = Short(a.Message + " | " + result.Raw, 700);
                        SaveStatus(u, d, row, p, a.Status, row.Response, ref ok, ref errors, ref unknown);
                    }
                }
            }
            catch (OperationCanceledException) { row.Status = "Остановлено"; row.Response = "Проверка остановлена пользователем."; break; }
            catch (Exception ex) { row.Status = "Ошибка"; row.Response = ex.Message; errors++; }
            progress.Value = Math.Min(i + 1, progress.Maximum); state.Text = $"Проверено: {i + 1}/{rows.Count} | OK: {ok} | Ошибки: {errors} | Не распознано: {unknown}"; grid.Refresh();
        }
        state.Text = stop.IsCancellationRequested ? $"Остановлено. OK: {ok}, ошибок: {errors}, неизвестно: {unknown}" : $"Готово. OK: {ok}, ошибок: {errors}, неизвестно: {unknown}";
    }

    void SaveStatus(Utm u, DocumentRow d, MarkCheckRow row, MarkParts p, string status, string response, ref int ok, ref int errors, ref int unknown)
    {
        var stored = status switch { "OK" => "OK", "EGAIS_ERROR" => "ERROR", "TIMEOUT" => "TIMEOUT", "UTM_ERROR" => "ERROR", _ => "UNKNOWN" };
        db.SaveMarkResult(u.Id, d.Id, row.Raw, p, stored, response);
        if (status == "OK") ok++; else if (status == "UNKNOWN") unknown++; else errors++;
    }

    static string DisplayStatus(string s) => s switch { "OK" => "OK", "EGAIS_ERROR" => "Ошибка ЕГАИС", "TIMEOUT" => "Таймаут", "UTM_ERROR" => "Ошибка УТМ", _ => "Не распознано" };
    void Stop() => stop.Cancel();
    static string Short(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n] + "…";
    Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true, Height = 30 }; b.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "ЕГАИС", MessageBoxButtons.OK, MessageBoxIcon.Error); } }; return b; }

    sealed class MarkCheckRow
    {
        public int RowNumber { get; } public string Raw { get; } public string Type { get; } public string Rank { get; } public string Number { get; }
        public string Status { get; set; } public string Response { get; set; }
        public MarkCheckRow(int n, string raw, string type, string rank, string number, string status, string response) { RowNumber = n; Raw = raw; Type = type; Rank = rank; Number = number; Status = status; Response = response; }
    }
}
