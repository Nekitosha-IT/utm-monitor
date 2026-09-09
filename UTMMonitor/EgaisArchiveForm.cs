using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace UTMMonitor;

public sealed class EgaisArchiveForm : Form
{
    readonly UtmDatabase db = new();
    readonly UtmClient client = new();
    readonly ComboBox utm = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    readonly DateTimePicker month = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM yyyy", ShowUpDown = true, Width = 150 };
    readonly ComboBox direction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    readonly TextBox search = new() { Width = 260, PlaceholderText = "Марка, товар, поставщик, от кого, кому..." };
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    readonly ProgressBar progress = new() { Width = 220, Style = ProgressBarStyle.Marquee, Visible = false };
    readonly Label state = new() { AutoSize = true, Text = "Выберите месяц и нажмите «Загрузить период»" };
    CancellationTokenSource? cts;
    List<ArchiveRow> rows = [];

    public EgaisArchiveForm()
    {
        Text = "ЕГАИС — Архив и загрузка за период"; Width = 1600; Height = 900; MinimumSize = new(1200, 700); StartPosition = FormStartPosition.CenterParent;
        direction.Items.AddRange(["Все", "Входящие", "Исходящие"]); direction.SelectedIndex = 0; month.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) }; root.RowStyles.Add(new(SizeType.Absolute, 82)); root.RowStyles.Add(new(SizeType.Percent, 100));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
        top.Controls.Add(new Label { Text = "УТМ:", AutoSize = true, Padding = new Padding(0, 8, 3, 0) }); top.Controls.Add(utm);
        top.Controls.Add(new Label { Text = "Месяц:", AutoSize = true, Padding = new Padding(14, 8, 3, 0) }); top.Controls.Add(month);
        top.Controls.Add(new Label { Text = "Направление:", AutoSize = true, Padding = new Padding(14, 8, 3, 0) }); top.Controls.Add(direction);
        top.Controls.Add(new Label { Text = "Поиск:", AutoSize = true, Padding = new Padding(14, 8, 3, 0) }); top.Controls.Add(search);
        top.Controls.Add(Button("Загрузить период", LoadPeriod)); top.Controls.Add(Button("Обновить из архива", LoadLocal)); top.Controls.Add(Button("Экспорт CSV", Export)); top.Controls.Add(Button("Стоп", Stop)); top.Controls.Add(progress); top.Controls.Add(state);
        root.Controls.Add(top, 0, 0); root.Controls.Add(grid, 0, 1); Controls.Add(root);
        Load += (_, _) => { LoadUtms(); LoadLocal(); };
        search.TextChanged += (_, _) => ApplyFilter();
        FormClosed += (_, _) => { cts?.Cancel(); client.Dispose(); };
    }

    void LoadUtms() { var list = db.All(); utm.DataSource = list; utm.DisplayMember = "Name"; utm.ValueMember = "Id"; }
    Utm? SelectedUtm() => utm.SelectedItem as Utm;
    void Stop() { cts?.Cancel(); state.Text = "Остановка загрузки..."; }

    void LoadLocal()
    {
        var u = SelectedUtm(); if (u == null) return;
        var start = new DateTime(month.Value.Year, month.Value.Month, 1); var end = start.AddMonths(1);
        rows = BuildRows(db.Documents(u.Id).Where(d => InPeriod(d.Date, start, end) && DirectionMatches(d.Direction)).ToList(), u.Id);
        ApplyFilter(); state.Text = $"Архив: {rows.Count} строк за {start:MMMM yyyy}";
    }

    async void LoadPeriod()
    {
        var u = SelectedUtm(); if (u == null) { MessageBox.Show(this, "Добавьте УТМ и выберите его."); return; }
        cts?.Cancel(); cts = new CancellationTokenSource(); progress.Visible = true; state.Text = "Получение входящих и исходящих документов из УТМ...";
        try
        {
            var ct = cts.Token; var incoming = await client.DocumentsAsync(u, "/api/db/in/list", "Входящие", ct); ct.ThrowIfCancellationRequested();
            state.Text = $"Входящие: {incoming.Count}. Получение исходящих...";
            var outgoing = await client.DocumentsAsync(u, "/api/db/out/list", "Исходящие", ct); ct.ThrowIfCancellationRequested();
            var queue = await client.OutQueueAsync(u, ct);
            var all = incoming.Concat(outgoing).Concat(queue).GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
            var start = new DateTime(month.Value.Year, month.Value.Month, 1); var end = start.AddMonths(1);
            var period = all.Where(d => InPeriod(d.Date, start, end) && DirectionMatches(d.Direction)).ToList();
            db.SaveDocuments(u.Id, period);
            rows = BuildRows(period, u.Id); ApplyFilter();
            state.Text = $"Загружено в архив: документов {period.Count}, строк {rows.Count}. Период {start:dd.MM.yyyy}–{end.AddDays(-1):dd.MM.yyyy}.";
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
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d)) return d.ToLocalTime() >= start && d.ToLocalTime() < end;
        return false;
    }

    List<ArchiveRow> BuildRows(IEnumerable<DocumentRow> documents, int uid)
    {
        var result = new List<ArchiveRow>();
        foreach (var d in documents)
        {
            var context = ExtractContext(d.RawJson);
            var marks = EgaisMarkAnalyzer.ExtractMarks(d.RawJson).Where(x => !x.Duplicate).ToList();
            var items = ExtractItems(d.RawJson);
            var existing = db.Marks(uid).Where(x => x.DocumentId.Equals(d.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            if (marks.Count == 0)
            {
                foreach (var item in items) result.Add(new(d.Id, d.Date, d.Direction, d.Type, d.Number, item.Product, item.Supplier, context.From, context.To, "", "", "", item.Quantity.ToString("0.###", CultureInfo.InvariantCulture), item.Price.ToString("0.##", CultureInfo.InvariantCulture), item.Mark, existing.FirstOrDefault()?.Status ?? "Не проверена"));
                if (items.Count == 0) result.Add(new(d.Id, d.Date, d.Direction, d.Type, d.Number, context.Product, context.Supplier, context.From, context.To, "", "", "", "", "", "", "Нет марок"));
                continue;
            }
            foreach (var m in marks)
            {
                var item = items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Mark) && NormalizeMark(x.Mark).Contains(NormalizeMark(m.Raw), StringComparison.OrdinalIgnoreCase));
                var checkedMark = existing.FirstOrDefault(x => x.Type == m.Parts.Type && x.Rank == m.Parts.Rank && x.Number == m.Parts.Number);
                result.Add(new(d.Id, d.Date, d.Direction, d.Type, d.Number, item.Product ?? context.Product, item.Supplier ?? context.Supplier, context.From, context.To, m.Raw, m.Parts.Type, m.Parts.Rank, m.Parts.Number, item.Quantity.ToString("0.###", CultureInfo.InvariantCulture), item.Price.ToString("0.##", CultureInfo.InvariantCulture), checkedMark?.Status ?? "Не проверена"));
            }
        }
        return result;
    }

    void ApplyFilter()
    {
        var q = search.Text.Trim(); var filtered = rows.Where(x => string.IsNullOrWhiteSpace(q) || x.Search.Contains(q, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Product).ThenBy(x => x.Supplier).ThenBy(x => x.Date).ToList();
        grid.DataSource = filtered.Select(x => new { Дата = FriendlyDate(x.Date), Направление = x.Direction, Товар = x.Product, Поставщик = x.Supplier, От_кого = x.From, Кому = x.To, Марка = x.Mark, Type = x.Type, Rank = x.Rank, Number = x.NumberPart, Количество = x.Quantity, Цена = x.Price, ТТН = x.Number, Документ = x.DocumentId, Статус = x.Status }).ToList();
    }

    void Export()
    {
        var q = search.Text.Trim(); var data = rows.Where(x => string.IsNullOrWhiteSpace(q) || x.Search.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        using var dlg = new SaveFileDialog { FileName = $"egais-archive-{month.Value:yyyy-MM}.csv", Filter = "CSV UTF-8|*.csv" }; if (dlg.ShowDialog(this) != DialogResult.OK) return;
        using var sw = new StreamWriter(dlg.FileName, false, new UTF8Encoding(true)); sw.WriteLine("Дата;Направление;Товар;Поставщик;От кого;Кому;Марка;Type;Rank;Number;Количество;Цена;ТТН;Документ;Статус");
        foreach (var x in data) sw.WriteLine(string.Join(';', new[] { Csv(FriendlyDate(x.Date)), Csv(x.Direction), Csv(x.Product), Csv(x.Supplier), Csv(x.From), Csv(x.To), Csv(x.Mark), Csv(x.Type), Csv(x.Rank), Csv(x.NumberPart), Csv(x.Quantity), Csv(x.Price), Csv(x.Number), Csv(x.DocumentId), Csv(x.Status) }));
        state.Text = $"Экспортировано строк: {data.Count}";
    }

    static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    static string FriendlyDate(string s) => DateTime.TryParse(s, out var d) ? d.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") : s;
    static string NormalizeMark(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());
    Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(9, 0, 9, 0) }; b.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error); } }; return b; }

    static Context ExtractContext(string raw)
    {
        var values = new List<string>();
        try { using var j = JsonDocument.Parse(raw); values.AddRange(JsonStrings(j.RootElement)); } catch { }
        try { var x = XDocument.Parse(raw); values.AddRange(x.Descendants().Select(e => e.Value).Where(v => !string.IsNullOrWhiteSpace(v))); } catch { }
        string Find(params string[] words) => values.FirstOrDefault(v => words.Any(w => v.Contains(w, StringComparison.OrdinalIgnoreCase))) ?? "";
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
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject()) { if (keys.Any(k => p.Name.Equals(k, StringComparison.OrdinalIgnoreCase)) && p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number) return p.Value.ToString(); var v = FindJson(p.Value, keys); if (v != "") return v; }
        }
        else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) { var v = FindJson(x, keys); if (v != "") return v; }
        return "";
    }

    static IEnumerable<string> JsonStrings(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.String) { yield return e.GetString() ?? ""; yield break; }
        if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) foreach (var s in JsonStrings(x)) yield return s;
        if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) foreach (var s in JsonStrings(p.Value)) yield return s;
    }

    static List<ItemArchive> ExtractItems(string raw)
    {
        var result = new List<ItemArchive>();
        try { using var j = JsonDocument.Parse(raw); foreach (var key in new[] { "items", "positions", "products", "productItems", "product" }) if (FindArray(j.RootElement, key, out var ar)) { foreach (var x in ar.EnumerateArray()) result.Add(new(J(x, "name", "productName", "fullName", "goodsName", "alcoholName"), N(x, "quantity", "qty", "amount", "count"), N(x, "price", "sum", "cost"), J(x, "mark", "barcode", "exciseMark", "markCode", "barcodeDataMatrix", "amc"), J(x, "supplier", "supplierName", "sellerName", "producerName"))); if (result.Count > 0) return result; } } catch { }
        try { var root = XDocument.Parse(raw).Root; if (root != null) foreach (var x in root.Descendants().Where(e => new[] { "Product", "Position", "ProductItem", "Item" }.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase))) result.Add(new(X(x, "name", "productName", "fullName", "goodsName", "alcoholName"), ND(x, "quantity", "qty", "amount", "count"), ND(x, "price", "sum", "cost", "priceWithVat"), X(x, "mark", "barcode", "exciseMark", "markCode", "barcodeDataMatrix", "amc"), X(x, "supplier", "supplierName", "sellerName", "producerName"))); } catch { }
        return result;
    }

    static bool FindArray(JsonElement e, string key, out JsonElement value) { if (e.ValueKind == JsonValueKind.Object) { foreach (var p in e.EnumerateObject()) { if (p.Name.Equals(key, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Array) { value = p.Value; return true; } if (FindArray(p.Value, key, out value)) return true; } } else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) if (FindArray(x, key, out value)) return true; value = default; return false; }
    static string J(JsonElement e, params string[] keys) => FindJson(e, keys);
    static double N(JsonElement e, params string[] keys) => double.TryParse(J(e, keys).Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    static string X(XElement e, params string[] names) { foreach (var n in names) { var a = e.Attributes().FirstOrDefault(x => x.Name.LocalName.Equals(n, StringComparison.OrdinalIgnoreCase)); if (a != null) return a.Value.Trim(); var z = e.DescendantsAndSelf().FirstOrDefault(x => x.Name.LocalName.Equals(n, StringComparison.OrdinalIgnoreCase)); if (z != null) return z.Value.Trim(); } return ""; }
    static double ND(XElement e, params string[] names) => double.TryParse(X(e, names).Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;

    sealed record Context(string Product, string Supplier, string From, string To);
    sealed record ItemArchive(string Product, double Quantity, double Price, string Mark, string Supplier);
    sealed record ArchiveRow(string DocumentId, string Date, string Direction, string DocumentType, string Number, string Product, string Supplier, string From, string To, string Mark, string Type, string Rank, string NumberPart, string Quantity, string Price, string Status)
    {
        public string Search => string.Join(' ', DocumentId, Date, Direction, DocumentType, Number, Product, Supplier, From, To, Mark, Type, Rank, NumberPart, Quantity, Price, Status);
    }
}
