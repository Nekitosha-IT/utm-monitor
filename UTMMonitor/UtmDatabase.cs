using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;

namespace UTMMonitor;

public sealed class UtmDatabase
{
    private readonly string _cs;

    public UtmDatabase()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UTMMonitor");
        Directory.CreateDirectory(dir);
        _cs = $"Data Source={Path.Combine(dir, "utm-monitor.sqlite")};Cache=Shared";

        using var c = Open();
        var schema = new[]
        {
            "PRAGMA journal_mode=WAL;",
            "CREATE TABLE IF NOT EXISTS utms(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,host TEXT NOT NULL,port INTEGER NOT NULL,enabled INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);",
            "CREATE TABLE IF NOT EXISTS documents(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,external_id TEXT NOT NULL,type TEXT,direction TEXT,number TEXT,date TEXT,status TEXT,items INTEGER NOT NULL DEFAULT 0,raw TEXT,updated_at TEXT NOT NULL,UNIQUE(utm_id,external_id));",
            "CREATE INDEX IF NOT EXISTS ix_documents_utm_date ON documents(utm_id,date);",
            "CREATE TABLE IF NOT EXISTS document_items(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,document_external_id TEXT NOT NULL,item_key TEXT NOT NULL,name TEXT,quantity REAL,price REAL,mark TEXT,raw TEXT,updated_at TEXT NOT NULL,UNIQUE(utm_id,document_external_id,item_key));",
            "CREATE TABLE IF NOT EXISTS marks(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,document_external_id TEXT,raw TEXT,type TEXT,rank TEXT,number TEXT,status TEXT,response TEXT,checked_at TEXT,UNIQUE(utm_id,document_external_id,raw));",
            "CREATE TABLE IF NOT EXISTS status_history(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,checked_at TEXT NOT NULL,online INTEGER NOT NULL,http_code INTEGER NOT NULL,response_ms INTEGER NOT NULL,version TEXT,error TEXT);",
            "CREATE TABLE IF NOT EXISTS certificates(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER NOT NULL,name TEXT,subject TEXT,issuer TEXT,not_after TEXT,valid INTEGER NOT NULL,raw TEXT,updated_at TEXT NOT NULL,UNIQUE(utm_id,name,subject));",
            "CREATE TABLE IF NOT EXISTS events(id INTEGER PRIMARY KEY AUTOINCREMENT,utm_id INTEGER,created_at TEXT NOT NULL,level TEXT NOT NULL,message TEXT NOT NULL,raw TEXT);"
        };

        foreach (var sql in schema)
        {
            using var q = c.CreateCommand();
            q.CommandText = sql;
            q.ExecuteNonQuery();
        }

        using var count = c.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM utms";
        if (Convert.ToInt64(count.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
        {
            using var insert = c.CreateCommand();
            insert.CommandText = "INSERT INTO utms(name,host,port,enabled) VALUES('УТМ 1','127.0.0.1',8080,1)";
            insert.ExecuteNonQuery();
        }
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        using var q = c.CreateCommand();
        q.CommandText = "PRAGMA busy_timeout=5000";
        q.ExecuteNonQuery();
        return c;
    }

    public List<Utm> All()
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT id,name,host,port,enabled FROM utms ORDER BY id";
        using var r = q.ExecuteReader();
        var result = new List<Utm>();
        while (r.Read())
            result.Add(new Utm((int)r.GetInt64(0), r.GetString(1), r.GetString(2), (int)r.GetInt64(3), r.GetInt64(4) != 0));
        return result;
    }

    public int Add(string name, string host, int port)
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO utms(name,host,port,enabled) VALUES($n,$h,$p,1); SELECT last_insert_rowid();";
        q.Parameters.AddWithValue("$n", name);
        q.Parameters.AddWithValue("$h", host);
        q.Parameters.AddWithValue("$p", port);
        return Convert.ToInt32(q.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void Update(Utm utm, string name, string host, int port, bool enabled)
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "UPDATE utms SET name=$n,host=$h,port=$p,enabled=$e WHERE id=$id";
        q.Parameters.AddWithValue("$n", name);
        q.Parameters.AddWithValue("$h", host);
        q.Parameters.AddWithValue("$p", port);
        q.Parameters.AddWithValue("$e", enabled ? 1 : 0);
        q.Parameters.AddWithValue("$id", utm.Id);
        q.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var table in new[] { "document_items", "marks", "documents", "status_history", "certificates", "events" })
        {
            using var q = c.CreateCommand();
            q.Transaction = tx;
            q.CommandText = $"DELETE FROM {table} WHERE utm_id=$id";
            q.Parameters.AddWithValue("$id", id);
            q.ExecuteNonQuery();
        }

        using (var q = c.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText = "DELETE FROM utms WHERE id=$id";
            q.Parameters.AddWithValue("$id", id);
            q.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void SaveDocuments(int uid, IEnumerable<DocumentRow> rows)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var d in rows)
        {
            using var q = c.CreateCommand();
            q.Transaction = tx;
            q.CommandText = @"INSERT INTO documents
(utm_id,external_id,type,direction,number,date,status,items,raw,updated_at)
VALUES($u,$x,$t,$d,$n,$dt,$s,$i,$r,$now)
ON CONFLICT(utm_id,external_id) DO UPDATE SET
 type=$t,direction=$d,number=$n,date=$dt,status=$s,items=$i,raw=$r,updated_at=$now";
            q.Parameters.AddWithValue("$u", uid);
            q.Parameters.AddWithValue("$x", d.Id);
            q.Parameters.AddWithValue("$t", d.Type);
            q.Parameters.AddWithValue("$d", d.Direction);
            q.Parameters.AddWithValue("$n", d.Number);
            q.Parameters.AddWithValue("$dt", d.Date);
            q.Parameters.AddWithValue("$s", d.Status);
            q.Parameters.AddWithValue("$i", d.Items);
            q.Parameters.AddWithValue("$r", d.RawJson);
            q.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            q.ExecuteNonQuery();
            SaveNested(c, tx, uid, d);
        }
        tx.Commit();
    }

    private static void SaveNested(SqliteConnection c, SqliteTransaction tx, int uid, DocumentRow d)
    {
        try
        {
            using var doc = JsonDocument.Parse(d.RawJson);
            var root = doc.RootElement;
            foreach (var key in new[] { "items", "positions", "products", "productItems" })
            {
                if (!Find(root, key, out var array) || array.ValueKind != JsonValueKind.Array)
                    continue;

                var index = 0;
                foreach (var item in array.EnumerateArray())
                {
                    string Get(params string[] keys)
                    {
                        foreach (var k in keys)
                            if (Find(item, k, out var value))
                                return value.ToString();
                        return string.Empty;
                    }

                    var mark = Get("mark", "barcode", "exciseMark", "markCode", "ean");
                    var itemKey = Get("id", "uuid", "guid", "code", "productCode");
                    if (string.IsNullOrWhiteSpace(itemKey))
                        itemKey = index.ToString(CultureInfo.InvariantCulture);

                    using var q = c.CreateCommand();
                    q.Transaction = tx;
                    q.CommandText = @"INSERT INTO document_items
(utm_id,document_external_id,item_key,name,quantity,price,mark,raw,updated_at)
VALUES($u,$d,$k,$n,$q,$p,$m,$r,$t)
ON CONFLICT(utm_id,document_external_id,item_key) DO UPDATE SET
 name=$n,quantity=$q,price=$p,mark=$m,raw=$r,updated_at=$t";
                    q.Parameters.AddWithValue("$u", uid);
                    q.Parameters.AddWithValue("$d", d.Id);
                    q.Parameters.AddWithValue("$k", itemKey);
                    q.Parameters.AddWithValue("$n", Get("name", "productName", "fullName"));
                    q.Parameters.AddWithValue("$q", ParseNumber(Get("quantity", "qty", "amount")));
                    q.Parameters.AddWithValue("$p", ParseNumber(Get("price", "sum", "cost")));
                    q.Parameters.AddWithValue("$m", mark);
                    q.Parameters.AddWithValue("$r", item.GetRawText());
                    q.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
                    q.ExecuteNonQuery();

                    if (!string.IsNullOrWhiteSpace(mark))
                        SaveMark(c, tx, uid, d.Id, mark);
                    index++;
                }
                return;
            }
        }
        catch (JsonException)
        {
            // Raw document is still stored even when nested JSON cannot be parsed.
        }
    }

    private static void SaveMark(SqliteConnection c, SqliteTransaction tx, int uid, string documentId, string raw)
    {
        using var q = c.CreateCommand();
        q.Transaction = tx;
        q.CommandText = "INSERT OR IGNORE INTO marks(utm_id,document_external_id,raw) VALUES($u,$d,$r)";
        q.Parameters.AddWithValue("$u", uid);
        q.Parameters.AddWithValue("$d", documentId);
        q.Parameters.AddWithValue("$r", raw);
        q.ExecuteNonQuery();
    }

    private static double ParseNumber(string value)
    {
        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    public void SaveMarkResult(int uid, string? document, string raw, MarkParts parts, string status, string response)
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = @"INSERT INTO marks
(utm_id,document_external_id,raw,type,rank,number,status,response,checked_at)
VALUES($u,$d,$r,$t,$k,$n,$s,$v,$c)
ON CONFLICT(utm_id,document_external_id,raw) DO UPDATE SET
 type=$t,rank=$k,number=$n,status=$s,response=$v,checked_at=$c";
        q.Parameters.AddWithValue("$u", uid);
        q.Parameters.AddWithValue("$d", (object?)document ?? DBNull.Value);
        q.Parameters.AddWithValue("$r", raw);
        q.Parameters.AddWithValue("$t", parts.Type);
        q.Parameters.AddWithValue("$k", parts.Rank);
        q.Parameters.AddWithValue("$n", parts.Number);
        q.Parameters.AddWithValue("$s", status);
        q.Parameters.AddWithValue("$v", response);
        q.Parameters.AddWithValue("$c", DateTime.UtcNow.ToString("O"));
        q.ExecuteNonQuery();
    }

    public List<DocumentRow> Documents(int? uid = null)
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = uid.HasValue
            ? "SELECT external_id,type,direction,number,date,status,items,raw FROM documents WHERE utm_id=$u ORDER BY date DESC"
            : "SELECT external_id,type,direction,number,date,status,items,raw FROM documents ORDER BY date DESC";
        if (uid.HasValue)
            q.Parameters.AddWithValue("$u", uid.Value);

        using var r = q.ExecuteReader();
        var result = new List<DocumentRow>();
        while (r.Read())
        {
            result.Add(new DocumentRow(
                r.GetString(0),
                r.IsDBNull(1) ? "" : r.GetString(1),
                r.IsDBNull(2) ? "" : r.GetString(2),
                r.IsDBNull(3) ? "" : r.GetString(3),
                r.IsDBNull(4) ? "" : r.GetString(4),
                r.IsDBNull(5) ? "" : r.GetString(5),
                r.GetInt32(6),
                r.IsDBNull(7) ? "" : r.GetString(7)));
        }
        return result;
    }

    public void SaveStatus(int uid, UtmStatus status)
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO status_history(utm_id,checked_at,online,http_code,response_ms,version,error) VALUES($u,$t,$o,$h,$m,$v,$e)";
        q.Parameters.AddWithValue("$u", uid);
        q.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
        q.Parameters.AddWithValue("$o", status.Online ? 1 : 0);
        q.Parameters.AddWithValue("$h", status.HttpCode);
        q.Parameters.AddWithValue("$m", status.ResponseMs);
        q.Parameters.AddWithValue("$v", status.Version ?? "");
        q.Parameters.AddWithValue("$e", status.Error ?? "");
        q.ExecuteNonQuery();
    }

    public void SaveCertificates(int uid, IEnumerable<CertificateInfo> certificates)
    {
        using var c = Open();
        foreach (var certificate in certificates)
        {
            using var q = c.CreateCommand();
            q.CommandText = @"INSERT INTO certificates
(utm_id,name,subject,issuer,not_after,valid,raw,updated_at)
VALUES($u,$n,$s,$i,$d,$v,$r,$t)
ON CONFLICT(utm_id,name,subject) DO UPDATE SET
 issuer=$i,not_after=$d,valid=$v,raw=$r,updated_at=$t";
            q.Parameters.AddWithValue("$u", uid);
            q.Parameters.AddWithValue("$n", certificate.Name);
            q.Parameters.AddWithValue("$s", certificate.Subject ?? "");
            q.Parameters.AddWithValue("$i", certificate.Issuer ?? "");
            q.Parameters.AddWithValue("$d", certificate.NotAfter ?? "");
            q.Parameters.AddWithValue("$v", certificate.Valid ? 1 : 0);
            q.Parameters.AddWithValue("$r", certificate.RawJson);
            q.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
            q.ExecuteNonQuery();
        }
    }

    public void Event(int? uid, string level, string message, string raw = "")
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO events(utm_id,created_at,level,message,raw) VALUES($u,$t,$l,$m,$r)";
        q.Parameters.AddWithValue("$u", (object?)uid ?? DBNull.Value);
        q.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
        q.Parameters.AddWithValue("$l", level);
        q.Parameters.AddWithValue("$m", message);
        q.Parameters.AddWithValue("$r", raw);
        q.ExecuteNonQuery();
    }

    private static bool Find(JsonElement element, string key, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
                if (Find(property.Value, key, out value))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (Find(item, key, out value))
                    return true;
        }

        value = default;
        return false;
    }
}
