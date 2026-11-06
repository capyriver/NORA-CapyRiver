using System.Text.Json;
namespace LocalChat;
record AirFile(string Name,long Bytes,string Sha256);
record AirSpec(string Id,string Name,string Repo,string Revision,bool Thinking,AirFile[] Files){
 public long Bytes=>Files.Sum(f=>f.Bytes);
 public CatalogModel Owner=>new(Id,Name,"air-models/"+Id,"config.json","",0,"",Thinking,"");
 public string Home(string root)=>Owner.Home(root);
}
static class AirCatalog {
 public static AirSpec[] Models {get;}=Load();
 static AirSpec[] Load(){using var s=typeof(AirCatalog).Assembly.GetManifestResourceStream("LocalChat.air-models.json")??throw new Exception("Нет каталога AirLLM");return JsonSerializer.Deserialize<AirSpec[]>(s,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;}
 public static AirSpec Get(string id)=>Models.FirstOrDefault(x=>x.Id==id)??throw new ArgumentException("Нет модели AirLLM");
 public static void Safe(string root,AirSpec s)=>ModelCatalog.SafeHome(root,s.Owner);
 public static bool Downloaded(string root,AirSpec s){Safe(root,s);return s.Files.All(f=>File.Exists(Path.Combine(s.Home(root),f.Name))&&new FileInfo(Path.Combine(s.Home(root),f.Name)).Length==f.Bytes)&&File.Exists(Path.Combine(s.Home(root),"nora-verified.json"));}
 public static AirSpec? Active(string root,int mode,string path)=>mode==0?Models.FirstOrDefault(s=>ModelCatalog.SamePath(path,s.Home(root))):null;
 public static CatalogEntry[] Entries(string root,int mode,string path)=>Models.Select(s=>{try{bool ok=Downloaded(root,s),files=Directory.Exists(s.Home(root))&&Directory.EnumerateFileSystemEntries(s.Home(root)).Any();return new CatalogEntry(s.Id,s.Name+" · AirLLM",(s.Thinking?"Чат и код":"Для кода · без thinking")+$" · слои на диске · до {s.Bytes*2.15/1e9+3:F0} ГБ места · экспериментально",s.Bytes,s.Thinking,ok,mode==0&&ModelCatalog.SamePath(path,s.Home(root)),files,files&&!ok,"");}catch(Exception e){return new CatalogEntry(s.Id,s.Name,"AirLLM",s.Bytes,s.Thinking,false,false,false,false,e.Message);}}).ToArray();
 public static bool HasFiles(string root)=>Entries(root,-1,"").Any(e=>e.HasFiles);
 public static async Task Install(string root,AirSpec s,Action<string> progress,CancellationToken ct,HttpClient? transport=null,bool checkSpace=true){
  Safe(root,s);ct.ThrowIfCancellationRequested();string home=s.Home(root);Directory.CreateDirectory(home);
  // Preserve original weights; splitting can add another full copy. Include headroom, not just download size.
  long present=s.Files.Sum(f=>{string p=Path.Combine(home,f.Name);return File.Exists(p)?Math.Min(f.Bytes,new FileInfo(p).Length):File.Exists(p+".part")?Math.Min(f.Bytes,new FileInfo(p+".part").Length):0;});
  long cache=Directory.Exists(Path.Combine(home,"splitted_model"))?Directory.EnumerateFiles(Path.Combine(home,"splitted_model"),"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length):0;
  long needed=checked(s.Bytes-present+Math.Max(0,(long)(s.Bytes*1.15)-cache)+3_000_000_000);
  if(checkSpace&&new DriveInfo(Path.GetPathRoot(Path.GetFullPath(home))!).AvailableFreeSpace<needed)throw new IOException($"Недостаточно места для весов и слоёв. Нужно ещё примерно {needed/1e9:F1} ГБ.");
  using var own=transport==null?new HttpClient{Timeout=Timeout.InfiniteTimeSpan}:null;var client=transport??own!;
  foreach(var f in s.Files){ct.ThrowIfCancellationRequested();Safe(root,s);progress(s.Name+" · "+f.Name);await DirectSetup.Download(client,new Uri($"https://huggingface.co/{s.Repo}/resolve/{s.Revision}/{f.Name}?download=true"),Path.Combine(home,f.Name),f.Bytes,f.Sha256,progress,ct);}
  ct.ThrowIfCancellationRequested();Safe(root,s);File.WriteAllText(Path.Combine(home,"nora-verified.json"),JsonSerializer.Serialize(new{s.Repo,s.Revision,s.Files}));
 }
 public static bool IsManagedReady(string root,string path){var s=Active(root,0,path);return s!=null&&Downloaded(root,s);}
 public static void Apply(Settings settings,string path,bool thinking){settings.Mode=0;settings.ModelPath=path;settings.ContextTokens=2048;settings.MaxTokens=1024;if(!thinking){settings.Reasoning=false;settings.ResponseMode=0;}}
}
