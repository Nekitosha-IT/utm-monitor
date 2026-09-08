using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace UTMMonitor;

public sealed class UtmClient : IDisposable
{
    readonly HttpClient _http = new(new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(2),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 8
    }) { Timeout = TimeSpan.FromSeconds(15) };

    static string Base(Utm u) => $"http://{u.Host}:{u.Port}";

    public async Task<(int Code, string Text, long Ms)> GetAsync(Utm u, string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var r = await _http.GetAsync(Base(u) + path, ct);
            return ((int)r.StatusCode, await r.Content.ReadAsStringAsync(ct), sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            return (0, e.Message, sw.ElapsedMilliseconds);
        }
    }

    public async Task<UtmStatus> StatusAsync(Utm u, CancellationToken ct = default)
    {
        var x = await GetAsync(u, "/api/info/list", ct);
        if (x.Code == 0) return new(false, 0, x.Ms, null, null, null, null, x.Text, null, null);
        try
        {
            using var d = JsonDocument.Parse(x.Text);
            string? F(params string[] k) => FirstJson(d.RootElement, k);
            return new(x.Code < 400, x.Code, x.Ms,
                F("version", "utmVersion", "versionUtm"),
                F("contour", "contourName", "contur"),
                F("ownerId", "fsrarId", "clientId", "FSRAR_ID", "fsrar_id"),
                BoolJson(d.RootElement, "license", "licensed"),
                x.Code >= 400 ? x.Text : null, null, null);
        }
        catch
        {
            return new(x.Code < 400, x.Code, x.Ms, null, null, null, null, x.Code >= 400 ? x.Text : null, null, null);
        }
    }

    public async Task<List<CertificateInfo>> CertificatesAsync(Utm u, CancellationToken ct = default)
    {
        var a = new List<CertificateInfo>();
        foreach (var path in new[] { "/api/certificate/list", "/api/certificate/GOST" })
        {
            var x = await GetAsync(u, path, ct);
            if (x.Code < 200 || x.Code >= 400 || string.IsNullOrWhiteSpace(x.Text)) continue;
            try
            {
                using var d = JsonDocument.Parse(x.Text);
                foreach (var v in Elements(d.RootElement))
                {
                    var n = FirstJson(v, "name", "certificateName", "type") ?? (path.EndsWith("GOST", StringComparison.OrdinalIgnoreCase) ? "GOST" : "RSA");
                    var exp = FirstJson(v, "expireDate", "notAfter", "validTo", "expirationDate", "validUntil");
                    var valid = !DateTime.TryParse(exp, out var dt) || dt >= DateTime.Now;
                    a.Add(new(n, FirstJson(v, "subject", "subjectName"), FirstJson(v, "issuer", "issuerName"), exp, valid, v.GetRawText()));
                }
            }
            catch { }
        }
        return a.GroupBy(x => $"{x.Name}|{x.Subject}", StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
    }

    public async Task<List<DocumentRow>> DocumentsAsync(Utm u, string path, string direction, CancellationToken ct = default)
    {
        var x = await GetAsync(u, path, ct);
        if (x.Code < 200 || x.Code >= 400 || string.IsNullOrWhiteSpace(x.Text)) return [];
        var a = new List<DocumentRow>();
        try
        {
            using var d = JsonDocument.Parse(x.Text);
            foreach (var v in Elements(d.RootElement))
            {
                var id = FirstJson(v, "id", "uuid", "documentId", "identity", "guid", "url");
                if (string.IsNullOrWhiteSpace(id)) continue;
                a.Add(new(id!, FirstJson(v, "type", "documentType", "docType", "name") ?? "", direction,
                    FirstJson(v, "number", "docNumber", "num", "waybillNumber") ?? "",
                    FirstJson(v, "date", "dateTime", "created", "timestamp") ?? "",
                    FirstJson(v, "status", "state") ?? "", CountItems(v), v.GetRawText()));
            }
            return a.GroupBy(z => z.Id, StringComparer.OrdinalIgnoreCase).Select(z => z.First()).ToList();
        }
        catch (JsonException)
        {
            foreach (var doc in XmlDocumentCandidates(x.Text))
            {
                var id = XmlValue(doc, "Identity", "DocId", "DocumentId", "GUID", "id", "Url") ?? Guid.NewGuid().ToString("N");
                a.Add(new(id, XmlValue(doc, "Type", "DocumentType", "DocType", "name") ?? "", direction,
                    XmlValue(doc, "NUMBER", "Number", "DocNumber", "WAYBILLNUMBER", "NUM") ?? "",
                    XmlValue(doc, "DATE", "Date", "DateTime", "created", "timestamp") ?? "",
                    XmlValue(doc, "Conclusion", "Status", "status", "State") ?? "", CountXmlItems(doc.ToString(SaveOptions.DisableFormatting)), doc.ToString(SaveOptions.DisableFormatting)));
            }
            return a.GroupBy(z => z.Id, StringComparer.OrdinalIgnoreCase).Select(z => z.First()).ToList();
        }
    }

    public async Task<List<DocumentRow>> OutQueueAsync(Utm u, CancellationToken ct = default)
    {
        var list = new List<DocumentRow>();
        var root = await GetAsync(u, "/opt/out", ct);
        if (root.Code < 200 || root.Code >= 400 || string.IsNullOrWhiteSpace(root.Text)) return list;
        var urls = ExtractUrls(root.Text).Distinct(StringComparer.OrdinalIgnoreCase).Take(1000).ToList();
        using var gate = new SemaphoreSlim(6);
        var tasks = urls.Select(async url =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var path = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new Uri(url).AbsolutePath : url;
                var x = await GetAsync(u, path, ct);
                if (x.Code < 200 || x.Code >= 400 || string.IsNullOrWhiteSpace(x.Text)) return;
                var number = XmlValue(x.Text, "NUMBER", "Number", "DocNumber", "WAYBILLNUMBER", "NUM") ?? JsonText(x.Text, "number", "docNumber", "num", "waybillNumber");
                var date = XmlValue(x.Text, "DATE", "Date", "dateTime", "DateTime") ?? JsonText(x.Text, "date", "dateTime", "created", "timestamp");
                var status = XmlValue(x.Text, "Conclusion", "Status", "status", "State") ?? JsonText(x.Text, "status", "state");
                var type = XmlValue(x.Text, "DocumentType", "DocType", "Type", "Document") ?? JsonText(x.Text, "type", "documentType", "docType", "name") ?? path.Trim('/').Split('/').FirstOrDefault(s => s.Length > 0) ?? "opt/out";
                var items = CountXmlItems(x.Text);
                if (items == 0) items = CountJsonItems(x.Text);
                var id = path;
                lock (list) list.Add(new DocumentRow(id, type, "Очередь /opt/out", number ?? "", date ?? "", status ?? "", items, x.Text));
            }
            catch { }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
        return list.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
    }

    static IEnumerable<string> ExtractUrls(string text)
    {
        try
        {
            var x = XDocument.Parse(text);
            return x.Descendants().Where(e => e.Name.LocalName.Equals("url", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value.Trim()).Where(v => v.Contains("/opt/out/", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return Regex.Matches(text, @"(?:https?://[^\s<]+)?/opt/out/[A-Za-z0-9_./-]+")
                .Select(m => m.Value.TrimEnd('"', '\''));
        }
    }

    static IEnumerable<XElement> XmlDocumentCandidates(string text)
    {
        try
        {
            var root = XDocument.Parse(text).Root;
            if (root == null) return [];
            var named = root.Descendants().Where(e => new[] { "Document", "TTN", "WayBill", "Waybill", "Reply", "DocumentResponse" }.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase)).ToList();
            return named.Count > 0 ? named : new[] { root };
        }
        catch { return []; }
    }

    static string? XmlValue(string text, params string[] names)
    {
        try { return XmlValue(XDocument.Parse(text).Root!, names); } catch { return null; }
    }
    static string? XmlValue(XElement root, params string[] names)
    {
        foreach (var n in names)
        {
            var attr = root.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(n, StringComparison.OrdinalIgnoreCase));
            if (attr != null && !string.IsNullOrWhiteSpace(attr.Value)) return attr.Value.Trim();
            var e = root.DescendantsAndSelf().FirstOrDefault(z => z.Name.LocalName.Equals(n, StringComparison.OrdinalIgnoreCase));
            if (e != null && !string.IsNullOrWhiteSpace(e.Value)) return e.Value.Trim();
        }
        return null;
    }

    static int CountXmlItems(string text)
    {
        try
        {
            var root = XDocument.Parse(text).Root;
            if (root == null) return 0;
            var candidates = root.Descendants().Where(e => new[] { "Product", "Position", "ProductItem", "Item" }.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase)).ToList();
            return candidates.Count;
        }
        catch { return 0; }
    }

    static string? JsonText(string text, params string[] names)
    {
        try { using var d = JsonDocument.Parse(text); return FirstJson(d.RootElement, names); } catch { return null; }
    }

    static int CountJsonItems(string text)
    {
        try { using var d = JsonDocument.Parse(text); return CountItems(d.RootElement); } catch { return 0; }
    }

    static int CountItems(JsonElement v)
    {
        foreach (var k in new[] { "items", "positions", "products", "productItems", "product" })
            if (Find(v, k, out var x) && x.ValueKind == JsonValueKind.Array) return x.GetArrayLength();
        return 0;
    }

    public async Task<SyncResult> SyncAsync(Utm u, CancellationToken ct = default)
    {
        var q = await OutQueueAsync(u, ct);
        var i = await DocumentsAsync(u, "/api/db/in/list", "Входящие", ct);
        var o = await DocumentsAsync(u, "/api/db/out/list", "Исходящие", ct);
        return new(i.Count + q.Count, o.Count, i.Count + o.Count + q.Count, "Синхронизация завершена");
    }

    public async Task<QueryResult> QueryBarcodeAsync(Utm u, MarkParts p, CancellationToken ct = default)
    {
        var esc = System.Security.SecurityElement.Escape;
        var xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><ns:QueryBarcode xmlns:ns=\"http://fsrar.ru/WEGAIS/QueryBarcode\"><ns:Query><ns:Type>{esc(p.Type)}</ns:Type><ns:Rank>{esc(p.Rank)}</ns:Rank><ns:Number>{esc(p.Number)}</ns:Number></ns:Query></ns:QueryBarcode>";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(xml, Encoding.UTF8, "application/xml"), "xml_file", "QueryBarcode.xml");
        try
        {
            using var r = await _http.PostAsync(Base(u) + "/opt/in/QueryBarcode", form, ct);
            var t = await r.Content.ReadAsStringAsync(ct);
            return r.IsSuccessStatusCode ? new(true, "Запрос отправлен", FindTicket(t), t) : new(false, $"UTM HTTP {(int)r.StatusCode}", null, t);
        }
        catch (Exception e) { return new(false, e.Message, null, ""); }
    }

    public async Task<TicketResult> PollTicketAsync(Utm u, string ticket, CancellationToken ct = default)
    {
        var clean = ticket.Trim();
        if (clean.Contains('/')) clean = clean.Split('/').LastOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? clean;
        var paths = new[] { "/opt/out/Ticket/", "/opt/out/ReplyBarcode/", "/opt/out/ReplyMark/" };
        for (var i = 0; i < 30; i++)
        {
            foreach (var prefix in paths)
            {
                var x = await GetAsync(u, prefix + Uri.EscapeDataString(clean), ct);
                if (x.Code >= 200 && x.Code < 400 && !string.IsNullOrWhiteSpace(x.Text)) return new(true, clean, "Получен ответ", x.Text);
            }
            await Task.Delay(1000, ct);
        }
        return new(false, clean, "Таймаут ожидания ответа", "");
    }

    static string? FindTicket(string s)
    {
        try
        {
            var x = XDocument.Parse(s);
            var u = x.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("url", StringComparison.OrdinalIgnoreCase));
            if (u != null) return u.Value.Trim().Split('/').LastOrDefault(v => !string.IsNullOrWhiteSpace(v));
            return x.Descendants().FirstOrDefault(e => e.Name.LocalName.Contains("Ticket", StringComparison.OrdinalIgnoreCase))?.Value.Trim();
        }
        catch
        {
            return JsonText(s, "ticket", "ticketId", "id");
        }
    }

    static IEnumerable<JsonElement> Elements(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Array) return e.EnumerateArray();
        foreach (var k in new[] { "documents", "items", "data", "result", "certificates", "content", "rows" })
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var z)) return Elements(z);
        return e.ValueKind == JsonValueKind.Object ? new[] { e } : [];
    }

    static string? FirstJson(JsonElement e, params string[] keys)
    {
        foreach (var key in keys) if (Find(e, key, out var value)) return value.ToString();
        return null;
    }

    static bool? BoolJson(JsonElement e, params string[] keys)
    {
        foreach (var key in keys) if (Find(e, key, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            if (bool.TryParse(v.ToString(), out var b)) return b;
        }
        return null;
    }

    static bool Find(JsonElement e, string key, out JsonElement value)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
                if (Find(p.Value, key, out value)) return true;
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var i in e.EnumerateArray()) if (Find(i, key, out value)) return true;
        }
        value = default;
        return false;
    }

    public void Dispose() => _http.Dispose();
}
