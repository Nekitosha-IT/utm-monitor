using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
namespace UTMMonitor;
public sealed class UtmClient:IDisposable
{
    readonly HttpClient _http=new(new SocketsHttpHandler{ConnectTimeout=TimeSpan.FromSeconds(2),PooledConnectionLifetime=TimeSpan.FromMinutes(2),MaxConnectionsPerServer=8}){Timeout=TimeSpan.FromSeconds(12)};
    static string Base(Utm u)=>$"http://{u.Host}:{u.Port}";
    public async Task<(int Code,string Text,long Ms)> GetAsync(Utm u,string path,CancellationToken ct=default){var sw=Stopwatch.StartNew();try{using var r=await _http.GetAsync(Base(u)+path,ct);return((int)r.StatusCode,await r.Content.ReadAsStringAsync(ct),sw.ElapsedMilliseconds);}catch(Exception e){return(0,e.Message,sw.ElapsedMilliseconds);}}
    public async Task<UtmStatus> StatusAsync(Utm u,CancellationToken ct=default){var x=await GetAsync(u,"/api/info/list",ct);if(x.Code==0)return new(false,0,x.Ms,null,null,null,null,x.Text,null,null);try{using var d=JsonDocument.Parse(x.Text);string? F(params string[] k){foreach(var z in k)if(Find(d.RootElement,z,out var v))return v.ToString();return null;}return new(x.Code<400,x.Code,x.Ms,F("version","utmVersion"),F("contour","contourName"),F("ownerId","fsrarId","clientId"),null,x.Code>=400?x.Text:null,null,null);}catch{return new(x.Code<400,x.Code,x.Ms,null,null,null,null,x.Text,null,null);}}
    public async Task<List<CertificateInfo>> CertificatesAsync(Utm u,CancellationToken ct=default){var a=new List<CertificateInfo>();foreach(var path in new[]{"/api/certificate/list","/api/certificate/GOST"}){var x=await GetAsync(u,path,ct);if(x.Code<200||x.Code>=400)continue;try{using var d=JsonDocument.Parse(x.Text);foreach(var v in Elements(d.RootElement)){string F(params string[] k){foreach(var z in k)if(Find(v,z,out var q))return q.ToString();return "";}var n=F("name","certificateName");if(n=="")n=path.EndsWith("GOST")?"GOST":"RSA";var exp=F("expireDate","notAfter","validTo","expirationDate");var valid=!DateTime.TryParse(exp,out var dt)||dt>=DateTime.Now;a.Add(new(n,F("subject","subjectName"),F("issuer","issuerName"),exp,valid,v.GetRawText()));}}catch{}}return a;}
    public async Task<List<DocumentRow>> DocumentsAsync(Utm u,string path,string direction,CancellationToken ct=default){var x=await GetAsync(u,path,ct);if(x.Code<200||x.Code>=400)return[];try{using var d=JsonDocument.Parse(x.Text);var a=new List<DocumentRow>();foreach(var v in Elements(d.RootElement)){string F(params string[] k){foreach(var z in k)if(Find(v,z,out var q))return q.ToString();return"";}var id=F("id","uuid","documentId","identity","guid");if(id=="")continue;a.Add(new(id,F("type","documentType","docType","name"),direction,F("number","docNumber","num"),F("date","dateTime","created","timestamp"),F("status","state"),CountItems(v),v.GetRawText()));}return a;}catch{return[];}}
    public async Task<List<DocumentRow>> OutQueueAsync(Utm u,CancellationToken ct=default)
    {
        var list=new List<DocumentRow>();
        var root=await GetAsync(u,"/opt/out",ct);
        if(root.Code<200||root.Code>=400||string.IsNullOrWhiteSpace(root.Text))return list;
        var urls=ExtractUrls(root.Text);
        using var gate=new SemaphoreSlim(6);
        var tasks=urls.Take(500).Select(async url=>{
            await gate.WaitAsync(ct);
            try{
                var path=url.StartsWith("http",StringComparison.OrdinalIgnoreCase)?new Uri(url).AbsolutePath:url;
                var x=await GetAsync(u,path,ct);
                if(x.Code<200||x.Code>=400||string.IsNullOrWhiteSpace(x.Text))return;
                var id=path;
                var type=path.Trim('/').Split('/').FirstOrDefault(s=>s.Length>0)??"opt/out";
                var number=XmlText(x.Text,"NUMBER","Number","DocNumber","WAYBILLNUMBER","NUM")??JsonText(x.Text,"number","docNumber","num");
                var date=XmlText(x.Text,"DATE","Date","dateTime","DateTime")??JsonText(x.Text,"date","dateTime","created","timestamp");
                var status=XmlText(x.Text,"Conclusion","Status","status","State")??JsonText(x.Text,"status","state");
                var items=CountXmlItems(x.Text);
                list.Add(new DocumentRow(id,type,"Входящие /opt/out",number??"",date??"",status??"",items,x.Text));
            }catch{ } finally{gate.Release();}
        });
        await Task.WhenAll(tasks);
        return list.GroupBy(x=>x.Id,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToList();
    }
    static IEnumerable<string> ExtractUrls(string text)
    {
        try{var x=XDocument.Parse(text);return x.Descendants().Where(e=>e.Name.LocalName.Equals("url",StringComparison.OrdinalIgnoreCase)).Select(e=>e.Value.Trim()).Where(v=>v.Contains("/opt/out/",StringComparison.OrdinalIgnoreCase));}
        catch{return System.Text.RegularExpressions.Regex.Matches(text,@"(?:https?://[^\s<]+)?/opt/out/[A-Za-z0-9_./-]+").Select(m=>m.Value.TrimEnd('"', '\''));}
    }
    static string? XmlText(string text,params string[] names){try{var x=XDocument.Parse(text);foreach(var n in names){var e=x.Descendants().FirstOrDefault(z=>z.Name.LocalName.Equals(n,StringComparison.OrdinalIgnoreCase));if(e!=null&&!string.IsNullOrWhiteSpace(e.Value))return e.Value.Trim();}}catch{}return null;}
    static string? JsonText(string text,params string[] names){try{using var d=JsonDocument.Parse(text);foreach(var n in names)if(Find(d.RootElement,n,out var v))return v.ToString();}catch{}return null;}
    static int CountXmlItems(string text){try{var x=XDocument.Parse(text);return x.Descendants().Count(e=>new[]{"Product","Position","ProductInfo","Item","alcCode"}.Contains(e.Name.LocalName,StringComparer.OrdinalIgnoreCase));}catch{return 0;}}
    static int CountItems(JsonElement v){foreach(var k in new[]{"items","positions","products","productItems"})if(Find(v,k,out var x)&&x.ValueKind==JsonValueKind.Array)return x.GetArrayLength();return 0;}
    public async Task<SyncResult> SyncAsync(Utm u,CancellationToken ct=default){var q=await OutQueueAsync(u,ct);var i=await DocumentsAsync(u,"/api/db/in/list","Входящие",ct);var o=await DocumentsAsync(u,"/api/db/out/list","Исходящие",ct);return new(i.Count+q.Count,o.Count,i.Count+o.Count+q.Count,"Синхронизация завершена");}
    public async Task<QueryResult> QueryBarcodeAsync(Utm u,MarkParts p,CancellationToken ct=default){var esc=System.Security.SecurityElement.Escape;var xml=$"<?xml version=\"1.0\" encoding=\"UTF-8\"?><ns:QueryBarcode xmlns:ns=\"http://fsrar.ru/WEGAIS/QueryBarcode\"><ns:Query><ns:Type>{esc(p.Type)}</ns:Type><ns:Rank>{esc(p.Rank)}</ns:Rank><ns:Number>{esc(p.Number)}</ns:Number></ns:Query></ns:QueryBarcode>";using var form=new MultipartFormDataContent();form.Add(new StringContent(xml,Encoding.UTF8,"application/xml"),"xml_file","QueryBarcode.xml");try{using var r=await _http.PostAsync(Base(u)+"/opt/in/QueryBarcode",form,ct);var t=await r.Content.ReadAsStringAsync(ct);return r.IsSuccessStatusCode?new(true,"Запрос отправлен",FindTicket(t),t):new(false,$"UTM HTTP {(int)r.StatusCode}",null,t);}catch(Exception e){return new(false,e.Message,null,"");}}
    public async Task<TicketResult> PollTicketAsync(Utm u,string ticket,CancellationToken ct=default){var paths=new[]{"/opt/out/Ticket/","/opt/out/ReplyBarcode/","/opt/out/ReplyMark/"};for(var i=0;i<20;i++){foreach(var prefix in paths){var x=await GetAsync(u,prefix+Uri.EscapeDataString(ticket),ct);if(x.Code>=200&&x.Code<400&&!string.IsNullOrWhiteSpace(x.Text))return new(true,ticket,"Получен ответ",x.Text);}await Task.Delay(1000,ct);}return new(false,ticket,"Таймаут ожидания ответа","");}
    static string? FindTicket(string s){try{var x=XDocument.Parse(s);var u=x.Descendants().FirstOrDefault(e=>e.Name.LocalName.Equals("url",StringComparison.OrdinalIgnoreCase));if(u!=null)return u.Value.Trim().Split('/').LastOrDefault(v=>!string.IsNullOrWhiteSpace(v));return x.Descendants().FirstOrDefault(e=>e.Name.LocalName.Contains("Ticket",StringComparison.OrdinalIgnoreCase))?.Value;}catch{return null;}}
    static IEnumerable<JsonElement> Elements(JsonElement e){if(e.ValueKind==JsonValueKind.Array)return e.EnumerateArray();foreach(var k in new[]{"documents","items","data","result","certificates","content"})if(e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(k,out var z))return Elements(z);return[];}
    static bool Find(JsonElement e,string key,out JsonElement value){if(e.ValueKind==JsonValueKind.Object){foreach(var p in e.EnumerateObject()){if(string.Equals(p.Name,key,StringComparison.OrdinalIgnoreCase)){value=p.Value;return true;}if(Find(p.Value,key,out value))return true;}}else if(e.ValueKind==JsonValueKind.Array){foreach(var i in e.EnumerateArray())if(Find(i,key,out value))return true;}value=default;return false;}
    public void Dispose()=>_http.Dispose();
}
