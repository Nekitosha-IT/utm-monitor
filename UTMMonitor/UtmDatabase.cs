using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace UTMMonitor;

public sealed class UtmDatabase
{
    private readonly string _cs;
    public string DataDirectory { get; }

    public UtmDatabase()
    {
        DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UTMMonitor");
        Directory.CreateDirectory(DataDirectory);
        _cs = $"Data Source={Path.Combine(DataDirectory, "utm-monitor.sqlite")};Cache=Shared";
        using var c = Open();
        foreach (var sql in new[]
        {
            "PRAGMA journal_mode=WAL;",
            "CREATE TABLE IF NOT EXISTS utms(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,host TEXT NOT NULL,port INTEGER NOT NULL,enabled INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);",
            "CREATE TABLE IF NOT EXISTS documents(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,external_id TEXT NOT NULL,type TEXT,direction TEXT,number TEXT,date TEXT,status TEXT,items INTEGER NOT NULL DEFAULT 0,raw TEXT,updated_at TEXT NOT NULL,UNIQUE(utm_id,external_id));",
            "CREATE INDEX IF NOT EXISTS ix_documents_utm_date ON documents(utm_id,date);",
            "CREATE INDEX IF NOT EXISTS ix_documents_number ON documents(number);",
            "CREATE TABLE IF NOT EXISTS document_items(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,document_external_id TEXT NOT NULL,item_key TEXT NOT NULL,name TEXT,quantity REAL,price REAL,mark TEXT,raw TEXT,updated_at TEXT NOT NULL,UNIQUE(utm_id,document_external_id,item_key));",
            "CREATE INDEX IF NOT EXISTS ix_items_mark ON document_items(mark);",
            "CREATE TABLE IF NOT EXISTS marks(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,document_external_id TEXT,raw TEXT,type TEXT,rank TEXT,number TEXT,status TEXT,response TEXT,checked_at TEXT,UNIQUE(utm_id,document_external_id,raw));",
            "CREATE INDEX IF NOT EXISTS ix_marks_number ON marks(number);",
            "CREATE TABLE IF NOT EXISTS status_history(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,checked_at TEXT NOT NULL,online INTEGER NOT NULL,http_code INTEGER NOT NULL,response_ms INTEGER NOT NULL,version TEXT,error TEXT);",
            "CREATE TABLE IF NOT EXISTS certificates(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,name TEXT,subject TEXT,issuer TEXT,not_after TEXT,valid INTEGER NOT NULL,raw TEXT,updated_at TEXT NOT NULL,UNIQUE(utm_id,name,subject));",
            "CREATE TABLE IF NOT EXISTS events(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER,created_at TEXT NOT NULL,level TEXT NOT NULL,message TEXT NOT NULL,raw TEXT);",
            "CREATE INDEX IF NOT EXISTS ix_events_utm_time ON events(utm_id,created_at);"
        }) { using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
        using var count = c.CreateCommand(); count.CommandText = "SELECT COUNT(*) FROM utms";
        if (Convert.ToInt64(count.ExecuteScalar(), CultureInfo.InvariantCulture) == 0) { using var q = c.CreateCommand(); q.CommandText = "INSERT INTO utms(name,host,port,enabled) VALUES('УТМ 1','127.0.0.1',8080,1)"; q.ExecuteNonQuery(); }
    }

    private SqliteConnection Open() { var c = new SqliteConnection(_cs); c.Open(); using var q = c.CreateCommand(); q.CommandText = "PRAGMA busy_timeout=5000"; q.ExecuteNonQuery(); return c; }
    public List<Utm> All() { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT id,name,host,port,enabled FROM utms ORDER BY id"; using var r = q.ExecuteReader(); var a = new List<Utm>(); while (r.Read()) a.Add(new((int)r.GetInt64(0), r.GetString(1), r.GetString(2), (int)r.GetInt64(3), r.GetInt64(4) != 0)); return a; }
    public int Add(string name, string host, int port) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "INSERT INTO utms(name,host,port,enabled) VALUES($n,$h,$p,1);SELECT last_insert_rowid();"; q.Parameters.AddWithValue("$n", name); q.Parameters.AddWithValue("$h", host); q.Parameters.AddWithValue("$p", port); return Convert.ToInt32(q.ExecuteScalar(), CultureInfo.InvariantCulture); }
    public void Update(Utm u, string name, string host, int port, bool enabled) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "UPDATE utms SET name=$n,host=$h,port=$p,enabled=$e WHERE id=$id"; q.Parameters.AddWithValue("$n", name); q.Parameters.AddWithValue("$h", host); q.Parameters.AddWithValue("$p", port); q.Parameters.AddWithValue("$e", enabled ? 1 : 0); q.Parameters.AddWithValue("$id", u.Id); q.ExecuteNonQuery(); }

    public void Delete(int id)
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        foreach (var t in new[] { "document_items", "marks", "documents", "status_history", "certificates", "events" }) { using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = $"DELETE FROM {t} WHERE utm_id=$id"; q.Parameters.AddWithValue("$id", id); q.ExecuteNonQuery(); }
        using (var q = c.CreateCommand()) { q.Transaction = tx; q.CommandText = "DELETE FROM utms WHERE id=$id"; q.Parameters.AddWithValue("$id", id); q.ExecuteNonQuery(); } tx.Commit();
    }

    public void SaveDocuments(int uid, IEnumerable<DocumentRow> rows)
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        foreach (var d in rows)
        {
            using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = @"INSERT INTO documents(utm_id,external_id,type,direction,number,date,status,items,raw,updated_at) VALUES($u,$x,$t,$d,$n,$dt,$s,$i,$r,$now)
ON CONFLICT(utm_id,external_id) DO UPDATE SET type=$t,direction=$d,number=$n,date=$dt,status=$s,items=$i,raw=$r,updated_at=$now";
            q.Parameters.AddWithValue("$u", uid); q.Parameters.AddWithValue("$x", d.Id); q.Parameters.AddWithValue("$t", d.Type); q.Parameters.AddWithValue("$d", d.Direction); q.Parameters.AddWithValue("$n", d.Number); q.Parameters.AddWithValue("$dt", d.Date); q.Parameters.AddWithValue("$s", d.Status); q.Parameters.AddWithValue("$i", d.Items); q.Parameters.AddWithValue("$r", d.RawJson); q.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O")); q.ExecuteNonQuery(); SaveNested(c, tx, uid, d);
        }
        tx.Commit();
    }

    static void SaveNested(SqliteConnection c, SqliteTransaction tx, int uid, DocumentRow d)
    {
        try
        {
            using var doc = JsonDocument.Parse(d.RawJson);
            foreach (var key in new[] { "items", "positions", "products", "productItems", "product" })
            {
                if (!Find(doc.RootElement, key, out var ar) || ar.ValueKind != JsonValueKind.Array) continue;
                var i = 0; foreach (var item in ar.EnumerateArray()) SaveItem(c, tx, uid, d.Id, item, i++); return;
            }
        }
        catch (JsonException) { }
        try
        {
            var root = XDocument.Parse(d.RawJson).Root; if (root == null) return;
            var items = root.Descendants().Where(e => new[] { "Product", "Position", "ProductItem", "Item" }.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase)).ToList();
            if (items.Count == 0) { var codes = root.Descendants().Where(e => e.Name.LocalName.Equals("alcCode", StringComparison.OrdinalIgnoreCase)).ToList(); items = codes.Select(x => x.Parent ?? x).Distinct().ToList(); }
            for (var i = 0; i < items.Count; i++) SaveXmlItem(c, tx, uid, d.Id, items[i], i);
        }
        catch { }
    }

    static void SaveItem(SqliteConnection c, SqliteTransaction tx, int uid, string doc, JsonElement item, int index)
    {
        string Get(params string[] ks) { foreach (var k in ks) if (Find(item, k, out var v)) return v.ToString(); return ""; }
        var mark = Get("mark", "barcode", "exciseMark", "markCode", "ean", "barcodeDataMatrix");
        SaveItemValues(c, tx, uid, doc, index.ToString(CultureInfo.InvariantCulture), Get("name", "productName", "fullName", "shortName", "goodsName", "alcoholName"), ParseNumber(Get("quantity", "qty", "amount", "count")), ParseNumber(Get("price", "sum", "cost")), mark, item.GetRawText());
    }

    static void SaveXmlItem(SqliteConnection c, SqliteTransaction tx, int uid, string doc, XElement item, int index)
    {
        string Get(params string[] names) => XmlValue(item, names) ?? "";
        var key = Get("id", "uuid", "guid", "code", "productCode", "alcCode"); if (key == "") key = index.ToString(CultureInfo.InvariantCulture);
        var mark = Get("mark", "barcode", "exciseMark", "markCode", "ean", "barcodeDataMatrix", "amc");
        SaveItemValues(c, tx, uid, doc, key, Get("name", "productName", "fullName", "shortName", "goodsName", "alcoholName"), ParseNumber(Get("quantity", "qty", "amount", "count")), ParseNumber(Get("price", "sum", "cost", "priceWithVat")), mark, item.ToString(SaveOptions.DisableFormatting));
    }

    static void SaveItemValues(SqliteConnection c, SqliteTransaction tx, int uid, string doc, string key, string name, double quantity, double price, string mark, string raw)
    {
        using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = @"INSERT INTO document_items(utm_id,document_external_id,item_key,name,quantity,price,mark,raw,updated_at) VALUES($u,$d,$k,$n,$q,$p,$m,$r,$t)
ON CONFLICT(utm_id,document_external_id,item_key) DO UPDATE SET name=$n,quantity=$q,price=$p,mark=$m,raw=$r,updated_at=$t";
        q.Parameters.AddWithValue("$u", uid); q.Parameters.AddWithValue("$d", doc); q.Parameters.AddWithValue("$k", key); q.Parameters.AddWithValue("$n", name); q.Parameters.AddWithValue("$q", quantity); q.Parameters.AddWithValue("$p", price); q.Parameters.AddWithValue("$m", mark); q.Parameters.AddWithValue("$r", raw); q.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O")); q.ExecuteNonQuery(); if (mark != "") SaveMark(c, tx, uid, doc, mark);
    }

    static void SaveMark(SqliteConnection c, SqliteTransaction tx, int uid, string doc, string raw)
    { using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = "INSERT OR IGNORE INTO marks(utm_id,document_external_id,raw) VALUES($u,$d,$r)"; q.Parameters.AddWithValue("$u", uid); q.Parameters.AddWithValue("$d", doc); q.Parameters.AddWithValue("$r", raw); q.ExecuteNonQuery(); }
    static string? XmlValue(XElement root, params string[] names) { foreach (var n in names) { var a = root.Attributes().FirstOrDefault(x => x.Name.LocalName.Equals(n, StringComparison.OrdinalIgnoreCase)); if (a != null && !string.IsNullOrWhiteSpace(a.Value)) return a.Value.Trim(); var e = root.DescendantsAndSelf().FirstOrDefault(x => x.Name.LocalName.Equals(n, StringComparison.OrdinalIgnoreCase)); if (e != null && !string.IsNullOrWhiteSpace(e.Value)) return e.Value.Trim(); } return null; }
    static double ParseNumber(string v) => double.TryParse(v.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;

    public void SaveMarkResult(int uid, string? document, string raw, MarkParts p, string status, string response)
    {
        var docId = document ?? "";
        using var c = Open(); using var q = c.CreateCommand(); q.CommandText = @"INSERT INTO marks(utm_id,document_external_id,raw,type,rank,number,status,response,checked_at) VALUES($u,$d,$r,$t,$k,$n,$s,$v,$c)
ON CONFLICT(utm_id,document_external_id,raw) DO UPDATE SET type=$t,rank=$k,number=$n,status=$s,response=$v,checked_at=$c";
        q.Parameters.AddWithValue("$u", uid); q.Parameters.AddWithValue("$d", docId); q.Parameters.AddWithValue("$r", raw); q.Parameters.AddWithValue("$t", p.Type); q.Parameters.AddWithValue("$k", p.Rank); q.Parameters.AddWithValue("$n", p.Number); q.Parameters.AddWithValue("$s", status); q.Parameters.AddWithValue("$v", response); q.Parameters.AddWithValue("$c", DateTime.UtcNow.ToString("O")); q.ExecuteNonQuery();
    }

    public List<DocumentRow> Documents(int? uid = null) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = uid.HasValue ? "SELECT external_id,type,direction,number,date,status,items,raw FROM documents WHERE utm_id=$u ORDER BY date DESC" : "SELECT external_id,type,direction,number,date,status,items,raw FROM documents ORDER BY date DESC"; if (uid.HasValue) q.Parameters.AddWithValue("$u", uid.Value); using var r = q.ExecuteReader(); var a = new List<DocumentRow>(); while (r.Read()) a.Add(new(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2), r.IsDBNull(3) ? "" : r.GetString(3), r.IsDBNull(4) ? "" : r.GetString(4), r.IsDBNull(5) ? "" : r.GetString(5), r.GetInt32(6), r.IsDBNull(7) ? "" : r.GetString(7))); return a; }
    public List<MarkRow> Marks(int? uid = null) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = uid.HasValue ? "SELECT id,COALESCE(document_external_id,''),COALESCE(raw,''),COALESCE(type,''),COALESCE(rank,''),COALESCE(number,''),COALESCE(status,''),COALESCE(checked_at,''),COALESCE(response,'') FROM marks WHERE utm_id=$u ORDER BY id DESC" : "SELECT id,COALESCE(document_external_id,''),COALESCE(raw,''),COALESCE(type,''),COALESCE(rank,''),COALESCE(number,''),COALESCE(status,''),COALESCE(checked_at,''),COALESCE(response,'') FROM marks ORDER BY id DESC"; if (uid.HasValue) q.Parameters.AddWithValue("$u", uid.Value); using var r = q.ExecuteReader(); var a = new List<MarkRow>(); while (r.Read()) a.Add(new(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8))); return a; }
    public List<HistoryRow> History(int uid, int limit = 200) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT checked_at,online,http_code,response_ms,version,error FROM status_history WHERE utm_id=$u ORDER BY id DESC LIMIT $l"; q.Parameters.AddWithValue("$u", uid); q.Parameters.AddWithValue("$l", limit); using var r = q.ExecuteReader(); var a = new List<HistoryRow>(); while (r.Read()) a.Add(new(r.GetString(0), r.GetInt32(1) != 0, r.GetInt32(2), r.GetInt64(3), r.IsDBNull(4) ? "" : r.GetString(4), r.IsDBNull(5) ? "" : r.GetString(5))); return a; }
    public List<EventRow> Events(int? uid = null, int limit = 500) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = uid.HasValue ? "SELECT created_at,level,message,raw FROM events WHERE utm_id=$u ORDER BY id DESC LIMIT $l" : "SELECT created_at,level,message,raw FROM events ORDER BY id DESC LIMIT $l"; if (uid.HasValue) q.Parameters.AddWithValue("$u", uid.Value); q.Parameters.AddWithValue("$l", limit); using var r = q.ExecuteReader(); var a = new List<EventRow>(); while (r.Read()) a.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? "" : r.GetString(3))); return a; }
    public List<CertificateInfo> Certificates(int uid) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT name,subject,issuer,not_after,valid,raw FROM certificates WHERE utm_id=$u ORDER BY name,subject"; q.Parameters.AddWithValue("$u", uid); using var r = q.ExecuteReader(); var a = new List<CertificateInfo>(); while (r.Read()) a.Add(new(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2), r.IsDBNull(3) ? "" : r.GetString(3), r.GetInt32(4) != 0, r.IsDBNull(5) ? "" : r.GetString(5))); return a; }
    public void SaveStatus(int uid, UtmStatus s) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "INSERT INTO status_history(utm_id,checked_at,online,http_code,response_ms,version,error) VALUES($u,$t,$o,$h,$m,$v,$e)"; q.Parameters.AddWithValue("$u", uid); q.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O")); q.Parameters.AddWithValue("$o", s.Online ? 1 : 0); q.Parameters.AddWithValue("$h", s.HttpCode); q.Parameters.AddWithValue("$m", s.ResponseMs); q.Parameters.AddWithValue("$v", s.Version ?? ""); q.Parameters.AddWithValue("$e", s.Error ?? ""); q.ExecuteNonQuery(); }
    public void SaveCertificates(int uid, IEnumerable<CertificateInfo> a) { using var c = Open(); foreach (var x in a) { using var q = c.CreateCommand(); q.CommandText = @"INSERT INTO certificates(utm_id,name,subject,issuer,not_after,valid,raw,updated_at) VALUES($u,$n,$s,$i,$d,$v,$r,$t) ON CONFLICT(utm_id,name,subject) DO UPDATE SET issuer=$i,not_after=$d,valid=$v,raw=$r,updated_at=$t"; q.Parameters.AddWithValue("$u", uid); q.Parameters.AddWithValue("$n", x.Name); q.Parameters.AddWithValue("$s", x.Subject ?? ""); q.Parameters.AddWithValue("$i", x.Issuer ?? ""); q.Parameters.AddWithValue("$d", x.NotAfter ?? ""); q.Parameters.AddWithValue("$v", x.Valid ? 1 : 0); q.Parameters.AddWithValue("$r", x.RawJson); q.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O")); q.ExecuteNonQuery(); } }
    public void Event(int? uid, string level, string message, string raw = "") { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "INSERT INTO events(utm_id,created_at,level,message,raw) VALUES($u,$t,$l,$m,$r)"; q.Parameters.AddWithValue("$u", (object?)uid ?? DBNull.Value); q.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O")); q.Parameters.AddWithValue("$l", level); q.Parameters.AddWithValue("$m", message); q.Parameters.AddWithValue("$r", raw); q.ExecuteNonQuery(); }

    static bool Find(JsonElement e, string key, out JsonElement value) { if (e.ValueKind == JsonValueKind.Object) { foreach (var p in e.EnumerateObject()) { if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; } if (Find(p.Value, key, out value)) return true; } } else if (e.ValueKind == JsonValueKind.Array) foreach (var i in e.EnumerateArray()) if (Find(i, key, out value)) return true; value = default; return false; }
}
