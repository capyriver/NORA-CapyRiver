using System.IO.Compression;
namespace LocalChat;
static class CudaRuntime {
 const string Release="https://github.com/ggml-org/llama.cpp/releases/download/b11669/";
 public const string EngineFile="llama-b11669-bin-win-cuda-12.4-x64.zip",LibrariesFile="cudart-llama-bin-win-cuda-12.4-x64.zip";
 public const long EngineBytes=266582698,LibrariesBytes=391443627;
 public const string EngineHash="c2da1f73ed176ae093ac5aceb978a7b167a16113f6d74c6789eeaee2135cb8dc",LibrariesHash="8c79a9b226de4b3cacfd1f83d24f962d0773be79f1e7b75c6af4ded7e32ae1d6";
 static CatalogModel Spec=new("cuda-runtime","CUDA runtime","runtimes/cuda-b11669","unused","",0,"",false,"");
 public static string Home(string root)=>Spec.Home(root);
 public static string Server(string root)=>Path.Combine(Home(root),"runtime","llama-server.exe");
 public static bool Ready(string root){ModelCatalog.SafeHome(root,Spec);string d=Path.Combine(Home(root),"runtime");return new[]{"llama-server.exe","ggml-cuda.dll","cublas64_12.dll","cublasLt64_12.dll","cudart64_12.dll","nora-verified.txt"}.All(f=>File.Exists(Path.Combine(d,f)));}
 public static async Task<string> Install(string root,Action<string> progress,CancellationToken ct){
  ModelCatalog.SafeHome(root,Spec);if(Ready(root))return Server(root);string home=Home(root);Directory.CreateDirectory(home);
  long free=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(home))!).AvailableFreeSpace;if(free<2_000_000_000)throw new IOException("Для CUDA-движка нужно ещё около 2 ГБ свободного места. Можно использовать Vulkan.");
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=true}){Timeout=Timeout.InfiniteTimeSpan};client.DefaultRequestHeaders.UserAgent.ParseAdd("NORA/3.3");
  progress("NVIDIA CUDA 12.4 · общий движок для всех моделей · 658 МБ загрузки");
  string engine=Path.Combine(home,EngineFile),libraries=Path.Combine(home,LibrariesFile);
  await DirectSetup.Download(client,new Uri(Release+EngineFile),engine,EngineBytes,EngineHash,progress,ct);
  await DirectSetup.Download(client,new Uri(Release+LibrariesFile),libraries,LibrariesBytes,LibrariesHash,progress,ct);
  ct.ThrowIfCancellationRequested();ModelCatalog.SafeHome(root,Spec);progress("Распаковка проверенного CUDA runtime…");Extract(new[]{engine,libraries},Path.Combine(home,"runtime"),ct);return Server(root);
 }
 public static void Extract(string[] archives,string destination,CancellationToken ct){
  string stage=destination+".installing";if(Directory.Exists(stage))Directory.Delete(stage,true);Directory.CreateDirectory(stage);var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
  try{foreach(var archive in archives){using var zip=ZipFile.OpenRead(archive);if(zip.Entries.Count>300)throw new IOException("Слишком много файлов CUDA.");foreach(var e in zip.Entries){ct.ThrowIfCancellationRequested();string n=e.FullName.Replace('\\','/');if(n.StartsWith('/')||n.Split('/').Any(p=>p is "." or ".."||p.Contains(':'))||((e.ExternalAttributes>>16)&0xF000)==0xA000||!names.Add(n))throw new IOException("Небезопасный путь в CUDA runtime.");if(e.Name.Length==0)continue;total=checked(total+e.Length);if(e.Length>900_000_000||total>1_700_000_000)throw new IOException("Превышен размер CUDA runtime.");string target=Path.GetFullPath(Path.Combine(stage,n));if(!target.StartsWith(Path.GetFullPath(stage)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Путь вне runtime.");Directory.CreateDirectory(Path.GetDirectoryName(target)!);e.ExtractToFile(target);}}
   foreach(string f in new[]{"llama-server.exe","ggml-cuda.dll","cublas64_12.dll","cublasLt64_12.dll","cudart64_12.dll"})if(!File.Exists(Path.Combine(stage,f)))throw new IOException("Неполный CUDA runtime: "+f);
   File.WriteAllText(Path.Combine(stage,"nora-verified.txt"),EngineHash+"\n"+LibrariesHash);ct.ThrowIfCancellationRequested();if(Directory.Exists(destination))Directory.Delete(destination,true);Directory.Move(stage,destination);
  }catch{if(Directory.Exists(stage))Directory.Delete(stage,true);throw;}
 }
}
