using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace LocalChat;
partial class MainWindow {
 object[] PeftEntries()=>PeftCatalog.Models.Select(m=>(object)new{m.Id,m.Url,m.Likes,m.SourceLicense,m.WeightBytes,present=PeftCatalog.Present(root,m),active=settings.Mode==3&&settings.PeftAdapterId==m.Id}).ToArray();
 string PeftFiles(){string folder=Path.Combine(root,"peft-worker-3.10");Directory.CreateDirectory(folder);foreach(var name in new[]{"peft_backend.py","peft-adapters.json","peft-base.json"})File.WriteAllText(Path.Combine(folder,name),Resource(name),new UTF8Encoding(false));return folder;}
 string PeftPython=>PeftCatalog.Python(root);
 void OpenPeftLibrary(){State();Post("openPeft");}
 async Task PeftProcess(string exe,List<string> args,CancellationToken token,bool allowModelDownload=false){
  using var process=Launch(exe,args,allowModelDownload);using var stop=token.Register(()=>{try{process.Kill(true);}catch{}});var errors=Drain(process.StandardError);var tail=new StringBuilder();
  while(await process.StandardOutput.ReadLineAsync(token) is string line){string text=line;try{using var doc=JsonDocument.Parse(line);var r=doc.RootElement;if(r.TryGetProperty("text",out var t))text=t.GetString()??line;}catch(JsonException){}tail.AppendLine(text);if(tail.Length>6000)tail.Remove(0,tail.Length-4000);Post("status",text);Post("setupProgress",text);}
  await process.WaitForExitAsync(token);string stderr=await errors;if(process.ExitCode!=0)throw new Exception(tail.ToString()+"\n"+stderr);
 }
 async Task PeftJob(Func<string,CancellationToken,Task> run,string label){
  RequireIdle();if(importing)throw new Exception("Дождись импорта");var previous=CloneSettings();bool committed=false;settingUp=true;setupCancel=new();var token=setupCancel.Token;StopAir();StopServer();Post("busy",true);Post("setupModel",label);Post("setupBusy",true);
  try{string folder=PeftFiles();await run(folder,token);token.ThrowIfCancellationRequested();SaveSettings();committed=true;Post("toast","Готово. Запуск выбранных 7B-весов на твоём ПК ещё нужно проверить.");}
  catch(OperationCanceledException){Post("status","Остановлено. Старые настройки сохранены; загрузку можно продолжить.");}
  catch(Exception error){Post("toast","PEFT: "+error.Message);Post("status","PEFT не подключён · прежняя модель сохранена");}
  finally{if(!committed){settings=previous;try{SaveSettings();}catch{}}settingUp=false;setupCancel.Dispose();setupCancel=null;if(!closing){State();Post("setupBusy",false);Post("busy",false);Post("openPeft");}}
 }
 async Task SetupPeft(){
  RequireIdle();string python=settings.Python;
  if(!File.Exists(python)){using var picker=new OpenFileDialog{Filter="Python|python.exe",Title="Выбери Python 3.10–3.13 x64 (не pythonw.exe)"};if(picker.ShowDialog(this)!=DialogResult.OK)return;python=picker.FileName;}
  await PeftJob(async(folder,token)=>{
   Post("setupProgress","Создаю отдельную среду. Веса и AirLLM не меняются. Загружаются официальные Python-пакеты…");
   await PeftProcess(python,new(){"-m","venv",Path.Combine(root,"peft-runtime")},token);
   await PeftProcess(PeftPython,new(){"-m","pip","install","--no-input","--disable-pip-version-check","--only-binary=:all:","--index-url","https://download.pytorch.org/whl/cu128","torch==2.8.0"},token);
   await PeftProcess(PeftPython,new(){"-m","pip","install","--no-input","--disable-pip-version-check","--only-binary=:all:","--index-url","https://pypi.org/simple","transformers==4.56.2","peft==0.17.1","accelerate==1.10.1","safetensors==0.6.2","huggingface_hub==0.34.4","bitsandbytes==0.47.0"},token);
   await PeftProcess(PeftPython,new(){"-u",Path.Combine(folder,"peft_backend.py"),folder,PeftCatalog.Cache(root),"check"},token);
  },"PEFT · установка среды CUDA");
 }
 async Task DownloadPeft(string? id,bool choose){
  RequireIdle();if(id!=null)PeftCatalog.Get(id);
  if(!File.Exists(PeftPython))throw new Exception("Сначала установи PEFT/CUDA в библиотеке LoRA. Нужен Python 3.10–3.13 x64.");
  await PeftJob(async(folder,token)=>{
   var args=new List<string>{"-u",Path.Combine(folder,"peft_backend.py"),folder,PeftCatalog.Cache(root),id==null?"all":choose?"prepare":"adapter"};if(id!=null)args.Add(id);
   await PeftProcess(PeftPython,args,token);
   if(choose){settings.Mode=3;settings.PeftAdapterId=id!;settings.ContextTokens=2048;settings.MaxTokens=512;settings.Reasoning=false;settings.ResponseMode=0;settings.ExperimentalProfileId="";}
  },choose?"Qwen2.5-Coder 7B + LoRA · установка":id==null?"16 LoRA · только адаптеры":"LoRA · загрузка адаптера");
 }
 async Task<string> PeftReply(List<ChatMessage> messages,Action<string> chunk,CancellationToken token,int? maximum,bool fit){
  var spec=PeftCatalog.Get(settings.PeftAdapterId);if(!File.Exists(PeftPython))throw new Exception("Установи PEFT/CUDA в библиотеке LoRA");
  string key="peft|"+spec.Id;if(air is not{HasExited:false}||airKey!=key){StopAir();string folder=PeftFiles();air=Launch(PeftPython,new(){"-u",Path.Combine(folder,"peft_backend.py"),folder,PeftCatalog.Cache(root),"serve"});airErrors=Drain(air.StandardError);airKey=key;}
  var process=air!;var request=JsonSerializer.Serialize(new{adapter_id=spec.Id,messages,context_tokens=settings.ContextTokens,max_tokens=maximum??settings.MaxTokens,temperature=settings.Temperature,fit_output=fit});await process.StandardInput.WriteLineAsync(request.AsMemory(),token);await process.StandardInput.FlushAsync(token);
  while(true){string? line=await process.StandardOutput.ReadLineAsync(token);if(line==null)throw new Exception("PEFT завершился. "+(airErrors==null?"":await airErrors));JsonDocument doc;try{doc=JsonDocument.Parse(line);}catch(JsonException){continue;}using(doc){var r=doc.RootElement;string type=r.GetProperty("type").GetString()??"",text=r.GetProperty("text").GetString()??"";if(type=="token")chunk(text);else if(type=="status")Post("status",text);else if(type=="error")throw new Exception(text);else if(type=="done"){if(r.TryGetProperty("limit_reached",out var limited)&&limited.GetBoolean()){var last=current.Messages.LastOrDefault(m=>m.Role=="assistant");if(last!=null)last.LimitReached=true;}if(text.Length==0)throw new Exception("Адаптер не вернул текст");return text;}}}
 }
}
