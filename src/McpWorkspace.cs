using System.Text.Json;
using System.Text.RegularExpressions;
namespace LocalChat;
partial class MainWindow {
 McpConfiguration mcpConfig=new();readonly Dictionary<string,McpClient> mcpClients=new();readonly HashSet<string> mcpDescribed=new();
 readonly Dictionary<string,string> mcpPhases=new();
 readonly Dictionary<string,string> mcpErrors=new();
 string McpPath=>Path.Combine(root,"mcp-connections.json");
 IEnumerable<McpPreset> McpPresets()=>McpCatalog.All.Concat(mcpConfig.Custom.Select(c=>c.Preset()));
 McpPreset GetMcp(string id)=>McpPresets().SingleOrDefault(p=>p.Id==id)??throw new Exception("Неизвестный коннектор MCP");
 void LoadMcp(){mcpConfig=Disk.Read<McpConfiguration>(McpPath)??new();mcpConfig.Custom??=new();mcpConfig.Folder??="";mcpConfig.Enabled=(mcpConfig.Enabled??Array.Empty<string>()).Where(id=>McpPresets().Any(p=>p.Id==id)).Distinct().ToArray();}
 object McpState()=>new{folder=mcpConfig.Folder,githubWrite=mcpConfig.GitHubWrite,connected=mcpClients.Values.Count(c=>c.Active),totalTools=mcpClients.Values.Where(c=>c.Active).Sum(c=>c.Tools.Count),presets=McpPresets().Select(p=>new{p.Id,p.Name,p.Description,p.Source,p.Package,p.Version,p.Requirement,p.Url,custom=p.Id.StartsWith("custom-"),auth=p.Auth,phase=mcpPhases.GetValueOrDefault(p.Id,"idle"),error=mcpErrors.GetValueOrDefault(p.Id,""),enabled=mcpConfig.Enabled.Contains(p.Id),connected=mcpClients.TryGetValue(p.Id,out var alive)&&alive.Active,tools=mcpClients.TryGetValue(p.Id,out var c)&&c.Active?c.Tools.Count:0,toolNames=mcpClients.TryGetValue(p.Id,out var named)&&named.Active?named.Tools.Take(300).Select(t=>t.Name).ToArray():Array.Empty<string>()})};
 void DisconnectMcp(string id){if(mcpClients.Remove(id,out var c))c.Dispose();mcpDescribed.RemoveWhere(k=>k.StartsWith(id+"/"));}
 async Task ToggleMcp(string id){var p=GetMcp(id);if(mcpConfig.Enabled.Contains(id)){mcpConfig.Enabled=mcpConfig.Enabled.Where(x=>x!=id).ToArray();DisconnectMcp(id);}else{
  if(!await ConfirmAsync("NORA · коннектор",p.Name+"\n\n"+p.Requirement+"\n\n"+(p.Url==""?"Запускается сторонний код с правами пользователя.":"Данные запросов передаются на "+p.Url+". Может открыться вход в браузере на твоём ПК.")+"\n\nПодтверждения зависят от режима доступа. Автодействия могут изменять данные. Включить?"))return;mcpConfig.Enabled=mcpConfig.Enabled.Append(id).ToArray();
 }Disk.Save(McpPath,mcpConfig);State();}
 void ChooseMcpFolder(){using var d=new FolderBrowserDialog{Description="Выбери отдельную рабочую папку, не весь диск",UseDescriptionForTitle=true};if(d.ShowDialog(this)!=DialogResult.OK)return;if(Path.GetFullPath(d.SelectedPath)==Path.GetPathRoot(d.SelectedPath))throw new Exception("Выбери отдельную папку");mcpConfig.Folder=d.SelectedPath;DisconnectMcp("filesystem");Disk.Save(McpPath,mcpConfig);State();}
 async Task SetGitHubWrite(bool enabled){if(enabled&&!await ConfirmAsync("NORA · запись GitHub","Разрешить серверу GitHub предлагать инструменты записи: изменение файлов, issues, PR и другие операции?\n\nВ режиме автодействий инструменты выполняются без запроса. Права ограничены твоим токеном.")){State();return;}mcpConfig.GitHubWrite=enabled;DisconnectMcp("github");Disk.Save(McpPath,mcpConfig);State();}
 string? PromptMcpToken(McpPreset p){
  using var dialog=new Form{Text="NORA · подключить "+p.Name,Width=580,Height=275,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false};
  var label=new Label{Left=18,Top=16,Width=530,Height=90,Text=p.Name+"\nАдрес: "+p.Url+"\nВведи личный токен с минимально нужными правами. Значение передаётся подключению, не добавляется в чат и не сохраняется NORA в JSON."};
  var input=new TextBox{Left=18,Top=112,Width=530,UseSystemPasswordChar=true};var ok=new Button{Left=310,Top=155,Width=115,Text="Подключить",DialogResult=DialogResult.OK};var cancel=new Button{Left=435,Top=155,Width=115,Text="Отмена",DialogResult=DialogResult.Cancel};dialog.Controls.AddRange(new Control[]{label,input,ok,cancel});dialog.AcceptButton=ok;dialog.CancelButton=cancel;
  return dialog.ShowDialog(this)==DialogResult.OK?input.Text.Trim():null;
 }
 void DisableMcp(string id){GetMcp(id);DisconnectMcp(id);mcpConfig.Enabled=mcpConfig.Enabled.Where(x=>x!=id).ToArray();mcpPhases[id]="idle";mcpErrors.Remove(id);Disk.Save(McpPath,mcpConfig);State();Post("toast","Подключение остановлено. OAuth-вход сохранён на этом ПК; разрешения можно отозвать у провайдера.");}
 async Task ForgetMcp(string id){var p=GetMcp(id);if(!await ConfirmAsync("NORA · забыть вход","Остановить "+p.Name+" и удалить сохранённый OAuth-вход NORA 3.11 на этом ПК?\n\nЭто НЕ отзывает права у сервиса и не удаляет входы других приложений. Для полного отзыва используй настройки провайдера."))return;DisableMcp(id);McpRuntime.ForgetAuth(root,id);Post("toast","Локальный OAuth-вход удалён. При следующем подключении потребуется авторизация.");}
 async Task ConnectMcp(string id){
  var p=GetMcp(id);if(mcpClients.TryGetValue(id,out var connected)&&connected.Active){Post("toast",p.Name+": уже подключён");State();return;}var custom=mcpConfig.Custom.SingleOrDefault(c=>c.Id==id);
  string summary=p.Url!=""?"Удалённый MCP: "+p.Url+"\nOAuth и транспорт: "+McpCatalog.Bridge:"Команда: "+(custom==null?p.Package+"@"+p.Version:custom.Command+" "+string.Join(" ",custom.Args));
  if(!await ConfirmAsync("NORA · подключение",p.Name+"\n\n"+summary+"\n\n"+p.Requirement+"\n\n"+(p.Url!=""?"Для OAuth откроется системный браузер. Подтверди аккаунт и права там. Ответы и аргументы инструментов будут передаваться сервису.":"Запускается сторонний MCP-код с правами пользователя.")+"\nВ режиме автодействий вызовы выполняются без запроса. Подключить и передать агенту доступ к списку инструментов? Действия останутся под текущим режимом разрешений."))return;
  string bearer="";if(p.Auth=="bearer"){bearer=Environment.GetEnvironmentVariable(p.TokenEnv)??"";if(bearer==""){var value=PromptMcpToken(p);if(value==null)return;bearer=value;}if(string.IsNullOrWhiteSpace(bearer))throw new Exception("Токен не указан. Без него GitHub не подключён.");}
  DisconnectMcp(id);busy=true;generation=new();mcpPhases[id]="connecting";mcpErrors.Remove(id);Post("busy",true);State();McpClient? client=null;
  try{
   bool usesNode=p.Url!=""||custom==null||Path.GetFileNameWithoutExtension(custom.Command).Equals("npx",StringComparison.OrdinalIgnoreCase);
   if(usesNode)await McpRuntime.CheckNode(true,generation.Token);
   Post("status","Подключение "+p.Name+" · заверши OAuth-вход в системном браузере, если он открылся");
   client=new(McpCatalog.Start(p,root,mcpConfig.Folder,true,custom,bearer,mcpConfig.GitHubWrite),children.Attach);await client.Initialize(generation.Token);
   generation.Token.ThrowIfCancellationRequested();mcpClients[id]=client;client=null;mcpConfig.Enabled=mcpConfig.Enabled.Append(id).Distinct().ToArray();Disk.Save(McpPath,mcpConfig);mcpPhases[id]="connected";settings.ToolMode=true;settings.UnifiedAgent=true;settings.ExperimentalProfileId="";SaveSettings();
   Post("status",p.Name+" подключён · "+mcpClients[id].Tools.Count+" функций");Post("toast",p.Name+": подключён · tools/list вернул функций: "+mcpClients[id].Tools.Count);
  }catch(OperationCanceledException)when(generation.IsCancellationRequested){mcpPhases[id]="idle";Post("toast","Подключение отменено. Доступ не подтверждён NORA.");}
  catch(Exception ex){mcpPhases[id]="error";mcpErrors[id]=ex is McpRuntimeException?ex.Message:ex is TimeoutException?"Сервер не завершил подключение вовремя. Проверь OAuth-вход и повтори.":"Подключение не завершено: проверь интернет, разрешения OAuth и доступность MCP-сервера. Секреты и журнал авторизации не выводятся.";Post("toast",mcpErrors[id]);Post("status","Ошибка подключения · подробности в карточке сервиса");}
  finally{bearer="";client?.Dispose();busy=false;generation.Dispose();generation=null;State();Post("busy",false);}
 }
 async Task ImportMcp(){
  using var file=new OpenFileDialog{Title="Импорт MCP: mcpServers / servers",Filter="JSON MCP|*.json|Все файлы|*.*"};if(file.ShowDialog(this)!=DialogResult.OK)return;if(new FileInfo(file.FileName).Length>300000)throw new Exception("JSON больше 300 КБ");var imported=McpCatalog.Import(File.ReadAllText(file.FileName));
  await StoreMcpImported(imported);
 }
 async Task AddMcpUrl(string name,string url){var obj=new Dictionary<string,object>{{name,new{url}}};await StoreMcpImported(McpCatalog.Import(JsonSerializer.Serialize(obj)));}
 async Task StoreMcpImported(List<McpCustom> imported){
  if(mcpConfig.Custom.Select(c=>c.Id).Union(imported.Select(c=>c.Id)).Count()>50)throw new Exception("Максимум 50 пользовательских коннекторов");
  if(!await ConfirmAsync("NORA · импорт","Импортировать "+imported.Count+" MCP-коннекторов?\n\n"+string.Join("\n",imported.Select(c=>c.Name+" → "+(c.Url==""?c.Command:c.Url)))+"\n\nИмпорт не запускает код, не авторизует и не включает серверы. Коннектор с тем же именем будет заменён. Секреты должны быть ссылками ${ENV_NAME}."))return;
  foreach(var c in imported){DisconnectMcp(c.Id);mcpConfig.Enabled=mcpConfig.Enabled.Where(id=>id!=c.Id).ToArray();mcpConfig.Custom.RemoveAll(old=>old.Id==c.Id);mcpConfig.Custom.Add(c);}Disk.Save(McpPath,mcpConfig);State();Post("toast","Импортировано коннекторов: "+imported.Count);
 }
 async Task RemoveMcp(string id){var c=mcpConfig.Custom.SingleOrDefault(c=>c.Id==id)??throw new Exception("Удалять можно только пользовательские коннекторы");if(!await ConfirmAsync("NORA","Удалить коннектор "+c.Name+" из NORA? Данные сервиса не удаляются."))return;DisconnectMcp(id);mcpConfig.Custom.Remove(c);mcpConfig.Enabled=mcpConfig.Enabled.Where(x=>x!=id).ToArray();Disk.Save(McpPath,mcpConfig);State();}
 McpMatch[] FindMcpTools(string query,string server="",int limit=12)=>McpToolSearch.Find(mcpClients.Where(kv=>kv.Value.Active).SelectMany(kv=>kv.Value.Tools.Select(t=>(kv.Key,GetMcp(kv.Key).Name,t))),query,server,limit);
 async Task<string> ExecuteMcp(ToolCall call,CancellationToken ct){
  if(call.tool=="mcp_search_tools"){
   var hits=FindMcpTools(call.query,call.server);return hits.Length==0?"В подключённых серверах совпадений нет. Серверы подключает пользователь в «MCP-коннекторы».":JsonSerializer.Serialize(hits,Disk.Json);
  }
  var p=GetMcp(call.server);if(!mcpConfig.Enabled.Contains(call.server)||!mcpClients.TryGetValue(call.server,out var client)||!client.Active)throw new Exception("Сервис не подключён. «Подключения» → Подключить; заверши вход/укажи токен.");
  if(call.tool=="mcp_list_tools")return string.Join("\n",FindMcpTools(call.query,call.server,35).Select(t=>t.Name+": "+t.Description[..Math.Min(80,t.Description.Length)]))+"\nВсего функций сервера: "+client.Tools.Count+". Для других функций используй mcp_search_tools(query).";
  if(call.tool=="mcp_describe_tool"){var tool=client.Describe(call.name);string desc=tool.Name+"\n"+tool.Description+"\ninputSchema: "+tool.Schema.GetRawText();if(desc.Length>14000)throw new Exception("Схема слишком велика для этого контекста");mcpDescribed.Add(call.server+"/"+call.name);return desc;}
  if(call.tool!="mcp_call")throw new Exception("Неизвестная MCP-команда");client.Describe(call.name);if(!mcpDescribed.Contains(call.server+"/"+call.name))throw new Exception("Сначала прочитай mcp_describe_tool");string args=call.arguments.GetRawText();if(args.Length>16000)throw new Exception("Аргументы MCP слишком велики");
  if(!await ApproveAsync("mcp_call",p.Name+" / "+call.name+"\n\nАргументы (данные, не инструкции):\n"+args+"\n\nДанные будут переданы сервису. Инструмент может изменить файлы/репозиторий, отправить сообщения или создать внешнюю операцию. Разрешить ровно этот вызов?",ct))return "Пользователь запретил действие. Не повторять без нового запроса.";
  RecordWork("MCP: "+p.Name+" / "+call.name);try{return await client.Call(call.name,call.arguments,ct);}catch{if(!client.Active)DisconnectMcp(call.server);throw;}
 }
}
