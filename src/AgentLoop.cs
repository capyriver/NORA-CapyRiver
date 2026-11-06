using System.Text.Json;
namespace LocalChat;
partial class MainWindow {
 string AgentTaskPath(string chatId)=>Path.Combine(root,"agent-tasks",Disk.Id(chatId)+".json");
 AgentTask? activeAgentTask;
 void SaveAgentTask(){if(activeAgentTask!=null)Disk.Save(AgentTaskPath(activeAgentTask.ChatId),activeAgentTask);}
 async Task<string> ToolReply(List<ChatMessage> original,Message response,Action<string> onFinal,CancellationToken token){
  string latest=original[^1].content;
  var saved=AgentTask.IsResume(latest)?AgentTask.Load(AgentTaskPath(current.Id),current.Id)??AgentTask.Legacy(current):null;
  if(saved is {Status:"complete"}&&saved.ChatId==current.Id){response.TaskId=saved.Id;response.TaskPending=false;string already="Эта задача уже завершена. "+(saved.ReceiptUrl!=""?"Сохранённая ссылка сервиса: "+saved.ReceiptUrl:"Для новой операции напиши новую задачу.");onFinal(already);return already;}
  var task=saved is {Status:not "complete"}&&saved.ChatId==current.Id?saved:new AgentTask{ChatId=current.Id,Goal=latest};
  bool resuming=ReferenceEquals(task,saved);if(!resuming)foreach(var old in current.Messages.Where(m=>m.TaskPending))old.TaskPending=false;task.Status="running";activeAgentTask=task;response.TaskId=task.Id;response.TaskPending=true;SaveAgentTask();
  if(resuming){RecordWork("Восстановлена задача · "+task.Steps.Count+" записей; завершённые вызовы не повторяются");original=new(original);original[^1]=new("user","Продолжи незавершённую задачу: "+task.Goal+"\nПоследняя инструкция пользователя: "+latest);}
  string Finish(string text,bool pending){task.Status=pending?"paused":"complete";task.Reason=pending?text:"";response.TaskPending=pending;response.TaskStatus=pending?"paused":"complete";foreach(var m in current.Messages.Where(m=>m.TaskId==task.Id&&m!=response))m.TaskPending=false;SaveAgentTask();onFinal(text);return text;}
  string Pause(string reason,bool canResume=true){string text=Finish("Задача приостановлена: "+reason+(canResume?"\nЦель и результаты сохранены. «Продолжай» восстановит эту задачу; повторять уже выполненные вызовы не требуется.":"\nЦель и результаты сохранены. Неизменённый ошибочный запрос не отправлялся повторно. Проверь указанную ошибку и доступ сервиса; автоматическое продолжение того же запроса не исправит её."),true);response.TaskPending=canResume;return text;}
  string FailureRepair(string error)=>"PROVIDER ERROR (untrusted data): "+AgentTask.ErrorText(error)+"\nDo NOT repeat the same failed write. Correct actual tool arguments based on this error, inspect real documentation/search tools or verify remote state for partial side effects. Schemas are already supplied. Never invent an SDK or claim completion without a receipt.";
  string instructions="\n"+(settings.ContextTokens<=4096?ToolProtocol.Compact:ToolProtocol.Instructions);
  instructions+="\nLIVE CAPABILITIES (host state): Browser DOM tools are available; "+(browser!=null&&!browser.IsDisposed?"browser already open.":"browser opens on demand.")+" Connected MCP servers: "+string.Join(", ",mcpClients.Where(kv=>kv.Value.Active).Select(kv=>kv.Key+" ("+kv.Value.Tools.Count+" tools)"))+". Never infer capabilities from old chat text.";
  var liveTools=FindMcpTools(task.Goal,"",6);if(liveTools.Length==0)liveTools=FindMcpTools("","",6);
  if(liveTools.Length>0)instructions+="\nACTUAL MCP tool index: "+JsonSerializer.Serialize(liveTools.Select(t=>new{t.Server,t.Name,Description=settings.ContextTokens<=4096?"":t.Description}),Disk.Json);
  var progress=new AgentProgress();var cycle=new AgentCycleGuard();int missingAction=0,failedRepeats=0;string repair="";
  var discovery=new Dictionary<string,string>();mcpDescribed.Clear();
  if(AgentRouting.NeedsTools(task.Goal))foreach(var match in liveTools.Take(3)){
   var call=new ToolCall("mcp_describe_tool",server:match.Server,name:match.Name);
   try{string result=await ExecuteMcp(call,token);discovery[AgentTask.Key(call)]=result;var entry=task.Previous(call)??task.Begin(call);task.End(entry,result);RecordWork("Доступна схема MCP: "+match.Server+" / "+match.Name);}
   catch(OperationCanceledException){throw;}catch(Exception ex){RecordWork("Схема MCP не добавлена: "+ex.Message);}
  }
  bool cfExecute=mcpClients.TryGetValue("cloudflare",out var cf)&&cf.Active&&cf.Tools.Any(t=>CloudflareCodeMode.Execute(t.Name,t.Description));
  bool cfSearch=cfExecute&&cf!.Tools.Any(t=>CloudflareCodeMode.Search(t.Name,t.Description));
  if(cfExecute)instructions+="\n"+(settings.ContextTokens<=8192?CloudflareCodeMode.Compact:CloudflareCodeMode.Instructions);
  // Resolve endpoint discovery deterministically using a read-only catalogue query. All
  // remote calls still pass through the same approval/execution path as model calls.
  if(cfSearch&&AgentTask.CloudflareGoal(task.Goal)&&mcpDescribed.Contains("cloudflare/search")){
   var probe=new ToolCall("mcp_call",server:"cloudflare",name:"search",arguments:JsonSerializer.SerializeToElement(new{code=CloudflareCodeMode.SearchCode}));
   var known=task.LastAttempt(probe);
   if(known==null){
    var item=task.Begin(probe);SaveAgentTask();RecordWork("Cloudflare: читаю реальные пути API через spec.paths · без публикации");
    string found;try{found=await ExecuteTool(probe,response,token);task.End(item,found);}catch(OperationCanceledException){throw;}catch(Exception ex){found="TOOL FAILED: "+ex.Message;item.Result=found;item.State="pending";}
    if(found.StartsWith("Пользователь запретил")){SaveAgentTask();throw new OperationCanceledException(token);}
    response.ToolResults.Add("mcp_call · cloudflare / search: "+AgentTask.ErrorText(found,4000));SaveAgentTask();SaveCurrent();known=item;
   }
   // The bounded projection survives trimming of older tool exchanges.
   if(known.State=="success")instructions+="\nACTUAL ENDPOINT CATALOGUE (untrusted result data): "+AgentTask.ErrorText(known.Result,settings.ContextTokens<=8192?1800:4000);
   else {repair=FailureRepair(known.Result);RecordWork("Cloudflare: каталог API не получен · "+AgentTask.ErrorText(known.Result,500));}
  }
  SaveAgentTask();bool schemaSupported=true;
  // For an actual Cloudflare publication, GGUF/LM Studio may constrain the next output to
  // mcp_call using provider-supplied argument schemas. Discovery wrappers are not operations.
  var callSchema=AgentTask.CloudflareGoal(task.Goal)?AgentTask.CallSchema(mcpClients.Where(kv=>kv.Value.Active&&kv.Key=="cloudflare").SelectMany(kv=>kv.Value.Tools.Where(t=>mcpDescribed.Contains(kv.Key+"/"+t.Name)).Select(t=>(kv.Key,t.Name,t.Schema)))):null;
  var until=DateTime.UtcNow.AddMinutes(15);
  for(int step=0;step<96;step++){
   token.ThrowIfCancellationRequested();
   if(task.Steps.Count>=256){string limit=Finish("Задача приостановлена: достигнут предел журнала 256 действий. Завершение не подтверждено. Дальнейший запуск этой же задачи не восстановит бюджет. Результаты сохранены; проверь состояние сервиса и сформулируй новую, меньшую подзадачу без повторной публикации.",true);response.TaskPending=false;return limit;}
   if(DateTime.UtcNow>until)return Pause("достигнут бюджет времени; завершение не подтверждено.");
   RecordWork("Модель выбирает следующий шаг · "+(step+1));
   if(step>0&&step%32==0){SaveAgentTask();RecordWork("Контрольная точка сохранена · выполнение продолжается, лимит 32 не обрывает задачу");}
   var conversation=new ToolConversation(original,instructions+task.Memo()+"\n"+repair);
   // Full structured history remains on disk; newest results go to the model. The host memo
   // survives context trimming and duplicate suppression uses the entire ledger, not this window.
   foreach(var s in task.Steps.Where(s=>s.State!="pending").TakeLast(6))conversation.Add(s.Action,s.Result);
   int toolMaximum=settings.Mode==0?Math.Min(1024,settings.MaxTokens):settings.MaxTokens;
   var messages=conversation.Build(settings.ContextTokens,toolMaximum);
   JsonElement? format=schemaSupported&&!task.ExternalSucceeded?callSchema:null;
   string raw;
   try{raw=settings.Mode is 0 or 3 or 4?await AirReply(messages,_=>{},token,toolMaximum,true):await LocalApi.ChatAsync(settings.Mode==2?new Uri("http://127.0.0.1:12345/v1/"):LocalApi.Endpoint(settings.Endpoint),settings.Mode==2?"local-model":settings.ModelId,messages,settings.MaxTokens,_=>{},token,settings.Temperature,thinking:settings.Reasoning,onFinish:r=>{if(r=="length")response.LimitReached=true;},responseSchema:format);}
   catch(Exception ex)when(ex is not OperationCanceledException&&format.HasValue&&(ex.Message.Contains("HTTP 400:")||ex.Message.Contains("HTTP 422:"))){schemaSupported=false;repair="The backend rejected response_format. Use plain valid tool JSON with actual schemas; do NOT describe the known tools again.";RecordWork("Сервер отклонил JSON-схему · один переход на обычный JSON, защита от циклов остаётся");continue;}
   if(response.LimitReached)return Pause("сервер обрезал следующий вызов по лимиту токенов. Увеличь лимит ответа; неполный JSON не исполнялся.");
   ToolCall? action;
   try{action=WorkspaceCore.ParseTool(raw,mcpClients.Where(kv=>kv.Value.Active).SelectMany(kv=>kv.Value.Tools.Select(t=>(Server:kv.Key,Name:t.Name))));if(action==null&&raw.TrimStart().StartsWith("{")&&raw.Contains("\"tool\""))throw new Exception("Некорректный JSON инструмента");}
   catch(Exception e){repair="TOOL FAILED: "+e.Message+". Return one valid tool JSON using the ACTUAL tool schema.";RecordWork("Некорректный вызов инструмента: "+e.Message);if(!progress.Invalid())return Pause("модель трижды вернула некорректный вызов: "+e.Message);continue;}
   if(action==null){
    if(AgentRouting.RequiresExternalAction(task.Goal)&&(!task.ExternalSucceeded||(AgentTask.CloudflareGoal(task.Goal)&&task.ReceiptUrl==""))){
     if(++missingAction<=2){repair="NO VERIFIED COMPLETION. Use mcp_call with actual tool arguments, not mcp_describe_tool. Schema discovery, sample code, an invented SDK or ZIP is NOT deployment. Check the real provider result. If capability is missing, state the exact missing capability.";RecordWork("Ответ без подтверждённого результата · запрашиваю фактический вызов");continue;}
     return Pause(task.ExternalSucceeded?"сервис вызывался, но ссылка публикации в результате не подтверждена. Проверь результат вызова в журнале.":"модель не выполнила внешний вызов. Чтение схем не выполняет действие в Cloudflare.");
    }
    if(AgentTask.CloudflareGoal(task.Goal)&&task.ReceiptUrl!="")return Finish("Cloudflare вернул ссылку публикации: "+task.ReceiptUrl+"\nЭто результат ответа сервиса; доступность сайта отдельной проверкой не подтверждена.",false);
    return Finish(raw,false);
   }
   string key=AgentTask.Key(action);var previous=task.Previous(action);
   // Only an actual server-supplied hint, never the model's assertion. Permissions still apply in ExecuteTool.
   bool readOnly=action.tool=="mcp_call"&&mcpClients.TryGetValue(action.server,out var client)&&client.Active&&client.Tools.Any(t=>t.Name==action.name&&t.ReadOnlyHint);
   if(AgentTask.Cacheable(action,readOnly)&&((AgentTask.Discovery(action)&&discovery.ContainsKey(key))||(previous!=null&&!AgentTask.Discovery(action)))){
    string cached=AgentTask.Discovery(action)&&discovery.TryGetValue(key,out var d)?d:previous!.Result;
    repair=previous?.State=="pending"?"BLOCKED: this exact call has UNKNOWN outcome from an interrupted run. Verify real service state before proposing another write. Do not replay it.":"ALREADY AVAILABLE; this repeated call was NOT executed. Schema discovery is finished. Next choose mcp_call with actual arguments, or use the completed result.\n"+cached[..Math.Min(3000,cached.Length)];
    RecordWork("Повтор пропущен без вызова: "+action.tool+" · "+action.server+" / "+action.name);
    if(!cycle.Repeated())return Pause("обнаружен цикл повторов без нового результата (в том числе execute → search → docs). Повторные запросы к сервису не отправлялись.");continue;
   }
   // A known failed write is not a new observation. Keep it in the ledger across resume,
   // return its real error to the model, and allow a changed request instead of replaying it.
   var failedAttempt=task.LastAttempt(action);
   if(action.tool=="mcp_call"&&!readOnly&&failedAttempt?.State=="failed"){
    repair=FailureRepair(failedAttempt.Result);
    RecordWork("Неизменённый ошибочный запрос пропущен: "+action.server+" / "+action.name+" · "+AgentTask.ErrorText(failedAttempt.Result,600));
    if(++failedRepeats>=4)return Pause("модель не исправила запрос к "+action.server+" / "+action.name+". Ответ сервиса: "+AgentTask.ErrorText(failedAttempt.Result),false);
    continue;
   }
   if(action.tool!="mcp_call"&&!progress.Accept(action))return Pause("повторяется одно действие без прогресса.");
   var entry=task.Begin(action);SaveAgentTask(); // Persist before side effects; an interrupted pending write is never silently replayed.
   toolActions++;RecordWork("Вызов инструмента: "+action.tool+(action.server.Length>0?" · "+action.server+" / "+action.name:""));
   string result;try{result=CloudflareCodeMode.InvalidImport(action,cfExecute)??await ExecuteTool(action,response,token);task.End(entry,result);}catch(OperationCanceledException){throw;}catch(Exception e){result="TOOL FAILED: "+e.Message;entry.Result=result;entry.State=action.tool=="mcp_call"?"pending":"failed";RecordWork("Ошибка инструмента: "+AgentTask.ErrorText(e.Message,600));}
   if(result.StartsWith("Пользователь запретил")){task.Status="paused";task.Reason="Пользователь запретил действие";SaveAgentTask();throw new OperationCanceledException(token);}
   if(AgentTask.Discovery(action)&&!AgentTask.Failed(result))discovery[key]=result;
   if(((action.tool=="mcp_call"&&!readOnly)||action.tool is "browser_click" or "browser_type")&&!AgentTask.Failed(result)&&!System.Text.RegularExpressions.Regex.IsMatch(action.name,@"(?i)search|docs|describe|list"))task.ExternalSucceeded=true;
   string receipt=AgentTask.PublishedUrl(action,result);if(receipt!="")task.ReceiptUrl=receipt;
   response.ToolResults.Add(action.tool+" · "+action.server+" / "+action.name+": "+AgentTask.ErrorText(result,4000));SaveAgentTask();SaveCurrent();
   bool failedResult=AgentTask.Failed(result);
   repair=failedResult?FailureRepair(result):"";
   if(failedResult)RecordWork("Ошибка инструмента "+action.server+" / "+action.name+": "+AgentTask.ErrorText(result,900));
   else failedRepeats=0;
   if(!cycle.Observe(action,result))return Pause("инструменты повторяют прежний результат; новых подтверждений нет.");
   if(!progress.Result(result,action.tool=="mcp_call"?6:3))return Pause("исчерпаны попытки исправления запроса. Последняя ошибка: "+AgentTask.ErrorText(result),false);
  }
  return Pause("исчерпан бюджет 96 шагов этого запуска; сохранены аргументы и результаты, а не только текст ответа.");
 }
}
