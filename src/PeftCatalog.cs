using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace LocalChat;
record PeftFile(string Name,long Bytes,string Sha256,string Url);
record PeftModelSpec(string Id,string Url,string Revision,int Likes,string Base,string SourceLicense,long WeightBytes,PeftFile[] Files);
static class PeftCatalog {
 public const string BaseId="Qwen/Qwen2.5-Coder-7B-Instruct";
 public static readonly PeftModelSpec[] Models=Load();
 static PeftModelSpec[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LocalChat.peft-adapters.json")??throw new Exception("Каталог PEFT отсутствует");using var doc=JsonDocument.Parse(stream);var models=doc.RootElement.GetProperty("models").Deserialize<PeftModelSpec[]>(Disk.Json)??[];if(models.Length!=16||models.Select(x=>x.Id).Distinct().Count()!=16)throw new Exception("Неверный каталог PEFT");foreach(var m in models){if(m.Likes<1||m.Base!=BaseId||m.Revision.Length!=40||m.Files.Length<2||m.Files.Any(f=>f.Name.Contains('/')||f.Name.Contains('\\')||f.Name.Contains("..")||f.Sha256.Length!=64||f.Bytes<=0||f.Url!=$"https://huggingface.co/{m.Id}/resolve/{m.Revision}/{f.Name}"))throw new Exception("Неверная запись PEFT");}return models;}
 public static PeftModelSpec Get(string id)=>Models.SingleOrDefault(x=>x.Id==id)??throw new Exception("Адаптер отсутствует в проверенном каталоге");
 public static string Cache(string root)=>Path.Combine(root,"peft-cache");
 public static string Home(string root,PeftModelSpec m)=>Path.Combine(Cache(root),"adapter-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(m.Id))).ToLowerInvariant()[..20]);
 public static bool Present(string root,PeftModelSpec m){try{return File.Exists(Path.Combine(Home(root,m),"verified.json"))&&m.Files.All(f=>new FileInfo(Path.Combine(Home(root,m),f.Name)) is {Exists:true} info&&info.Length==f.Bytes);}catch{return false;}}
 public static string Python(string root)=>Path.Combine(root,"peft-runtime","Scripts","python.exe");
}
