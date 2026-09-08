using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace UTMMonitor;

public sealed class DocumentDetailsForm : Form
{
    public DocumentDetailsForm(DocumentRow d)
    {
        Text = $"ТТН / документ — {d.Number} — {d.Direction}";
        Width = 1250; Height = 780; MinimumSize = new(900, 600); StartPosition = FormStartPosition.CenterParent; Font = new("Segoe UI", 9F);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var human = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new("Segoe UI", 10F) }; human.Text = Human(d);
        var all = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new("Consolas", 9F) }; all.Text = Fields(d.RawJson);
        var raw = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new("Consolas", 9F) }; raw.Text = Pretty(d.RawJson);
        tabs.TabPages.Add(Page("Понятно человеку", human)); tabs.TabPages.Add(Page("Все поля ЕГАИС", all)); tabs.TabPages.Add(Page("Исходные данные", raw)); Controls.Add(tabs);
    }

    static TabPage Page(string s, Control c) { var p = new TabPage(s); p.Controls.Add(c); return p; }

    static string Human(DocumentRow d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("ДОКУМЕНТ ЕГАИС / ТТН");
        sb.AppendLine("══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine($"Направление: {d.Direction}"); sb.AppendLine($"Тип: {d.Type}"); sb.AppendLine($"Номер: {d.Number}"); sb.AppendLine($"Дата: {d.Date}"); sb.AppendLine($"Статус: {d.Status}"); sb.AppendLine($"Позиций: {d.Items}"); sb.AppendLine($"ID: {d.Id}"); sb.AppendLine();
        if (TryJson(d.RawJson, out var j)) HumanJson(j.RootElement, sb);
        else if (TryXml(d.RawJson, out var x)) HumanXml(x.Root!, sb);
        else sb.AppendLine("Не удалось распознать формат ответа. Исходные данные сохранены ниже.");
        return sb.ToString();
    }

    static void HumanJson(JsonElement root, StringBuilder sb)
    {
        sb.AppendLine("ТОВАРЫ / ПОЗИЦИИ"); sb.AppendLine("──────────────────────────────────────────────────────────────────────────────");
        var arrays = new[] { "items", "positions", "products", "productItems", "product" }; bool found = false; int n = 1;
        foreach (var key in arrays)
        {
            if (!Find(root, key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in arr.EnumerateArray())
            {
                found = true; var name = S(item, "name", "productName", "fullName", "shortName", "goodsName", "alcoholName"); var brand = S(item, "brand", "brandName", "tradeMark"); var producer = S(item, "producer", "producerName", "manufacturer", "manufacturerName", "ownerName"); var qty = S(item, "quantity", "qty", "amount", "count"); var volume = S(item, "volume", "volumeValue", "capacity", "volumeMl", "volumeL"); var alc = S(item, "alcohol", "alcoholVolume", "alcoholPercent", "strength"); var price = S(item, "price", "sum", "cost"); var mark = S(item, "mark", "barcode", "exciseMark", "markCode", "ean", "barcodeDataMatrix"); var code = S(item, "code", "productCode", "alcCode", "catalogCode", "fsrarId");
                sb.AppendLine($"{n}. {Fallback(name, "Товар без названия")}"); if (brand != "") sb.AppendLine($"   Бренд: {brand}"); if (producer != "") sb.AppendLine($"   Производитель: {producer}"); if (volume != "") sb.AppendLine($"   Объём: {volume}"); if (alc != "") sb.AppendLine($"   Крепость: {alc}"); if (qty != "") sb.AppendLine($"   Количество: {qty}"); if (price != "") sb.AppendLine($"   Цена/сумма: {price}"); if (code != "") sb.AppendLine($"   Код товара ЕГАИС: {code}"); if (mark != "") sb.AppendLine($"   Акцизная марка: {mark}"); sb.AppendLine(); n++;
            }
        }
        if (!found) sb.AppendLine("Список товаров отдельным массивом не передан. Поля ответа доступны во вкладке «Все поля ЕГАИС».");
        foreach (var key in new[] { "supplier", "shipper", "consignee", "sender", "receiver", "transport", "delivery", "invoice", "waybill", "certificate", "reference", "ttn", "act" }) if (Find(root, key, out var v)) { sb.AppendLine(); sb.AppendLine(HumanTitle(key)); sb.AppendLine(Flatten(v, "  ")); }
    }

    static void HumanXml(XElement root, StringBuilder sb)
    {
        sb.AppendLine("ТОВАРЫ / ПОЗИЦИИ"); sb.AppendLine("──────────────────────────────────────────────────────────────────────────────");
        var items = root.Descendants().Where(e => new[] { "Product", "Position", "ProductItem", "Item" }.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase)).ToList(); int n = 1;
        foreach (var item in items)
        {
            var name = X(item, "name", "productName", "fullName", "shortName", "goodsName", "alcoholName"); var brand = X(item, "brand", "brandName", "tradeMark"); var producer = X(item, "producer", "producerName", "manufacturer", "manufacturerName", "ownerName"); var qty = X(item, "quantity", "qty", "amount", "count"); var volume = X(item, "volume", "volumeValue", "capacity", "volumeMl", "volumeL"); var alc = X(item, "alcohol", "alcoholVolume", "alcoholPercent", "strength"); var price = X(item, "price", "sum", "cost", "priceWithVat"); var mark = X(item, "mark", "barcode", "exciseMark", "markCode", "ean", "barcodeDataMatrix", "amc"); var code = X(item, "code", "productCode", "alcCode", "catalogCode", "fsrarId");
            sb.AppendLine($"{n}. {Fallback(name, "Товар без названия")}"); if (brand != "") sb.AppendLine($"   Бренд: {brand}"); if (producer != "") sb.AppendLine($"   Производитель: {producer}"); if (volume != "") sb.AppendLine($"   Объём: {volume}"); if (alc != "") sb.AppendLine($"   Крепость: {alc}"); if (qty != "") sb.AppendLine($"   Количество: {qty}"); if (price != "") sb.AppendLine($"   Цена/сумма: {price}"); if (code != "") sb.AppendLine($"   Код товара ЕГАИС: {code}"); if (mark != "") sb.AppendLine($"   Акцизная марка: {mark}"); sb.AppendLine(); n++;
        }
        if (items.Count == 0) sb.AppendLine("Список товарных позиций не найден; полный XML доступен во вкладке «Исходные данные».");
        foreach (var key in new[] { "Supplier", "Shipper", "Consignee", "Sender", "Receiver", "Transport", "Delivery", "Invoice", "Waybill", "Certificate", "Reference", "TTN", "Act" }) { var e = root.Descendants().FirstOrDefault(z => z.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase)); if (e != null) { sb.AppendLine(); sb.AppendLine(HumanTitle(key)); sb.AppendLine(FlattenXml(e, "  ")); } }
    }

    static string Fields(string raw)
    {
        if (TryJson(raw, out var j)) { var sb = new StringBuilder(); Dump(j.RootElement, sb, ""); return sb.ToString(); }
        if (TryXml(raw, out var x)) { var sb = new StringBuilder(); DumpXml(x.Root!, sb, ""); return sb.ToString(); }
        return raw;
    }

    static void Dump(JsonElement e, StringBuilder sb, string path)
    {
        if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) Dump(p.Value, sb, path == "" ? p.Name : path + "." + p.Name);
        else if (e.ValueKind == JsonValueKind.Array) { int i = 0; foreach (var x in e.EnumerateArray()) Dump(x, sb, $"{path}[{i++}]"); if (i == 0) sb.AppendLine($"{path} = []"); }
        else sb.AppendLine($"{path} = {e}");
    }

    static void DumpXml(XElement e, StringBuilder sb, string path)
    {
        var p = path == "" ? e.Name.LocalName : path + "." + e.Name.LocalName;
        foreach (var a in e.Attributes()) sb.AppendLine($"{p}.@{a.Name.LocalName} = {a.Value}");
        var children = e.Elements().ToList();
        if (children.Count == 0) { if (!string.IsNullOrWhiteSpace(e.Value)) sb.AppendLine($"{p} = {e.Value.Trim()}"); return; }
        foreach (var c in children) DumpXml(c, sb, p);
    }

    static string Flatten(JsonElement e, string pad) { if (e.ValueKind == JsonValueKind.Object) return string.Join(Environment.NewLine, e.EnumerateObject().Select(x => $"{pad}{x.Name}: {Flatten(x.Value, pad + "  ")}")); if (e.ValueKind == JsonValueKind.Array) return string.Join(Environment.NewLine, e.EnumerateArray().Select((x, i) => $"{pad}[{i}] {Flatten(x, pad + "  ")}")); return e.ToString(); }
    static string FlattenXml(XElement e, string pad) { var sb = new StringBuilder(); foreach (var a in e.Attributes()) sb.AppendLine($"{pad}@{a.Name.LocalName}: {a.Value}"); if (!e.Elements().Any()) { if (!string.IsNullOrWhiteSpace(e.Value)) sb.AppendLine($"{pad}{e.Name.LocalName}: {e.Value.Trim()}"); } else foreach (var c in e.Elements()) sb.Append(FlattenXml(c, pad + "  ")); return sb.ToString().TrimEnd(); }
    static string Pretty(string raw) { if (TryJson(raw, out var j)) return JsonSerializer.Serialize(j.RootElement, new JsonSerializerOptions { WriteIndented = true }); if (TryXml(raw, out var x)) return x.ToString(SaveOptions.None); return raw; }
    static bool TryJson(string raw, out JsonDocument j) { try { j = JsonDocument.Parse(raw); return true; } catch { j = null!; return false; } }
    static bool TryXml(string raw, out XDocument x) { try { x = XDocument.Parse(raw, LoadOptions.PreserveWhitespace); return true; } catch { x = null!; return false; } }
    static string S(JsonElement e, params string[] keys) { foreach (var k in keys) if (Find(e, k, out var v)) return v.ToString(); return ""; }
    static string? X(XElement e, params string[] keys) { foreach (var k in keys) { var a = e.Attributes().FirstOrDefault(z => z.Name.LocalName.Equals(k, StringComparison.OrdinalIgnoreCase)); if (a != null && a.Value.Trim() != "") return a.Value.Trim(); var c = e.DescendantsAndSelf().FirstOrDefault(z => z.Name.LocalName.Equals(k, StringComparison.OrdinalIgnoreCase)); if (c != null && c.Value.Trim() != "") return c.Value.Trim(); } return null; }
    static string Fallback(string? s, string f) => string.IsNullOrWhiteSpace(s) ? f : s!;
    static string HumanTitle(string s) => s.ToLowerInvariant() switch { "supplier" => "ПОСТАВЩИК", "shipper" => "ОТПРАВИТЕЛЬ", "consignee" => "ПОЛУЧАТЕЛЬ", "receiver" => "ПОЛУЧАТЕЛЬ", "sender" => "ОТПРАВИТЕЛЬ", "transport" => "ТРАНСПОРТ", "delivery" => "ДОСТАВКА", "invoice" => "СЧЁТ", "waybill" => "НАКЛАДНАЯ", "certificate" => "СПРАВКА / СЕРТИФИКАТ", "reference" => "СПРАВКА", "ttn" => "ТТН", "act" => "АКТ", _ => s.ToUpperInvariant() };
    static bool Find(JsonElement e, string key, out JsonElement value) { if (e.ValueKind == JsonValueKind.Object) { foreach (var p in e.EnumerateObject()) { if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; } if (Find(p.Value, key, out value)) return true; } } else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) if (Find(x, key, out value)) return true; value = default; return false; }
}
