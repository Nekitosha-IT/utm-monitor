using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace UTMMonitor;

public sealed class EgaisArchiveAdvancedForm : Form
{
    readonly UtmDatabase db = new();
    readonly UtmClient client = new();
    readonly ComboBox utm = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    readonly DateTimePicker fromDate = new() { Format = DateTimePickerFormat.Short, Width = 105 };
    readonly DateTimePicker toDate = new() { Format = DateTimePickerFormat.Short, Width = 105 };
    readonly ComboBox direction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
    readonly ComboBox product = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    readonly ComboBox supplier = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    readonly ComboBox sender = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    readonly ComboBox receiver = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    readonly ComboBox status = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    readonly TextBox search = new() { Width = 220, PlaceholderText = "Марка, ТТН, документ, товар..." };
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    readonly ProgressBar progress = new() { Width = 180, Style = ProgressBarStyle.Marquee, Visible = false };
    readonly Label state = new() { AutoSize = true, Text = "Готово" };
    CancellationTokenSource? cts;
    List<ArchiveItem> allRows = [];
    List<ArchiveItem> shownRows = [];

    public EgaisArchiveAdvancedForm()
    {
        Text = "ЕГАИС — Архив документов и акцизных марок";
        Width = 1800; Height = 950; MinimumSize = new(1300, 750); StartPosition = FormStartPosition.CenterParent;
        direction.Items.AddRange(["Все", "Входящие", "Исходящие"]); direction.SelectedIndex = 0;
        status.Items.Add("Все"); status.SelectedIndex = 0;
        fromDate.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        toDate.Value = fromDate.Value.AddMonths(1).AddDays(-1);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
        root.RowStyles.Add(new(SizeType.Absolute, 150)); root.RowStyles.Add(new(SizeType.Percent, 100));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
        Add(top, "УТМ:", utm); Add(top, "С:", fromDate); Add(top, "По:", toDate); Add(top, "Направление:", direction);
        Add(top, "Товар:", product); Add(top, "Поставщик:", supplier); Add(top, "От кого:", sender); Add(top, "Кому:", receiver); Add(top, "Статус:", status); Add(top, "Поиск:", search);
        top.Controls.Add(Button("Загрузить весь доступный период", LoadPeriod)); top.Controls.Add(Button("Обновить архив", LoadLocal)); top.Controls.Add(Button("Сбросить фильтры", ResetFilters)); top.Controls.Add(Button("Экспорт CSV", Export)); top.Controls.Add(Button("Стоп", Stop)); top.Controls.Add(progress); top.Controls.Add(state);
        root.Controls.Add(top, 0, 0); root.Controls.Add(grid, 0, 1); Controls.Add(root);

        Load += (_, _) => { LoadUtms(); LoadLocal(); };
        foreach (var c in new Control[] { direction, product, supplier, sender, receiver, status }) c.SelectedValueChanged += (_, _) => ApplyFilter();
        search.TextChanged += (_, _) => ApplyFilter();
        grid.CellDoubleClick += OpenMarkCard;
        FormClosed += (_, _) => { cts?.Cancel(); client.Dispose(); };
    }

    static void Add(Control parent, string caption, Control control)
    {
        parent.Controls.Add(new Label { Text = caption, AutoSize = true, Padding = new Padding(8, 8, 3, 0) }); parent.Controls.Add(control);
    }

    Button Button(string text, Action action)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(9, 0, 9, 0) };
        b.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error); } };
        return b;
    }

    void LoadUtms()
    {
        var list = db.All(); utm.DataSource = list; utm.DisplayMember = "Name"; utm.ValueMember = "Id";
    }

    Utm? SelectedUtm() => utm.SelectedItem as Utm;
    void Stop() => cts?.Cancel();

    void ResetFilters()
    {
        direction.SelectedIndex = 0; product.SelectedIndex = 0; supplier.SelectedIndex = 0; sender.SelectedIndex = 0; receiver.SelectedIndex = 0; status.SelectedIndex = 0; search.Clear(); ApplyFilter();
    }

    void LoadLocal()
    {
        var u = SelectedUtm(); if (u == null) return;
        var start = fromDate.Value.Date; var end = toDate.Value.Date.AddDays(1);
        var docs = db.Documents(u.Id).Where(d => InPeriod(d.Date, start, end) && DirectionMatches(d.Direction)).ToList();
        allRows = BuildRows(docs, u.Id); RebuildFilters(); ApplyFilter();
        state.Text = $"Локальный архив: документов {docs.Count}, строк {allRows.Count}";
    }

    async void LoadPeriod()
    {
        var u = SelectedUtm(); if (u == null) { MessageBox.Show(this, "Добавьте УТМ и выберите его."); return; }
        cts?.Cancel(); cts = new CancellationTokenSource(); progress.Visible = true;
        try
        {
            var ct = cts.Token;
            state.Text = "Получение входящих документов из УТМ...";
            var incoming = await client.DocumentsAsync(u, "/api/db/in/list", "Входящие", ct);
            ct.ThrowIfCancellationRequested();
            state.Text = $"Входящих получено: {incoming.Count}. Получение исходящих...";
            var outgoing = await client.DocumentsAsync(u, "/api/db/out/list", "Исходящие", ct);
            ct.ThrowIfCancellationRequested();
            state.Text = "Получение очереди исходящих документов...";
            var queue = await client.OutQueueAsync(u, ct);
            var all = incoming.Concat(outgoing).Concat(queue).GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
            var start = fromDate.Value.Date; var end = toDate.Value.Date.AddDays(1);
            var docs = all.Where(d => InPeriod(d.Date, start, end) && DirectionMatches(d.Direction)).ToList();
            db.SaveDocuments(u.Id, docs);
            allRows = BuildRows(docs, u.Id); RebuildFilters(); ApplyFilter();
            state.Text = $"Загружено: документов {docs.Count}, строк {allRows.Count}. Доступно в УТМ: {all.Count}.";
        }
        catch (OperationCanceledException) { state.Text = "Загрузка остановлена."; }
        catch (Exception ex) { state.Text = "Ошибка загрузки."; MessageBox.Show(this, ex.Message, "ЕГАИС — архив", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { progress.Visible = false; }
    }

    bool DirectionMatches(string value)
    {
        var d = direction.SelectedItem?.ToString() ?? "Все";
        return d == "Все" || (d == "Входящие" && (value.Contains("вход", StringComparison.OrdinalIgnoreCase) || value.Equals("in", StringComparison.OrdinalIgnoreCase))) || (d == "Исходящие" && (value.Contains("исход", StringComparison.OrdinalIgnoreCase) || value.Equals("out", StringComparison.OrdinalIgnoreCase)));
    }

    static bool InPeriod(string value, DateTime start, DateTime end)
    {
        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var d)) return d >= start && d < end;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d)) { d = d.ToLocalTime(); return d >= start && d < end; }
        return false;
    }

    List<ArchiveItem> BuildRows(IEnumerable<DocumentRow> documents, int uid)
    {
        var result = new List<ArchiveItem>();
        var existing = db.Marks(uid).ToList();
        foreach (var d in documents)
        {
            var context = ExtractContext(d.RawJson); var items = ExtractItems(d.RawJson);
            var marks = EgaisMarkAnalyzer.ExtractMarks(d.RawJson).Where(x => !x.Duplicate).ToList();
            var docMarks = existing.Where(x => x.DocumentId.Equals(d.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            if (marks.Count == 0)
            {
                if (items.Count == 0) result.Add(new ArchiveItem(d, "", "", "", "", context.Product, context.Supplier, context.From, context.To, "", "", "", "", "", "Нет марок"));
                foreach (var item in items) result.Add(new ArchiveItem(d, "", "", "", "", item.Product, item.Supplier, context.From, context.To, item.Mark, "", "", "", item.Quantity, item.Price, "Не проверена"));
                continue;
            }
            foreach (var m in marks)
            {
                var item = items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Mark) && (Normalize(x.Mark).Contains(Normalize(m.Raw), StringComparison.OrdinalIgnoreCase) || Normalize(m.Raw).Contains(Normalize(x.Mark), StringComparison.OrdinalIgnoreCase)));
                var checkedMark = docMarks.FirstOrDefault(x => x.Type == m.Parts.Type && x.Rank == m.Parts.Rank && x.Number == m.Parts.Number);
                result.Add(new ArchiveItem(d, m.Raw, m.Parts.Type, m.Parts.Rank, m.Parts.Number, item?.Product ?? context.Product, item?.Supplier ?? context.Supplier, context.From, context.To, item?.Mark ?? m.Raw, item?.Quantity ?? "", item?.Price ?? "", checkedMark?.Status ?? "Не проверена"));
            }
        }
        return result;
    }

    void RebuildFilters()
    {
        Fill(product, allRows.Select(x => x.Product)); Fill(supplier, allRows.Select(x => x.Supplier)); Fill(sender, allRows.Select(x => x.From)); Fill(receiver, allRows.Select(x => x.To)); Fill(status, allRows.Select(x => x.Status));
    }

    static void Fill(ComboBox box, IEnumerable<string> values)
    {
        var current = box.SelectedItem?.ToString() ?? "Все"; var list = values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Take(500).ToList(); list.Insert(0, "Все");
        box.DataSource = list; var idx = list.FindIndex(x => x.Equals(current, StringComparison.OrdinalIgnoreCase)); box.SelectedIndex = idx >= 0 ? idx : 0;
    }

    void ApplyFilter()
    {
        var q = search.Text.Trim(); var p = Pick(product); var s = Pick(supplier); var f = Pick(sender); var r = Pick(receiver); var st = Pick(status);
        shownRows = allRows.Where(x => (p == "Все" || Eq(x.Product, p)) && (s == "Все" || Eq(x.Supplier, s)) && (f == "Все" || Eq(x.From, f)) && (r == "Все" || Eq(x.To, r)) && (st == "Все" || Eq(x.Status, st)) && (string.IsNullOrWhiteSpace(q) || x.Search.Contains(q, StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.Product).ThenBy(x => x.Supplier).ThenBy(x => x.Date).ThenBy(x => x.Mark).ToList();
        grid.DataSource = shownRows.Select((x, i) => new { N = i + 1, Дата = x.FriendlyDate, Направление = x.Direction, Товар = x.Product, Поставщик = x.Supplier, От_кого = x.From, Кому = x.To, Марка = x.Mark, Type = x.Type, Rank = x.Rank, Number = x.NumberPart, Количество = x.Quantity, Цена = x.Price, ТТН = x.Number, Документ = x.DocumentId, Статус = x.Status }).ToList();
        if (grid.Columns.Contains("N")) grid.Columns["N"].HeaderText = "№";
        state.Text = $"Показано строк: {shownRows.Count} из {allRows.Count}";
    }

    static string Pick(ComboBox b) => b.SelectedItem?.ToString() ?? "Все";
    static bool Eq(string a, string b) => string.Equals(a?.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    void OpenMarkCard(object? senderObj, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= shownRows.Count) return; var x = shownRows[e.RowIndex]; if (string.IsNullOrWhiteSpace(x.Mark)) return;
        using var card = new EgaisMarkArchiveDetailsForm(x.Mark, x.Type, x.Rank, x.NumberPart, x.Product, x.Supplier, x.From, x.To, x.Number, x.FriendlyDate, x.Status, x.Raw);
        card.ShowDialog(this);
    }

    void Export()
    {
        using var dlg = new SaveFileDialog { FileName = $"egais-archive-{fromDate.Value:yyyyMMdd}-{toDate.Value:yyyyMMdd}.csv", Filter = "CSV UTF-8|*.csv" }; if (dlg.ShowDialog(this) != DialogResult.OK) return;
        using var sw = new StreamWriter(dlg.FileName, false, new UTF8Encoding(true)); sw.WriteLine("Дата;Направление;Товар;Поставщик;От кого;Кому;Марка;Type;Rank;Number;Количество;Цена;ТТН;Документ;Статус");
        foreach (var x in shownRows) sw.WriteLine(string.Join(';', new[] { Csv(x.FriendlyDate), Csv(x.Direction), Csv(x.Product), Csv(x.Supplier), Csv(x.From), Csv(x.To), Csv(x.Mark), Csv(x.Type), Csv(x.Rank), Csv(x.NumberPart), Csv(x.Quantity), Csv(x.Price), Csv(x.Number), Csv(x.DocumentId), Csv(x.Status) }));
        state.Text = $"Экспортировано строк: {shownRows.Count}";
    }

    static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    static string Normalize(string s) => new((s ?? "").Where(char.IsDigit).ToArray());

    static Context ExtractContext(string raw)
    {
        var from = JsonOrXml(raw, ["supplier", "shipper", "sender", "consignor", "from", "seller"]);
        var to = JsonOrXml(raw, ["receiver", "consignee", "recipient", "buyer", "to", "client"]);
        var product = JsonOrXml(raw, ["productName", "goodsName", "alcoholName", "fullName", "name"]);
        var supplier = JsonOrXml(raw, ["supplierName", "shipperName", "sellerName", "producerName", "supplier"]);
        return new(product, supplier, from, to);
    }

    static string JsonOrXml(string raw, string[] keys)
    {
        try { using var j = JsonDocument.Parse(raw); var v = FindJson(j.RootElement, keys); if (!string.IsNullOrWhiteSpace(v)) return v; } catch { }
        try { var x = XDocument.Parse(raw).Root; if (x != null) foreach (var k in keys) { var e = x.DescendantsAndSelf().FirstOrDefault(z => z.Name.LocalName.Equals(k, StringComparison.OrdinalIgnoreCase)); if (e != null && !string.IsNullOrWhiteSpace(e.Value)) return e.Value.Trim(); } } catch { }
        return "";
    }

    static string FindJson(JsonElement e, string[] keys)
    {
        if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) { if (keys.Any(k => p.Name.Equals(k, StringComparison.OrdinalIgnoreCase)) && p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number) return p.Value.ToString(); var v = FindJson(p.Value, keys); if (!string.IsNullOrWhiteSpace(v)) return v; }
        if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) { var v = FindJson(x, keys); if (!string.IsNullOrWhiteSpace(v)) return v; }
        return "";
    }

    static List<RawItem> ExtractItems(string raw)
    {
        var result = new List<RawItem>();
        try
        {
            using var j = JsonDocument.Parse(raw);
            foreach (var e in Walk(j.RootElement))
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var mark = FindJson(e, ["mark", "markCode", "barcode", "exciseMark", "egaisMark"]);
                if (string.IsNullOrWhiteSpace(mark)) continue;
                result.Add(new RawItem(FindJson(e, ["productName", "goodsName", "name"]), FindJson(e, ["supplier", "supplierName", "shipperName"]), FindJson(e, ["quantity", "count", "volume"]), FindJson(e, ["price", "cost"]), mark));
            }
        }
        catch { }
        try
        {
            var x = XDocument.Parse(raw);
            foreach (var e in x.Descendants().Where(z => z.HasElements))
            {
                var mark = e.Descendants().FirstOrDefault(z => ["mark", "markCode", "barcode", "exciseMark", "egaisMark"].Any(k => z.Name.LocalName.Equals(k, StringComparison.OrdinalIgnoreCase)))?.Value?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(mark)) continue;
                result.Add(new RawItem(Desc(e, ["productName", "goodsName", "name"]), Desc(e, ["supplier", "supplierName", "shipperName"]), Desc(e, ["quantity", "count", "volume"]), Desc(e, ["price", "cost"]), mark));
            }
        }
        catch { }
        return result.GroupBy(x => Normalize(x.Mark), StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
    }

    static IEnumerable<JsonElement> Walk(JsonElement e)
    {
        yield return e;
        if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) foreach (var x in Walk(p.Value)) yield return x;
        if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) foreach (var y in Walk(x)) yield return y;
    }

    static string Desc(XElement e, string[] keys) => e.DescendantsAndSelf().FirstOrDefault(z => keys.Any(k => z.Name.LocalName.Equals(k, StringComparison.OrdinalIgnoreCase)))?.Value?.Trim() ?? "";

    readonly record struct Context(string Product, string Supplier, string From, string To);
    readonly record struct RawItem(string Product, string Supplier, string Quantity, string Price, string Mark);
    readonly record struct ArchiveItem(DocumentRow Doc, string RawMark, string Type, string Rank, string NumberPart, string Product, string Supplier, string From, string To, string Mark, string Quantity, string Price, string Status)
    {
        public string Date => Doc.Date;
        public string Direction => Doc.Direction;
        public string Number => Doc.Number;
        public string DocumentId => Doc.Id;
        public string FriendlyDate => Date;
        public string Raw => Doc.RawJson;
        public string Search => string.Join(' ', new[] { Product, Supplier, From, To, Mark, Type, Rank, NumberPart, Number, DocumentId, Status });
    }
}