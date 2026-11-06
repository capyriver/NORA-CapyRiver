using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace LocalChat;
sealed record McpTool(string Name,string Description,JsonElement Schema,bool ReadOnlyHint=false);
sealed class McpClient:IDisposable {
 readonly Process process;readonly SemaphoreSlim gate=new(1,1);readonly StringBuilder pending=new();readonly char[] buffer=new char[4096];int id;bool disposed;
 public bool Active {get{try{return !disposed&&!process.HasExited;}catch{return false;}}}
 public List<McpTool> Tools {get;}=new();
 public McpClient(ProcessStartInfo info,Action<Process>? attach=null){process=Process.Start(info)??throw new Exception("MCP процесс не запущен");attach?.Invoke(process);_ = DrainErrors();}
 async Task DrainErrors(){try{char[] b=new char[1024];while(await process.StandardError.ReadAsync(b)>0){}}catch{} } // Never log potential credentials.
 async Task<string> Line(CancellationToken ct){while(true){string text=pending.ToString();int end=text.IndexOf('\n');if(end>=0){string line=text[..end].TrimEnd('\r');pending.Remove(0,end+1);return line;}int n=await process.StandardOutput.ReadAsync(buffer.AsMemory(),ct);if(n==0)throw new Exception("MCP процесс закрыл соединение. Проверь Node.js 24+, зависимости и переменные окружения.");pending.Append(buffer,0,n);if(pending.Length>1_000_000)throw new Exception("Ответ MCP больше 1 МБ");}}
 async Task Send(object value,CancellationToken ct){string json=JsonSerializer.Serialize(value);await process.StandardInput.WriteLineAsync(json.AsMemory(),ct);await process.StandardInput.FlushAsync(ct);}
 public async Task<JsonElement> Request(string method,object args,CancellationToken ct,int timeoutSeconds=120){
  await gate.WaitAsync(ct);try{
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));var token=timeout.Token;int request=++id;
   await Send(new{jsonrpc="2.0",id=request,method,@params=args},token);
   for(int records=0;records<500;records++){
    var line=await Line(token);if(string.IsNullOrWhiteSpace(line))continue;
    using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
    if(root.TryGetProperty("method",out _)){ // No sampling, elicitation, roots or nested capabilities.
     if(root.TryGetProperty("id",out var incoming))await Send(new{jsonrpc="2.0",id=incoming,error=new{code=-32601,message="Client capability not supported"}},token);continue;
    }
    if(!root.TryGetProperty("id",out var rid)||!rid.TryGetInt32(out int actual)||actual!=request)continue;
    if(root.TryGetProperty("error",out _))throw new Exception("MCP вернул ошибку протокола для "+method+". Секреты и детали провайдера не записаны в чат.");
    if(!root.TryGetProperty("result",out var result))throw new Exception("Нет result в ответе MCP");return result.Clone();
   }throw new Exception("Слишком много служебных сообщений MCP");
  }catch(OperationCanceledException)when(!ct.IsCancellationRequested){Dispose();throw new TimeoutException("MCP не ответил за "+timeoutSeconds+" секунд. Подключи сервер заново.");}catch{Dispose();throw;}finally{gate.Release();}
 }
 public async Task Initialize(CancellationToken ct){
  var init=await Request("initialize",new{protocolVersion="2025-06-18",capabilities=new{},clientInfo=new{name="NORA",version="3.18.0"}},ct,300);
  if(!init.TryGetProperty("protocolVersion",out var v)||!new[]{"2024-11-05","2025-03-26","2025-06-18","2025-11-25"}.Contains(v.GetString()))throw new Exception("Несовместимая версия протокола MCP");
  await Send(new{jsonrpc="2.0",method="notifications/initialized"},ct);
  string? cursor=null;var cursors=new HashSet<string>();
  do{
   var result=await Request("tools/list",cursor==null?new{}:(object)new{cursor},ct);
   foreach(var tool in result.GetProperty("tools").EnumerateArray()){
    string name=tool.GetProperty("name").GetString()??"";if(name.Length==0||name.Length>180||Tools.Any(t=>t.Name==name))throw new Exception("Некорректный список MCP tools");
    Tools.Add(new(name,tool.TryGetProperty("description",out var d)?d.GetString()??"":"",tool.GetProperty("inputSchema").Clone(),tool.TryGetProperty("annotations",out var annotations)&&annotations.ValueKind==JsonValueKind.Object&&annotations.TryGetProperty("readOnlyHint",out var readOnly)&&readOnly.ValueKind==JsonValueKind.True));if(Tools.Count>5000)throw new Exception("Слишком много инструментов MCP");
   }
   cursor=result.TryGetProperty("nextCursor",out var next)?next.GetString():null;if(cursor!=null&&!cursors.Add(cursor))throw new Exception("MCP повторяет страницу инструментов");
  }while(cursor!=null);
 }
 public McpTool Describe(string name)=>Tools.SingleOrDefault(t=>t.Name==name)??throw new Exception("Инструмент отсутствует в tools/list: "+name);
 public async Task<string> Call(string name,JsonElement args,CancellationToken ct){
  var tool=Describe(name);if(args.ValueKind!=JsonValueKind.Object)throw new Exception("arguments должен быть JSON-объектом");
  if(tool.Schema.TryGetProperty("required",out var req))foreach(var key in req.EnumerateArray())if(!args.TryGetProperty(key.GetString()!,out _))throw new Exception("Отсутствует обязательный аргумент: "+key.GetString());
  var result=await Request("tools/call",new{name,arguments=args},ct);var output=new StringBuilder();
  bool error=result.TryGetProperty("isError",out var e)&&e.ValueKind==JsonValueKind.True;output.AppendLine(error?"MCP TOOL FAILED":"MCP TOOL RESULT (not proof of a real-world outcome beyond the provider response)");
  if(result.TryGetProperty("content",out var content))foreach(var c in content.EnumerateArray()){
   if(c.TryGetProperty("type",out var type)&&type.GetString()=="text"&&c.TryGetProperty("text",out var text))output.AppendLine(text.GetString());else output.AppendLine("[нетекстовое содержимое не передано модели]");
   if(output.Length>12000)break;
  }
  if(result.TryGetProperty("structuredContent",out var structured)&&output.Length<12000)output.AppendLine(structured.ToString());
  string answer=output.ToString();return answer.Length>12000?answer[..12000]+"\n[Ответ ограничен; используй более точный запрос]":answer;
 }
 public void Dispose(){if(disposed)return;disposed=true;try{if(!process.HasExited)process.Kill(true);}catch{}process.Dispose();}
}
