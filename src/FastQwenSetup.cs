namespace LocalChat;
static class FastQwenSetup {
 public const string Source="https://huggingface.co/Qwen/Qwen3-8B-GGUF";
 public const string ModelUrl=Source+"/resolve/7c41481f57cb95916b40956ab2f0b139b296d974/Qwen3-8B-Q4_K_M.gguf?download=true";
 public const long ModelSize=5027783488;
 public const string ModelHash="d98cdcbd03e17ce47681435b5150e34c1417f50b5c0019dd560e4882c5745785";
 public static async Task<DirectSetup.Result> Install(string root,Action<string> progress,CancellationToken ct){
  string home=Path.Combine(root,"qwen-fast-2.3");Directory.CreateDirectory(home);string model=Path.Combine(home,"Qwen3-8B-Q4_K_M.gguf");
  if(!File.Exists(model)){long free=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(home))!).AvailableFreeSpace;long partial=File.Exists(model+".part")?new FileInfo(model+".part").Length:0;if(free<ModelSize-Math.Min(ModelSize,partial)+350_000_000)throw new Exception("Для GGUF Qwen3-8B нужно около 6 ГБ свободного диска.");}
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=true}){Timeout=Timeout.InfiniteTimeSpan};client.DefaultRequestHeaders.UserAgent.ParseAdd("NORA/2.3");string vk=Path.Combine(home,"vulkan.zip"),cpu=Path.Combine(home,"cpu.zip");
  progress("Загрузка официального llama.cpp Vulkan · 32 МБ");await DirectSetup.Download(client,new Uri("https://github.com/ggml-org/llama.cpp/releases/download/b11669/llama-b11669-bin-win-vulkan-x64.zip"),vk,32182350,DirectSetup.VulkanHash,progress,ct);
  progress("Загрузка резервного CPU-движка · 20 МБ");await DirectSetup.Download(client,new Uri("https://github.com/ggml-org/llama.cpp/releases/download/b11669/llama-b11669-bin-win-cpu-x64.zip"),cpu,19537886,DirectSetup.CpuHash,progress,ct);
  string vkDir=Path.Combine(home,"runtime-vulkan"),cpuDir=Path.Combine(home,"runtime-cpu");DirectSetup.Extract(vk,vkDir);DirectSetup.Extract(cpu,cpuDir);
  progress("Загрузка официального Qwen3-8B Q4_K_M · 5,03 ГБ");await DirectSetup.Download(client,new Uri(ModelUrl),model,ModelSize,ModelHash,progress,ct);return new(Path.Combine(vkDir,"llama-server.exe"),Path.Combine(cpuDir,"llama-server.exe"),model);
 }
}
static class QwenRuntimeArguments {
 public static List<string> Build(Settings s,int layers){
  var args=new List<string>{"-m",s.GgufModel,"-c",Math.Clamp(s.ContextTokens,512,24576).ToString(),"-np","1","--host","127.0.0.1","--port","12345","--alias","local-model"};
  if(s.FastQwenMode){
   // Leave GPU layers UNSET for llama.cpp's memory-fit logic; reserve 1024 MiB.
   if(layers==0)args.AddRange(new[]{"-ngl","0"});
   args.AddRange(new[]{"--fit","on","--fit-target","1024","--fit-ctx",Math.Clamp(s.ContextTokens,512,24576).ToString(),"-b","256","-ub","128","--chat-template-kwargs",s.Reasoning?"{\"enable_thinking\":true}":"{\"enable_thinking\":false}","--reasoning-budget",s.Reasoning?"48":"0"});
  }else args.AddRange(new[]{"-ngl",layers.ToString()});
  return args;
 }
}
