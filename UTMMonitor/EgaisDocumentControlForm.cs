using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace UTMMonitor;

public sealed class EgaisDocumentControlForm : Form
{
    readonly UtmDatabase db = new();
    readonly ComboBox utm = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    readonly TextBox search = new() { Width = 320, PlaceholderText = "Номер, ID, статус, ФСРАР..." };
    readonly DataGridView docs = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    readonly PropertyGrid details = new() { Dock = DockStyle.Fill, HelpVisible = false, ToolbarVisible = false };
    readonly DataGridView marks = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = true, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    readonly DataGridView items = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = true, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    readonly Label state = new() { AutoSize = true, Text = "Готово" };
    List<DocumentRow> allDocs = [];

    public EgaisDocumentControlForm()
    {
        Text = "ЕГАИС — Контроль ТТН и акцизных марок";
        Width = 1450; Height = 900; MinimumSize = new(1100, 700); StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new(SizeType.Absolute, 46)); root.RowStyles.Add(new(SizeType.Percent, 100));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        top.Controls.Add(new Label { Text = "УТМ:", AutoSize = true, Padding = new Padding(0, 7, 4, 0) });
        top.Controls.Add(utm); top.Controls.Add(new Label { Text = "Поиск:", AutoSize = true, Padding = new Padding(10, 7, 4, 0) }); top.Controls.Add(search);
        top.Controls.Add(Button("Обновить", LoadDocuments)); top.Controls.Add(Button("Проверить ТТН", ControlSelected)); top.Controls.Add(Button("Экспорт проблем", ExportProblems)); top.Controls.Add(state);
        root.Controls.Add(top, 0, 0);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 380 };
        split.Panel1.Controls.Add(docs);
        var lower = new TabControl { Dock = DockStyle.Fill };
        var tDetails = new TabPage("Реквизиты"); tDetails.Controls.Add(details);
        var tItems = new TabPage("Товары"); tItems.Controls.Add(items);
        var tMarks = new TabPage("Марки"); tMarks.Controls.Add(marks);
        lower.TabPages.Add(tDetails); lower.TabPages.Add(tItems); lower.TabPages.Add(tMarks);
        split.Panel2.Controls.Add(lower); root.Controls.Add(split, 0, 1); Controls.Add(root);
        Load += (_, _) => { LoadUtms(); LoadDocuments(); };
        search.TextChanged += (_, _) => ApplyFilter();
        docs.SelectionChanged += (_, _) => ShowSelected();
    }

    void LoadUtms()
    {
        var list = db.All(); utm.DataSource = list; utm.DisplayMember = "Name"; utm.ValueMember = "Id";
    }

    Utm? SelectedUtm() => utm.SelectedItem as Utm;

    void LoadDocuments()
    {
        var u = SelectedUtm(); if (u == null) return;
        allDocs = db.Documents(u.Id);
        ApplyFilter(); state.Text = $"Документов: {allDocs.Count}";
    }

    void ApplyFilter()
    {
        var q = search.Text.Trim();
        var rows = allDocs.Where(d => string.IsNullOrWhiteSpace(q) || (d.Id + " " + d.Number + " " + d.Type + " " + d.Direction + " " + d.Status + " " + d.RawJson).Contains(q, StringComparison.OrdinalIgnoreCase)).Select(d => new
        {
            ID = d.Id,
            Направление = FriendlyDirection(d.Direction),
            Документ = FriendlyType(d.Type),
            Номер = string.IsNullOrWhiteSpace(d.Number) ? "—" : d.Number,
            Дата = FriendlyDate(d.Date),
            Состояние = FriendlyStatus(d.Status),
            Позиций = d.Items,
            Контроль = DocumentState(d)
        }).ToList();
        docs.DataSource = rows;
        if (docs.Rows.Count > 0) docs.Rows[0].Selected = true;
    }

    void ShowSelected()
    {
        if (docs.CurrentRow == null) return;
        var id = docs.CurrentRow.Cells[0].Value?.ToString();
        var d = allDocs.FirstOrDefault(x => x.Id == id); if (d == null) return;
        var u = SelectedUtm(); if (u == null) return;
        var parsed = ParseDocument(d.RawJson);
        details.SelectedObject = new DocumentProperties(d, parsed);
        items.DataSource = parsed.Items;
        marks.DataSource = db.Marks(u.Id).Where(x => x.DocumentId.Equals(d.Id, StringComparison.OrdinalIgnoreCase)).Select(x => new
        {
            ID = x.Id, Марка = x.Raw, Type = x.Type, Rank = x.Rank, Number = x.Number,
            Состояние = FriendlyMarkStatus(x.Status), Проверено = FriendlyDate(x.CheckedAt), Ответ = Short(x.Response, 180)
        }).ToList();
    }

    void ControlSelected()
    {
        if (docs.CurrentRow == null) { MessageBox.Show(this, "Выберите ТТН."); return; }
        var id = docs.CurrentRow.Cells[0].Value?.ToString(); var d = allDocs.FirstOrDefault(x => x.Id == id); if (d == null) return;
        var u = SelectedUtm(); if (u == null) return;
        var ms = db.Marks(u.Id).Where(x => x.DocumentId.Equals(d.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        var parsed = ParseDocument(d.RawJson);
        var total = parsed.Items.Sum(x => x.Quantity);
        var markCount = parsed.Items.Count(x => !string.IsNullOrWhiteSpace(x.Mark));
        var checkedOk = ms.Count(x => x.Status.Equals("OK", StringComparison.OrdinalIgnoreCase) || x.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase));
        var errors = ms.Count(x => x.Status.Equals("ERROR", StringComparison.OrdinalIgnoreCase) || x.Status.Equals("TIMEOUT", StringComparison.OrdinalIgnoreCase));
        var missing = Math.Max(0, markCount - ms.Count);
        var text = $"ТТН {d.Number}\r\nТип: {FriendlyType(d.Type)}\r\nНаправление: {FriendlyDirection(d.Direction)}\r\nДата: {FriendlyDate(d.Date)}\r\n\r\nПозиций: {parsed.Items.Count}\r\nКоличество: {total:0.###}\r\nМарок в документе: {markCount}\r\nМарок проверено: {ms.Count}\r\nУспешно: {checkedOk}\r\nОшибок: {errors}\r\nНе проверено: {missing}\r\n\r\nИТОГ: {(errors > 0 ? "ОШИБКА — есть проблемные марки" : missing > 0 ? "ТРЕБУЕТ ПРОВЕРКИ — есть непроверенные марки" : markCount == 0 ? "ВНИМАНИЕ — марки не найдены в разобранных данных" : "OK")}";
        MessageBox.Show(this, text, "Контроль ТТН", MessageBoxButtons.OK, errors > 0 ? MessageBoxIcon.Error : MessageBoxIcon.Information);
        state.Text = errors > 0 ? "ТТН: есть ошибки" : missing > 0 ? "ТТН: требуется проверка" : "ТТН проверена";
    }

    void ExportProblems()
    {
        var u = SelectedUtm(); if (u == null) return;
        var problems = allDocs.Where(d => DocumentState(d) != "OK").ToList();
        using var dlg = new SaveFileDialog { FileName = "egais-problem-documents.csv", Filter = "CSV UTF-8|*.csv" }; if (dlg.ShowDialog(this) != DialogResult.OK) return;
        using var sw = new StreamWriter(dlg.FileName, false, new System.Text.UTF8Encoding(true));
        sw.WriteLine("ID;Направление;Тип;Номер;Дата;Статус;Позиции;Контроль");
        foreach (var d in problems) sw.WriteLine(string.Join(';', new[] { Csv(d.Id), Csv(FriendlyDirection(d.Direction)), Csv(FriendlyType(d.Type)), Csv(d.Number), Csv(FriendlyDate(d.Date)), Csv(FriendlyStatus(d.Status)), d.Items.ToString(CultureInfo.InvariantCulture), Csv(DocumentState(d)) }));
        state.Text = $"Экспортировано проблем: {problems.Count}";
    }

    string DocumentState(DocumentRow d)
    {
        var u = SelectedUtm(); if (u == null) return "—";
        if (string.IsNullOrWhiteSpace(d.Number) || string.IsNullOrWhiteSpace(d.Date)) return "Реквизиты";
        var ms = db.Marks(u.Id).Where(x => x.DocumentId.Equals(d.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (ms.Any(x => x.Status.Equals("ERROR", StringComparison.OrdinalIgnoreCase) || x.Status.Equals("TIMEOUT", StringComparison.OrdinalIgnoreCase))) return "Марки: ошибка";
        return "OK";
    }

    static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    static string FriendlyDirection(string s) => s.Equals("in", StringComparison.OrdinalIgnoreCase) || s.Contains("вход", StringComparison.OrdinalIgnoreCase) ? "Входящая" : s.Equals("out", StringComparison.OrdinalIgnoreCase) || s.Contains("исход", StringComparison.OrdinalIgnoreCase) ? "Исходящая" : string.IsNullOrWhiteSpace(s) ? "—" : s;
    static string FriendlyType(string s) => s switch { "TTN" or "WayBill" or "Waybill" => "Товарно-транспортная накладная", "TTNInformB" => "Подтверждение ТТН", "TTNInformF2" => "Ответ по ТТН", _ => string.IsNullOrWhiteSpace(s) ? "Документ" : s };
    static string FriendlyStatus(string s) => s switch { "NEW" => "Новый", "ACCEPTED" => "Принят", "REJECTED" => "Отклонён", "PROCESSED" => "Обработан", "ERROR" => "Ошибка", _ => string.IsNullOrWhiteSpace(s) ? "—" : s };
    static string FriendlyMarkStatus(string s) => s switch { "OK" or "SUCCESS" => "✓ OK", "ERROR" => "✕ Ошибка", "TIMEOUT" => "⌛ Таймаут", _ => string.IsNullOrWhiteSpace(s) ? "Не проверена" : s };
    static string FriendlyDate(string s) => DateTime.TryParse(s, out var d) ? d.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") : s;
    static string Short(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n] + "…";

    static ParsedDocument ParseDocument(string raw)
    {
        var result = new ParsedDocument();
        try
        {
            using var j = JsonDocument.Parse(raw); ParseJson(j.RootElement, result);
            if (result.Items.Count > 0) return result;
        }
        catch { }
        try { var root = XDocument.Parse(raw).Root; if (root != null) ParseXml(root, result); } catch { }
        return result;
    }

    static void ParseJson(JsonElement root, ParsedDocument result)
    {
        foreach (var p in new[] { "items", "positions", "products", "productItems", "product" })
            if (Find(root, p, out var ar) && ar.ValueKind == JsonValueKind.Array) { var i = 0; foreach (var x in ar.EnumerateArray()) result.Items.Add(new ItemView(i++, J(x, "name", "productName", "fullName", "goodsName"), N(x, "quantity", "qty", "amount", "count"), N(x, "price", "sum", "cost"), J(x, "mark", "barcode", "exciseMark", "markCode", "barcodeDataMatrix", "amc"))); return; }
    }
    static void ParseXml(XElement root, ParsedDocument result)
    {
        var es = root.Descendants().Where(x => new[] { "Product", "Position", "ProductItem", "Item" }.Contains(x.Name.LocalName, StringComparer.OrdinalIgnoreCase)).ToList();
        for (var i = 0; i < es.Count; i++) { var x = es[i]; result.Items.Add(new ItemView(i + 1, X(x, "name", "productName", "fullName", "goodsName"), ND(x, "quantity", "qty", "amount", "count"), ND(x, "price", "sum", "cost", "priceWithVat"), X(x, "mark", "barcode", "exciseMark", "markCode", "barcodeDataMatrix", "amc"))); }
    }
    static bool Find(JsonElement e, string key, out JsonElement value) { if (e.ValueKind == JsonValueKind.Object) { foreach (var p in e.EnumerateObject()) { if (p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; } if (Find(p.Value, key, out value)) return true; } } else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) if (Find(x, key, out value)) return true; value = default; return false; }
    static string J(JsonElement e, params string[] keys) { foreach (var k in keys) if (Find(e, k, out var v) && v.ValueKind != JsonValueKind.Null) return v.ToString(); return ""; }
    static double N(JsonElement e, params string[] keys) => double.TryParse(J(e, keys).Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    static string X(XElement e, params string[] names) { foreach (var n in names) { var a = e.Attributes().FirstOrDefault(x => x.Name.LocalName.Equals(n, StringComparison.OrdinalIgnoreCase)); if (a != null) return a.Value.Trim(); var z = e.DescendantsAndSelf().FirstOrDefault(x => x.Name.LocalName.Equals(n, StringComparison.OrdinalIgnoreCase)); if (z != null) return z.Value.Trim(); } return ""; }
    static double ND(XElement e, params string[] names) => double.TryParse(X(e, names).Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;

    Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(10, 0, 10, 0) }; b.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error); } }; return b; }

    sealed class ParsedDocument { public List<ItemView> Items { get; } = []; }
    sealed record ItemView(int RowNumber, string Наименование, double Количество, double Цена, string Марка);
    sealed class DocumentProperties
    {
        public string ID { get; } public string Номер { get; } public string Дата { get; } public string Тип { get; } public string Направление { get; } public string Статус { get; } public string Контроль { get; } public string ИсходныеДанные { get; }
        public DocumentProperties(DocumentRow d, ParsedDocument p) { ID = d.Id; Номер = d.Number; Дата = FriendlyDate(d.Date); Тип = FriendlyType(d.Type); Направление = FriendlyDirection(d.Direction); Статус = FriendlyStatus(d.Status); Контроль = p.Items.Count > 0 ? "Структура документа разобрана" : "Не удалось выделить позиции — смотреть исходные данные"; ИсходныеДанные = d.RawJson; }
    }
}
