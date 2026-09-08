using Microsoft.Data.Sqlite;

namespace UTMMonitor;

public sealed class UtmStore
{
    private readonly string _db;
    public UtmStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UTMMonitor");
        Directory.CreateDirectory(dir); _db = Path.Combine(dir, "utm-monitor.sqlite"); Init();
    }
    private SqliteConnection Open() { var c = new SqliteConnection($"Data Source={_db};Mode=ReadWriteCreate;Cache=Shared"); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="PRAGMA journal_mode=WAL; PRAGMA busy_timeout=3000;"; cmd.ExecuteNonQuery(); return c; }
    private void Init() { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="CREATE TABLE IF NOT EXISTS utms(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,host TEXT NOT NULL,port INTEGER NOT NULL,enabled INTEGER NOT NULL DEFAULT 1);"; cmd.ExecuteNonQuery(); }
    public List<Utm> All() { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT id,name,host,port,enabled FROM utms ORDER BY id"; using var r=cmd.ExecuteReader(); var x=new List<Utm>(); while(r.Read()) x.Add(new((int)r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt32(3),r.GetInt32(4)!=0)); return x; }
    public int Add(string name,string host,int port) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO utms(name,host,port) VALUES($n,$h,$p); SELECT last_insert_rowid();"; cmd.Parameters.AddWithValue("$n",name);cmd.Parameters.AddWithValue("$h",host);cmd.Parameters.AddWithValue("$p",port);return Convert.ToInt32(cmd.ExecuteScalar()); }
    public void Delete(int id) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM utms WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery(); }
    public void Update(int id,string name,string host,int port,bool enabled) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="UPDATE utms SET name=$n,host=$h,port=$p,enabled=$e WHERE id=$id"; foreach(var p in new[]{("$n",(object)name),("$h",host),("$p",port),("$e",enabled?1:0),("$id",id)})cmd.Parameters.AddWithValue(p.Item1,p.Item2);cmd.ExecuteNonQuery(); }
}
