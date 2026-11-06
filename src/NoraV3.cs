using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LocalChat;

partial class MainWindow {
 const string NoraV3Revision="851bf6e806efd8d0a36b00ddf55e13ccb7b8cd0a";
 const string NoraV3Sha256="80120cb91816658e67bc3cbbd7f0d751d217923109cf50f99c656083f00a1139";
 string NoraV3Home=>Path.Combine(root,"nora-v3");
 string NoraV3Python=>Path.Combine(root,"nora-v3-runtime","Scripts","python.exe");
 string NoraV3Script=>Path.Combine(NoraV3Home,"nora_v3_backend.py");
 string NoraV3Adapter=>Path.Combine(NoraV3Home,"adapter");
 bool NoraV3Ready()=>File.Exists(NoraV3Python)
  &&File.Exists(Path.Combine(NoraV3Adapter,"adapter_model.safetensors"))
  &&File.Exists(Path.Combine(NoraV3Home,"base","config.json"))
  &&File.Exists(Path.Combine(NoraV3Home,"base",".nora-v3-base-complete"))
  &&File.ReadAllText(Path.Combine(NoraV3Home,"base",".nora-v3-base-complete")).Trim()==NoraV3Revision
  &&Directory.EnumerateFiles(Path.Combine(NoraV3Home,"base"),"model*.safetensors").Any();

 static string FileHash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();}
 void PrepareNoraV3Assets(){
  Directory.CreateDirectory(NoraV3Adapter);
  foreach(var name in new[]{"adapter_config.json","adapter_model.safetensors"}){
   var destination=Path.Combine(NoraV3Adapter,name);
   using var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("LocalChat.nora-v3."+name)
    ??throw new Exception("Не найден ресурс оригинальной NORA v3: "+name);
   // A malformed existing adapter is NOT silently overwritten; verify and stop.
   if(File.Exists(destination)){
    if(name=="adapter_model.safetensors"&&FileHash(destination)!=NoraV3Sha256)
     throw new Exception("Адаптер NORA v3 был изменён. Убери повреждённый файл вручную (резервную копию сохрани), затем повтори установку.");
    continue;
   }
   var temporary=destination+".tmp";
   try{using(var output=File.Create(temporary))source.CopyTo(output);File.Move(temporary,destination);}
   finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
  if(FileHash(Path.Combine(NoraV3Adapter,"adapter_model.safetensors"))!=NoraV3Sha256)
   throw new Exception("Не совпал SHA256 встроенного адаптера NORA v3");
  File.WriteAllText(NoraV3Script,Resource("nora_v3_backend.py"),new UTF8Encoding(false));
 }
 async Task ChooseNoraV3(){
  RequireIdle();if(importing)throw new Exception("Дождись импорта репозитория.");
  if(!NoraV3Ready()){
   if(!await ConfirmAsync("CapyRiver Core 4B · установка",
      "Подключить ОРИГИНАЛЬНУ NORA AI v3 (Qwen3.5-4B + проверенный LoRA)?\n\n"
     +"Базовая модель: около 9,4 ГБ (Hugging Face); дополнительно Python/CUDA-библиотеки — несколько ГБ. "
     +"Понадобятся интернет и много свободного места на диске. Видеопамять 8 ГБ — режим NF4 4-bit. "
     +"Скорость и запуск на твоём ноутбуке пока не проверены.\n\n"
     +"Модель будет скачана в %LOCALAPPDATA%\\NORA\\nora-v3; исходный адаптер встроен в приложение. "
     +"Чаты и старые модели сохранятся. Продолжить?"))return;
   string python=settings.Python;
   if(!File.Exists(python)){
    using var picker=new OpenFileDialog{Filter="Python|python.exe",Title="Выбери Python 3.10–3.13 x64 (не pythonw.exe)"};
    if(picker.ShowDialog(this)!=DialogResult.OK)return;
    python=picker.FileName;
   }
   var previous=CloneSettings();bool committed=false;settingUp=true;setupCancel=new();
   var token=setupCancel.Token;Post("busy",true);Post("setupModel","CapyRiver Core 4B · Qwen3.5 NF4");Post("setupBusy",true);
   StopAir();StopServer();
   try{
    PrepareNoraV3Assets();
    if(!File.Exists(NoraV3Python)){
     Post("setupProgress","Создаю отдельную среду Python для NORA v3...");
     await PeftProcess(python,new(){"-m","venv",Path.Combine(root,"nora-v3-runtime")},token);
    }
    await PeftProcess(NoraV3Python,new(){"-m","pip","install","--no-input","--disable-pip-version-check","--only-binary=:all:","--index-url","https://download.pytorch.org/whl/cu128","torch==2.8.0"},token);
    await PeftProcess(NoraV3Python,new(){"-m","pip","install","--no-input","--disable-pip-version-check","--only-binary=:all:","transformers==5.2.0","peft==0.21.2","accelerate","bitsandbytes==0.47.0","huggingface_hub","safetensors"},token);
    await PeftProcess(NoraV3Python,new(){"-u",NoraV3Script,NoraV3Home,"check"},token);
    // Only this explicit, user-approved process can access Hugging Face.
    await PeftProcess(NoraV3Python,new(){"-u",NoraV3Script,NoraV3Home,"prepare"},token,true);
    token.ThrowIfCancellationRequested();
    if(!NoraV3Ready())throw new Exception("Файлы NORA v3 неполные. Установку можно повторить.");
    SelectNoraV3();committed=true;
    Post("status","NORA v3 установлена. Первая генерация загрузит модель в VRAM.");
    Post("toast","NORA v3 4B подключена. Инференс пока не проверен на твоём GPU.");
   }catch(OperationCanceledException){Post("status","Установка остановлена. Прежняя модель и чаты сохранены.");}
    catch(Exception ex){Post("toast","NORA v3: "+ex.Message);Post("status","Ошибка установки NORA v3 · прежняя модель сохранена");}
   finally{
    if(!committed){settings=previous;try{SaveSettings();}catch{}}
    settingUp=false;setupCancel.Dispose();setupCancel=null;
    if(!closing){State();Post("setupBusy",false);Post("busy",false);OpenModelCatalog();}
   }
  }else{PrepareNoraV3Assets();StopAir();StopServer();SelectNoraV3();}
 }
 void SelectNoraV3(){
  settings.Mode=4;settings.ContextTokens=4096;settings.MaxTokens=512;
  settings.Reasoning=false;settings.ResponseMode=0;settings.ExperimentalProfileId="";
  SaveSettings();State();Post("catalogChanged");Post("toast","Выбрана оригинальная CapyRiver Core 4B · NF4 / CUDA");
 }
 async Task<string> NoraV3Reply(List<ChatMessage> messages,Action<string> chunk,CancellationToken token,int? maximum,bool fit){
  if(!NoraV3Ready())throw new Exception("NORA v3 не установлена. Открой «Модели → CapyRiver Core 4B».");
  string key="nora-v3|"+NoraV3Python;
  if(air is not{HasExited:false}||airKey!=key){
   StopAir();PrepareNoraV3Assets();
   air=Launch(NoraV3Python,new(){"-u",NoraV3Script,NoraV3Home,"serve"});
   airErrors=Drain(air.StandardError);airKey=key;
  }
  var process=air!;
  var request=JsonSerializer.Serialize(new{messages,context_tokens=settings.ContextTokens,max_tokens=maximum??settings.MaxTokens,
   temperature=settings.Temperature,fit_output=fit});
  await process.StandardInput.WriteLineAsync(request.AsMemory(),token);await process.StandardInput.FlushAsync(token);
  while(true){
   string? line=await process.StandardOutput.ReadLineAsync(token);
   if(line==null)throw new Exception("NORA v3 завершилась. "+(airErrors==null?"":await airErrors));
   JsonDocument doc;try{doc=JsonDocument.Parse(line);}catch(JsonException){continue;}
   using(doc){var r=doc.RootElement;string type=r.GetProperty("type").GetString()??"",text=r.GetProperty("text").GetString()??"";
    if(type=="token")chunk(text);
    else if(type=="status")Post("status",text);
    else if(type=="error")throw new Exception(text);
    else if(type=="done"){
     if(r.TryGetProperty("limit_reached",out var limit)&&limit.GetBoolean()){
      var last=current.Messages.LastOrDefault(m=>m.Role=="assistant");if(last!=null)last.LimitReached=true;
     }
     if(text.Length==0)throw new Exception("NORA v3 не вернула текст");
     return text;
    }
   }
  }
 }
}
