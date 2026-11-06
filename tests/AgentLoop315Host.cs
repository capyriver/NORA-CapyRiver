using System.Text.Json;
namespace LocalChat;
// Deterministic host/model fixtures: production AgentLoop.cs is compiled unchanged below.
record ChatMessage(string role,string content);
sealed record McpTool(string Name,string Description,JsonElement Schema,bool ReadOnlyHint=false);
sealed record McpMatch(string Server,string ServerName,string Name,string Description);
sealed class FakeMcp {public bool Active=true;public List<McpTool> Tools=new();}
static class LocalApi {
 public static Func<List<ChatMessage>,JsonElement?,string> Next=(_,_)=>"Готово";
 public static List<(List<ChatMessage> Messages,JsonElement? Schema)> Requests=new();public static string? FinishReason;
 public static Uri Endpoint(string s)=>new(s);
 public static Task<string> ChatAsync(Uri endpoint,string model,List<ChatMessage> messages,int maxTokens,Action<string> onToken,CancellationToken token,double temperature=.7,List<string>? images=null,bool thinking=false,Action<string>? onFinish=null,JsonElement? responseSchema=null){token.ThrowIfCancellationRequested();Requests.Add((messages,responseSchema));string text=Next(messages,responseSchema);if(FinishReason!=null)onFinish?.Invoke(FinishReason);return Task.FromResult(text);}
}
partial class MainWindow {
 public readonly string root=Path.Combine(Path.GetTempPath(),"nora315-"+Guid.NewGuid().ToString("N"));
 public Settings settings=new(){Mode=1,ModelId="test",ContextTokens=24576,MaxTokens=1024};
 public ChatSession current=new();public Dictionary<string,FakeMcp> mcpClients=new();public HashSet<string> mcpDescribed=new();public FakeBrowser? browser;public int toolActions;
 public List<ToolCall> Executed=new();public List<string> Log=new();public Func<ToolCall,string> Result=_=>"ok";public CancellationTokenSource? generation;
 public Action<ToolCall>? BeforeExecute;public int LastAirMaximum;
 public class FakeBrowser {public bool IsDisposed;public bool FailManual;public void SetManual(bool v){if(FailManual)throw new ObjectDisposedException("browser");}}
 public MainWindow(){Directory.CreateDirectory(root);var schema=JsonSerializer.SerializeToElement(new{type="object",properties=new{code=new{type="string"}},required=new[]{"code"}});mcpClients["cloudflare"]=new(){Tools=new(){new("execute","Run actual code",schema),new("search","Search actual API",schema),new("docs","API docs",schema)}};}
 McpMatch[] FindMcpTools(string q,string server="",int limit=12)=>mcpClients.SelectMany(kv=>kv.Value.Tools.Select(t=>new McpMatch(kv.Key,"Cloudflare",t.Name,t.Description))).Take(limit).ToArray();
 Task<string> ExecuteMcp(ToolCall a,CancellationToken ct){ct.ThrowIfCancellationRequested();mcpDescribed.Add(a.server+"/"+a.name);return Task.FromResult(a.name+" inputSchema: "+mcpClients[a.server].Tools.Single(t=>t.Name==a.name).Schema.GetRawText());}
 Task<string> ExecuteTool(ToolCall a,Message response,CancellationToken ct){ct.ThrowIfCancellationRequested();BeforeExecute?.Invoke(a);Executed.Add(a);return Task.FromResult(Result(a));}
 Task<string> AirReply(List<ChatMessage> m,Action<string> chunk,CancellationToken ct,int max,bool fit){LastAirMaximum=max;return Task.FromResult(LocalApi.Next(m,null));}
 void SaveCurrent()=>Disk.Save(Path.Combine(root,"chat.json"),current);
 void RecordWork(string text)=>Log.Add(text);
 public AgentTask? Checkpoint=>Disk.Read<AgentTask>(AgentTaskPath(current.Id));
 public void PutCheckpoint(AgentTask task)=>Disk.Save(AgentTaskPath(current.Id),task);
 public async Task<Message> Run(string question,CancellationToken token=default){
  toolActions=0;activeAgentTask=null;var response=new Message{Role="assistant",State="streaming"};current.Messages.Add(new(){Role="user",Text=question});current.Messages.Add(response);
  var messages=new List<ChatMessage>{new("system","You are NORA. Answer latest request."),new("user",question)};
  try{response.Text=await ToolReply(messages,response,_=>{},token);response.State="complete";}
  finally{if(activeAgentTask is {Status:"running"}){activeAgentTask.Status="paused";response.TaskPending=true;SaveAgentTask();}SaveCurrent();}return response;
 }
}
