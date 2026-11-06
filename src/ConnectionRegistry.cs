using System.Text.Json;
namespace LocalChat;
sealed record RegistryEntry(string Id,string Name,string Description,string Version,string Url,string Source);
sealed record RegistryPage(RegistryEntry[] Entries,string Cursor);
static class ConnectionRegistry {
 public const string Root="https://registry.modelcontextprotocol.io/v0.1/servers";
 public static Uri Query(string search,string cursor){if(search.Length>120||cursor.Length>1000)throw new Exception("Слишком длинный поиск реестра");return new(Root+"?limit=30&version=latest&search="+Uri.EscapeDataString(search)+"&cursor="+Uri.EscapeDataString(cursor));}
 public static RegistryPage Parse(string json){
  using var doc=JsonDocument.Parse(json);var result=new List<RegistryEntry>();
  foreach(var item in doc.RootElement.GetProperty("servers").EnumerateArray()){
   if(item.TryGetProperty("_meta",out var meta)&&meta.TryGetProperty("io.modelcontextprotocol.registry/official",out var official)&&official.TryGetProperty("status",out var status)&&status.GetString()!="active")continue;
   var s=item.GetProperty("server");string name=s.GetProperty("name").GetString()??"",title=s.TryGetProperty("title",out var t)?t.GetString()??name:name,description=s.TryGetProperty("description",out var d)?d.GetString()??"":"",version=s.GetProperty("version").GetString()??"";string url="";
   if(s.TryGetProperty("remotes",out var remotes))foreach(var remote in remotes.EnumerateArray()){if(!remote.TryGetProperty("type",out var rt)||rt.GetString() is not ("streamable-http" or "sse")||!remote.TryGetProperty("url",out var u))continue;try{url=McpCatalog.ValidateUrl(u.GetString()??"").AbsoluteUri;break;}catch{}}
   string source="https://registry.modelcontextprotocol.io/";if(s.TryGetProperty("repository",out var repo)&&repo.TryGetProperty("url",out var ru)){try{source=McpCatalog.ValidateUrl(ru.GetString()??"").AbsoluteUri;}catch{}}
   if(name.Length>200||title.Length>200||description.Length>4000)continue;result.Add(new(name,title,description,version,url,source));
  }
  string cursor=doc.RootElement.TryGetProperty("metadata",out var m)&&m.TryGetProperty("nextCursor",out var c)?c.GetString()??"":"";return new(result.GroupBy(x=>x.Id).Select(g=>g.Last()).Take(30).ToArray(),cursor);
 }
 public static async Task<RegistryPage> Search(string search,string cursor,CancellationToken ct){using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(25)};client.DefaultRequestHeaders.UserAgent.ParseAdd("NORA/3.8");using var response=await client.GetAsync(Query(search,cursor),HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();using var stream=await response.Content.ReadAsStreamAsync(ct);using var bytes=new MemoryStream();byte[] b=new byte[8192];int n;while((n=await stream.ReadAsync(b,ct))>0){if(bytes.Length+n>2_000_000)throw new Exception("Слишком большой ответ реестра");bytes.Write(b,0,n);}return Parse(System.Text.Encoding.UTF8.GetString(bytes.ToArray()));}
}
