using System.Diagnostics;
using System.Text.Json;

namespace UTMMonitor;

public sealed class UtmClient
{
    private readonly HttpClient _http = new(){Timeout=TimeSpan.FromSeconds(3)};
    public async Task<UtmStatus> StatusAsync(Utm u, CancellationToken ct=default)
    {
        var sw=Stopwatch.StartNew(); try { using var r=await _http.GetAsync($"http://{u.Host}:{u.Port}/api/info/list",ct); var text=await r.Content.ReadAsStringAsync(ct); sw.Stop();
            if(!r.IsSuccessStatusCode)return new(false,(int)r.StatusCode,sw.ElapsedMilliseconds,null,null,null,null,$"HTTP {(int)r.StatusCode}",null,null);
            using var d=JsonDocument.Parse(text); var root=d.RootElement; string? version=Find(root,"version","Version"); string? contour=Find(root,"contour","Contour"); string? owner=Find(root,"ownerId","owner_id","OwnerId"); bool? license=FindBool(root,"license","License");
            return new(true,(int)r.StatusCode,sw.ElapsedMilliseconds,version,contour,owner,license,null,null,null);
        } catch(OperationCanceledException){return new(false,0,sw.ElapsedMilliseconds,null,null,null,null,"Таймаут",null,null);} catch(Exception ex){return new(false,0,sw.ElapsedMilliseconds,null,null,null,null,ex.Message,null,null);}
    }
    private static string? Find(JsonElement e,params string[] names){if(e.ValueKind==JsonValueKind.Object)foreach(var p in e.EnumerateObject()){if(names.Any(n=>string.Equals(n,p.Name,StringComparison.OrdinalIgnoreCase))&&p.Value.ValueKind==JsonValueKind.String)return p.Value.GetString();var x=Find(p.Value,names);if(x!=null)return x;}if(e.ValueKind==JsonValueKind.Array)foreach(var x in e.EnumerateArray()){var v=Find(x,names);if(v!=null)return v;}return null;}
    private static bool? FindBool(JsonElement e,params string[] names){if(e.ValueKind==JsonValueKind.Object)foreach(var p in e.EnumerateObject()){if(names.Any(n=>string.Equals(n,p.Name,StringComparison.OrdinalIgnoreCase))){if(p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)return p.Value.GetBoolean();}var x=FindBool(p.Value,names);if(x.HasValue)return x;}if(e.ValueKind==JsonValueKind.Array)foreach(var x in e.EnumerateArray()){var v=FindBool(x,names);if(v.HasValue)return v;}return null;}
}
