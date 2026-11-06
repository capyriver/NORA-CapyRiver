using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace LocalChat;
static class GitHubImport {
 public static (string owner,string repo) Parse(string text){
  if(!Uri.TryCreate(text.Trim().TrimEnd('/'),UriKind.Absolute,out var u)||u.Scheme!="https"||u.Host!="github.com"||u.UserInfo!=""||!u.IsDefaultPort||u.Query!=""||u.Fragment!="")throw new ArgumentException("Вставь ссылку вида https://github.com/owner/repo на публичный репозиторий.");
  var parts=u.AbsolutePath.Trim('/').Split('/');if(parts.Length!=2||parts.Any(x=>!Regex.IsMatch(x,"^[A-Za-z0-9_.-]+$")||x is "." or ".."))throw new ArgumentException("Нужна ссылка на корень репозитория: https://github.com/owner/repo, без /tree или /blob.");
  return(parts[0],parts[1].EndsWith(".git")?parts[1][..^4]:parts[1]);
 }
 static HttpClient Client(){var c=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(3)};c.DefaultRequestHeaders.UserAgent.ParseAdd("LocalChat/2.0");return c;}
 public static async Task<Repository> Download(string url,CancellationToken ct){
  var(owner,repo)=Parse(url);using var c=Client();using var metadata=await c.GetAsync($"https://api.github.com/repos/{owner}/{repo}",ct);
  if(!metadata.IsSuccessStatusCode)throw new Exception($"GitHub HTTP {(int)metadata.StatusCode}. Нужен публичный репозиторий; также возможен лимит запросов GitHub.");
  using var doc=JsonDocument.Parse(await metadata.Content.ReadAsStringAsync(ct));var branch=doc.RootElement.GetProperty("default_branch").GetString()??"main";
  using var response=await c.GetAsync($"https://codeload.github.com/{owner}/{repo}/zip/refs/heads/{Uri.EscapeDataString(branch)}",HttpCompletionOption.ResponseHeadersRead,ct);
  response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>20*1024*1024)throw new Exception("Архив больше 20 МБ. Выбери меньший репозиторий.");
  using var input=await response.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();var chunk=new byte[32768];int n;
  while((n=await input.ReadAsync(chunk,ct))>0){if(buffer.Length+n>20*1024*1024)throw new Exception("Архив больше 20 МБ.");await buffer.WriteAsync(chunk.AsMemory(0,n),ct);}
  buffer.Position=0;var result=ReadZip(buffer);result.Name=$"{owner}/{repo}";result.Url=$"https://github.com/{owner}/{repo}";result.Branch=branch;return result;
 }
 static readonly HashSet<string> Extensions=new(StringComparer.OrdinalIgnoreCase){".md",".txt",".py",".cs",".js",".jsx",".ts",".tsx",".json",".yaml",".yml",".toml",".rs",".go",".java",".cpp",".c",".h",".html",".css",".sql",".sh",".ps1"};
 public static Repository ReadZip(Stream stream){
  using var zip=new ZipArchive(stream,ZipArchiveMode.Read,true);if(zip.Entries.Count>30000)throw new Exception("Слишком много файлов в архиве.");var result=new Repository();long total=0;
  foreach(var e in zip.Entries.OrderByDescending(e=>e.Name.Equals("AGENTS.md",StringComparison.OrdinalIgnoreCase)).ThenByDescending(e=>e.Name.StartsWith("README",StringComparison.OrdinalIgnoreCase)).ThenByDescending(e=>new[]{"design-ui-designer.md","engineering-frontend-developer.md","security-ai-generated-code-auditor.md"}.Contains(e.Name,StringComparer.OrdinalIgnoreCase))){
   if(result.Files.Count>=400||total>2_000_000)break;
   var parts=e.FullName.Replace('\\','/').Split('/');if(parts.Length<2||parts.Any(p=>p is "." or ".."||p.Contains(':')||p.Contains('\0'))||e.FullName.StartsWith('/'))continue;
   string path=string.Join('/',parts.Skip(1));if(path.Length==0||e.Length==0||e.Length>60000)continue;
   if(parts.Any(p=>new[]{"node_modules",".git",".venv","venv","vendor","dist","build","__pycache__"}.Contains(p,StringComparer.OrdinalIgnoreCase)))continue;
   if(!Extensions.Contains(System.IO.Path.GetExtension(e.Name))||e.Name.StartsWith('.')||e.Name.Contains("lock",StringComparison.OrdinalIgnoreCase)||e.Name.Contains("secret",StringComparison.OrdinalIgnoreCase))continue;
   using var input=e.Open();using var bytes=new MemoryStream();byte[] buf=new byte[4096];int n;bool oversized=false;while((n=input.Read(buf))>0){if(bytes.Length+n>60000){oversized=true;break;}bytes.Write(buf,0,n);}if(oversized)continue;
   string text;try{text=new UTF8Encoding(false,true).GetString(bytes.ToArray());}catch{continue;}
   if(text.Contains('\0'))continue;total+=text.Length;if(total>2_000_000)break;result.Files.Add(new(){Path=path,Text=text});
  }
  if(result.Files.Count==0)throw new Exception("В архиве не найдено подходящих текстовых файлов.");return result;
 }
 public static (string text,string[] paths) Context(Repository repo,string question,int maxChars){
  var terms=Regex.Matches(question.ToLowerInvariant(),@"[\p{L}\p{N}_]{3,}").Select(x=>x.Value).Distinct().Take(30).ToArray();
  int Score(RepoFile f)=>terms.Sum(t=>(f.Path.Contains(t,StringComparison.OrdinalIgnoreCase)?8:0)+(f.Text.Contains(t,StringComparison.OrdinalIgnoreCase)?2:0))+(f.Path.Contains("README",StringComparison.OrdinalIgnoreCase)?1:0);
  var chosen=repo.Files.OrderByDescending(Score).ThenBy(f=>f.Path.Length).Take(3).ToList();var sb=new StringBuilder();var paths=new List<string>();
  foreach(var f in chosen){int left=maxChars-sb.Length;if(left<100)break;int pos=terms.Select(t=>f.Text.IndexOf(t,StringComparison.OrdinalIgnoreCase)).Where(x=>x>=0).DefaultIfEmpty(0).Min();int start=Math.Max(0,pos-120);int take=Math.Min(Math.Min(left-60,maxChars/2),f.Text.Length-start);if(take<=0)continue;sb.AppendLine($"FILE: {f.Path}\n{f.Text.Substring(start,take)}\n");paths.Add(f.Path);}
  return(sb.ToString(),paths.ToArray());
 }
}
