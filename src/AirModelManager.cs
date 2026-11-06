using System.Diagnostics;
namespace LocalChat;
partial class MainWindow {
 string ExistingAirPath=>string.IsNullOrWhiteSpace(settings.OriginalAirModelPath)?(!string.IsNullOrWhiteSpace(settings.ModelPath)&&AirCatalog.Active(root,0,settings.ModelPath)==null?settings.ModelPath:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AirLLM","models","Qwen3-8B")):settings.OriginalAirModelPath;
 CatalogEntry ExistingAirEntry(){bool found=File.Exists(Path.Combine(ExistingAirPath,"config.json"));return new("air-existing","Моя Qwen3 · 8B","Твоя прежняя папка AirLLM · без повторной загрузки · не удаляется через каталог",0,true,found,settings.Mode==0&&ModelCatalog.SamePath(settings.ModelPath,ExistingAirPath),false,false,"");}
 CatalogEntry[] AirEntries()=>new[]{ExistingAirEntry()}.Concat(AirCatalog.Entries(root,settings.Mode,settings.ModelPath)).ToArray();
 void RememberAir(){if(string.IsNullOrWhiteSpace(settings.OriginalAirModelPath)&&AirCatalog.Active(root,0,settings.ModelPath)==null)settings.OriginalAirModelPath=settings.ModelPath;}
 async Task CheckAirEnvironment(string path,CancellationToken token){
  if(!File.Exists(settings.Python))throw new Exception("Укажи существующий Python из AirLLM в настройках. Приложение не устанавливает Python/CUDA автоматически.");
  string script=Path.Combine(root,"air_backend-2.8.py");File.WriteAllText(script,Resource("air_backend.py"));
  using var process=Launch(settings.Python,new(){"-u",script,"--check",path});using var stop=token.Register(()=>{try{process.Kill(true);}catch{}});
  var output=process.StandardOutput.ReadToEndAsync();var errors=Drain(process.StandardError);await process.WaitForExitAsync(token);string text=await output;string tail=await errors;
  if(process.ExitCode!=0)throw new Exception("Проверка AirLLM/CUDA не пройдена. Нужна совместимая версия AirLLM и transformers для Qwen2/Qwen3.\n"+text+tail);
 }
 async Task ChooseAirModel(string id){RequireIdle();if(importing)throw new Exception("Дождись импорта.");
  if(id=="air-existing"){
   if(!File.Exists(Path.Combine(ExistingAirPath,"config.json"))){Post("modelSettingsRequired",0);Post("toast","Укажи папку своей нынешней Qwen3-8B. Повторно скачивать её не нужно.");return;}
   RememberAir();StopAir();StopServer();AirCatalog.Apply(settings,ExistingAirPath,true);SaveSettings();State();Post("catalogChanged");Post("toast","Твоя Qwen3-8B выбрана. Веса и чаты сохранены.");return;
  }
  var spec=AirCatalog.Get(id);AirCatalog.Safe(root,spec);bool ready=AirCatalog.Downloaded(root,spec);
  if(!await ConfirmAsync("NORA · AirLLM",(ready?"Проверить и выбрать ":"Скачать ")+spec.Name+" через AirLLM?\n\n"+$"Веса: {spec.Bytes/1e9:F1} ГБ. С весами и слоями резервируй до {spec.Bytes*2.15/1e9+3:F0} ГБ диска.\n\n"+"Экспериментально: запуск на твоём ноутбуке не проверен. Большие слои, KV-кэш или подготовка могут не поместиться в ОЗУ/VRAM. Скорость может быть очень низкой.\n\nЭто текстовая модель: фото не распознаёт и не генерирует. При первом сообщении AirLLM подготовит слои; оригиналы сохраняются. Python/CUDA/AirLLM должны быть уже установлены.\n\nСкачать/проверить и выбрать?"))return;
  var previous=CloneSettings();bool committed=false;settingUp=true;setupCancel=new();var token=setupCancel.Token;Post("busy",true);Post("setupModel",spec.Name+" · AirLLM");Post("setupBusy",true);StopAir();StopServer();var progress=new Progress<string>(text=>{if(!closing&&settingUp&&setupCancel?.Token==token){Post("status",text);Post("setupProgress",text);}});Action<string> report=t=>((IProgress<string>)progress).Report(t);
  try{
   // Probe using a tiny pinned config before fetching tens of GB. No remote Python files downloaded.
   if(!File.Exists(settings.Python))throw new Exception("Не найден Python AirLLM. Укажи путь в настройках.");
   var config=spec.Files.Single(f=>f.Name=="config.json");string home=spec.Home(root);Directory.CreateDirectory(home);using(var client=new HttpClient{Timeout=Timeout.InfiniteTimeSpan})await DirectSetup.Download(client,new Uri($"https://huggingface.co/{spec.Repo}/resolve/{spec.Revision}/config.json"),Path.Combine(home,"config.json"),config.Bytes,config.Sha256,report,token);
   report("Проверяю Python, CUDA и поддержку архитектуры AirLLM…");await CheckAirEnvironment(home,token);
   await Task.Run(()=>AirCatalog.Install(root,spec,report,token),token);token.ThrowIfCancellationRequested();if(closing)return;
   RememberAir();AirCatalog.Apply(settings,home,spec.Thinking);SaveSettings();committed=true;State();Post("catalogChanged");Post("toast","Веса проверены и модель выбрана. Слои подготовятся при первом сообщении; запуск ещё не проверен.");
  }catch(OperationCanceledException){Post("status","Остановлено. Частичная загрузка сохранена.");}catch(Exception e){Post("toast","AirLLM не подключён: "+e.Message);}
  finally{if(!committed){settings=previous;if(!closing)try{SaveSettings();}catch(Exception e){Post("toast","Не удалось восстановить настройки: "+e.Message);}}settingUp=false;setupCancel.Dispose();setupCancel=null;if(!closing){State();Post("setupBusy",false);Post("busy",false);OpenModelCatalog();}}
 }
 async Task DeleteAirModels(string? id){RequireIdle();if(importing)throw new Exception("Дождись импорта.");var specs=id==null?AirCatalog.Models:new[]{AirCatalog.Get(id)};var owners=specs.Select(s=>s.Owner).ToArray();long bytes=ModelCatalog.UsedBytes(root,owners);if(bytes==0){Post("toast","Нет управляемых файлов AirLLM.");return;}if(!await ConfirmAsync("NORA · удаление AirLLM",$"Удалить {(id==null?"ВСЕ скачанные NORA модели AirLLM":"эту модель AirLLM")}?\n\nВеса, подготовленные слои и части загрузок: {bytes/1e9:F1} ГБ. Без корзины.\n\nТвоя исходная Qwen3-8B вне NORA, чаты и GGUF сохраняются."))return;
  bool active=settings.Mode==0&&specs.Any(s=>ModelCatalog.SamePath(settings.ModelPath,s.Home(root)));if(active){StopAir();settings.ModelPath="";SaveSettings();}
  settingUp=true;setupCancel=new();Post("busy",true);try{var errors=await Task.Run(()=>ModelCatalog.Delete(root,owners));Post("toast",errors.Length==0?"Файлы AirLLM удалены. Твоя исходная модель и чаты сохранены.":string.Join("\n",errors));}finally{settingUp=false;setupCancel.Dispose();setupCancel=null;if(!closing){State();Post("busy",false);OpenModelCatalog();}}
 }
}
