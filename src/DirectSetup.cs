using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
namespace LocalChat;
// Pinned public artifacts. No arbitrary URL, script execution or global installation.
static class DirectSetup {
 public const string ModelSource="https://huggingface.co/prithivMLmods/Qwen3-4B-2507-abliterated-GGUF";
 public const string ModelUrl=ModelSource+"/resolve/39bd5e36db343a5c5c14b64f01e3a14245fe882f/Qwen3-4B-Instruct-2507-abliterated-GGUF/Qwen3-4B-Instruct-2507-abliterated.Q4_K_M.gguf?download=true";
 public const string ModelHash="528639d25ec2ebd81f68dbc508f4dbdb2f9fa314cddbcf0c3512e34281ecd7a8";
 public const long ModelSize=2497280256;
 public const string DirectPrompt="Ты локальный помощник NORA. Отвечай на русском прямо, содержательно и по существу. Подстраивай подробность и стиль под запрос. Отделяй проверенные факты от предположений. Не заявляй о выполнении действий, для которых у тебя нет инструментов.";
 public const string VulkanHash="2c07574e7bde71321cb785282b20cc821e951132149eeceeea32ddebdbf0598e";
 public const string CpuHash="37b3efcf962847ca869745fd4ccf7f5e7e8dacfeff9fa3523b4ca8cdc66d632b";
 const string Release="https://github.com/ggml-org/llama.cpp/releases/download/b11669/";
 public record Result(string Server,string CpuServer,string Model);
 public static async Task<Result> Install(string root,Action<string> progress,CancellationToken ct){
  string home=Path.Combine(root,"direct-2.1");Directory.CreateDirectory(home);
  string model=Path.Combine(home,"Qwen3-4B-Instruct-2507-abliterated.Q4_K_M.gguf");
  if(!File.Exists(model)){long free=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(home))!).AvailableFreeSpace;long partial=File.Exists(model+".part")?new FileInfo(model+".part").Length:0;if(free<ModelSize-Math.Min(ModelSize,partial)+350_000_000)throw new Exception("Для модели и движка недостаточно места. Освободи примерно 3 ГБ на системном диске.");}
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=true}){Timeout=Timeout.InfiniteTimeSpan};client.DefaultRequestHeaders.UserAgent.ParseAdd("NORA/2.1");
  string vk=Path.Combine(home,"vulkan.zip"),cpu=Path.Combine(home,"cpu.zip");
  progress("Загрузка официального llama.cpp Vulkan · 32 МБ");await Download(client,new Uri(Release+"llama-b11669-bin-win-vulkan-x64.zip"),vk,32182350,VulkanHash,progress,ct);
  progress("Загрузка резервного CPU-движка · 20 МБ");await Download(client,new Uri(Release+"llama-b11669-bin-win-cpu-x64.zip"),cpu,19537886,CpuHash,progress,ct);
  progress("Подготовка движка…");string vkDir=Path.Combine(home,"runtime-vulkan"),cpuDir=Path.Combine(home,"runtime-cpu");Extract(vk,vkDir);Extract(cpu,cpuDir);
  progress("Загрузка Qwen3-4B Instruct abliterated Q4_K_M · 2,50 ГБ");await Download(client,new Uri(ModelUrl),model,ModelSize,ModelHash,progress,ct);
  return new(Path.Combine(vkDir,"llama-server.exe"),Path.Combine(cpuDir,"llama-server.exe"),model);
 }
 public static async Task<bool> Verify(string path,long size,string sha,CancellationToken ct){if(!File.Exists(path)||new FileInfo(path).Length!=size)return false;await using var f=File.OpenRead(path);return Convert.ToHexString(await SHA256.HashDataAsync(f,ct)).Equals(sha,StringComparison.OrdinalIgnoreCase);}
 public static async Task Download(HttpClient client,Uri url,string path,long size,string sha,Action<string> progress,CancellationToken ct){
  if(await Verify(path,size,sha,ct)){progress("Файл уже загружен · SHA-256 верна");return;}
  string part=path+".part";Directory.CreateDirectory(Path.GetDirectoryName(path)!);long offset=File.Exists(part)?new FileInfo(part).Length:0;
  if(offset>size){File.Delete(part);offset=0;}
  if(offset<size){
   using var req=new HttpRequestMessage(HttpMethod.Get,url);if(offset>0)req.Headers.Range=new RangeHeaderValue(offset,null);
   using var response=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
   if(response.StatusCode==HttpStatusCode.PartialContent){if(response.Content.Headers.ContentRange?.From!=offset||response.Content.Headers.ContentRange?.Length!=size)throw new Exception("Сервер вернул неверный диапазон файла.");}else offset=0;
   await using var stream=await response.Content.ReadAsStreamAsync(ct);await using var file=new FileStream(part,offset>0?FileMode.Append:FileMode.Create,FileAccess.Write,FileShare.None,131072,true);
   byte[] buffer=new byte[131072];long written=offset;var last=DateTime.MinValue;int read;
   while((read=await stream.ReadAsync(buffer,ct))>0){if(written+read>size)throw new Exception("Файл превышает ожидаемый размер; загрузка остановлена.");await file.WriteAsync(buffer.AsMemory(0,read),ct);written+=read;if((DateTime.UtcNow-last).TotalMilliseconds>500){progress($"Загрузка: {written*100.0/size:F1}% · {written/1_000_000:N0} / {size/1_000_000:N0} МБ");last=DateTime.UtcNow;}}
   await file.FlushAsync(ct);if(written!=size)throw new Exception("Загрузка прервалась. Повтори установку: скачивание продолжится с сохранённой части.");
  }
  progress("Проверка SHA-256…");if(!await Verify(part,size,sha,ct)){File.Delete(part);throw new Exception("SHA-256 не совпала. Файл не установлен. Повтори загрузку.");}
  File.Move(part,path,true);
 }
 public static void Extract(string archive,string destination){
  string staging=destination+".installing";if(Directory.Exists(staging))Directory.Delete(staging,true);Directory.CreateDirectory(staging);
  try{using var z=ZipFile.OpenRead(archive);if(z.Entries.Count>300)throw new Exception("Слишком много файлов в runtime.");long total=0;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var e in z.Entries){string name=e.FullName.Replace('\\','/');if(name.StartsWith('/')||name.Split('/').Any(x=>x is "." or ".."||x.Contains(':'))||((e.ExternalAttributes>>16)&0xF000)==0xA000||!names.Add(name))throw new Exception("Небезопасный путь в runtime.");if(e.Name.Length==0)continue;total+=e.Length;if(total>350_000_000||e.Length>200_000_000)throw new Exception("Слишком большой runtime.");string target=Path.GetFullPath(Path.Combine(staging,name));if(!target.StartsWith(Path.GetFullPath(staging)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Путь вне runtime.");Directory.CreateDirectory(Path.GetDirectoryName(target)!);e.ExtractToFile(target);}
   if(!File.Exists(Path.Combine(staging,"llama-server.exe")))throw new Exception("В архиве нет llama-server.exe.");if(Directory.Exists(destination))Directory.Delete(destination,true);Directory.Move(staging,destination);
  }catch{if(Directory.Exists(staging))Directory.Delete(staging,true);throw;}
 }
}
