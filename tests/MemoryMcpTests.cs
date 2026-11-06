using LocalChat;
using System.Diagnostics;
using System.Text.Json;
int passed=0;void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
void Reject(Action a,string name){try{a();}catch{Check(true,name);return;}throw new Exception("Not rejected: "+name);}
JsonElement J(string s)=>JsonDocument.Parse(s).RootElement.Clone();
var chat=new ChatSession{MemoryNote="Моё имя Вася; приложение NORA"};
for(int i=0;i<20;i++){chat.Messages.Add(new(){Role="user",Text=i==0?"Моя видеокарта RTX 3060, 12 ГБ":"вопрос номер "+i});chat.Messages.Add(new(){Role="assistant",Text="ответ "+i});}
chat.Messages.Add(new(){Role="user",Text="текущий вопрос"});chat.Messages.Add(new(){Role="assistant",State="streaming"});
Check(Disk.History(chat).Count==40,"only completed user/assistant pairs");Check(ChatMemory.Recall(chat,"Вспомни, какая видеокарта?").Contains("RTX 3060"),"old relevant message retrieved");Check(ChatMemory.Recall(chat,"Привет").Contains("Вася"),"pinned note always included");Check(!ChatMemory.Recall(new(),"Привет").Contains("Вася"),"no cross-chat memory leakage");Check(ChatMemory.Search(chat,"RTX").Contains("3060"),"history search finds early fact");
var history=new List<ChatMessage>{new("system","Rules"),new("user","OLD "+new string('a',2500)),new("assistant","old answer"),new("user","latest question")};Check(ContextWindow.Fit(history,1024,200)==2,"trim full oldest pair");Check(history.Count==2&&history[^1].content=="latest question","preserve latest user intact");Reject(()=>ContextWindow.Fit(new(){new("system","x"),new("user",new string('я',3000))},1024,200),"oversized current question rejected, not clipped");
var conv=new ToolConversation(new(){new("system","rules"),new("user","my name Вася"),new("assistant","noted"),new("user","read repo and compare")}," tools");conv.Add("action1","first tool result");conv.Add("action2","second tool result");var built=conv.Build(8192,512);Check(built.Any(m=>m.content=="my name Вася"),"tool mode retains past chat");Check(built.Any(m=>m.content=="read repo and compare"),"tool mode retains current question");Check(built.Any(m=>m.content.Contains("first tool result"))&&built.Any(m=>m.content.Contains("second tool result")),"tool transcript accumulates all results");
var tight=new ToolConversation(new(){new("system","rules"),new("user","original question")},"");tight.Add("first",new string('a',3000));tight.Add("second","latest result");Check(tight.Build(1024,200).Any(m=>m.content=="original question"),"compaction never removes initiating question");Check(tight.Build(1024,200).Any(m=>m.content.Contains("latest result")),"compaction retains newest tool result");
ArtifactWriter.Validate("hello.bat","@echo off\necho Hello");Check(System.Text.Encoding.UTF8.GetString(ArtifactWriter.Encode("hello.bat","@echo off\necho Hello")).Contains("\r\n"),"BAT encoding uses CRLF");Reject(()=>ArtifactWriter.Validate("hello.bat","import os\nprint('oops')"),"Python disguised as BAT rejected");Reject(()=>ArtifactWriter.Validate("../bad.txt","a"),"artifact path traversal rejected");Check(FileTools.Artifacts("```batch\n@echo off\necho test\n```").Any(f=>f.name.EndsWith(".bat")),"batch fences exported as BAT");
Check(WorkspaceCore.ParseTool("{\"tool\":\"mcp_call\",\"server\":\"memory\",\"name\":\"search_nodes\",\"arguments\":{\"query\":\"Vasya\"}}")?.arguments.GetProperty("query").GetString()=="Vasya","MCP structured arguments preserved");Check(!WorkspaceCore.NeedsApproval(2,"mcp_call",true)&&WorkspaceCore.NeedsApproval(1,"mcp_call",true),"MCP follows auto/review policy");Reject(()=>McpCatalog.Find("evil"),"unknown servers rejected");
string folder=Path.Combine(Path.GetTempPath(),"nora-mcp-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
try{
 string file=Path.Combine(folder,"chat.json");chat.Messages[^3].ToolResults.Add("actual tool result");Disk.Save(file,chat);Check(Disk.Read<ChatSession>(file)!.MemoryNote==chat.MemoryNote,"pinned memory survives restart");Check(Disk.Read<ChatSession>(file)!.Messages.Any(m=>m.ToolResults.Contains("actual tool result")),"tool outcomes survive restart");
 var info=McpCatalog.Start(McpCatalog.Find("filesystem"),folder,folder,true);Check(info.ArgumentList.Concat(new[]{info.Arguments}).Any(x=>x.Contains("@modelcontextprotocol/server-filesystem@2.1.0")),"MCP invocation uses fixed package version");Reject(()=>McpCatalog.Start(McpCatalog.Find("filesystem"),folder,Path.GetPathRoot(folder)!,false),"whole disk access rejected");
 string mock=Path.Combine(folder,"mock.cjs");File.WriteAllText(mock,"""
const rl=require('readline').createInterface({input:process.stdin});let denied=false;
const send=o=>process.stdout.write(JSON.stringify(o)+'\n');
rl.on('line',s=>{const q=JSON.parse(s);if(q.id==='server-1'){denied=q.error?.code===-32601;return;}
if(!q.id)return;
if(q.method==='initialize'){send({jsonrpc:'2.0',id:'server-1',method:'sampling/createMessage',params:{}});send({jsonrpc:'2.0',id:q.id,result:{protocolVersion:'2024-11-05',capabilities:{tools:{}},serverInfo:{name:'mock',version:'1'}}});}
if(q.method==='tools/list')send({jsonrpc:'2.0',id:q.id,result:{tools:[{name:'echo',description:'Echo test',annotations:{readOnlyHint:true},inputSchema:{type:'object',properties:{message:{type:'string'}},required:['message']}}]}});
if(q.method==='tools/call'){if(q.params.arguments.message==='stall')return;send({jsonrpc:'2.0',id:q.id,result:{content:[{type:'text',text:q.params.arguments.message+' samplingDenied='+denied}]}});}
});
""");
 ProcessStartInfo Mock(){var p=new ProcessStartInfo("node"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};p.ArgumentList.Add(mock);return p;}
 using(var c=new McpClient(Mock())){await c.Initialize(CancellationToken.None);Check(c.Tools.Single().Name=="echo","mock MCP full handshake and schema");Check(c.Tools.Single().ReadOnlyHint,"server read-only annotation preserved through real tools/list");Check((await c.Call("echo",J("{\"message\":\"hello\"}"),CancellationToken.None)).Contains("hello samplingDenied=true"),"server sampling requests denied and tool call works");var parsed=WorkspaceCore.ParseTool("{\"name\":\"echo\",\"arguments\":{\"message\":\"last-user-314\"}}",c.Tools.Select(t=>(Server:"mock",Name:t.Name)))!;Check(parsed.tool=="mcp_call"&&parsed.server=="mock","model native call normalized against actual tools/list");Check((await c.Call(parsed.name,parsed.arguments,CancellationToken.None)).Contains("last-user-314"),"normalized model call executes through real MCP transport");Reject(()=>c.Call("nonexistent",J("{}"),CancellationToken.None).GetAwaiter().GetResult(),"unknown remote tool rejected");Reject(()=>c.Call("echo",J("{}"),CancellationToken.None).GetAwaiter().GetResult(),"required arguments enforced");}
 using(var c=new McpClient(Mock())){await c.Initialize(CancellationToken.None);using var stop=new CancellationTokenSource(300);try{await c.Call("echo",J("{\"message\":\"stall\"}"),stop.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"MCP cancellation interrupts hanging tool and kills process");}}
 if(args.Contains("--live")){
  foreach(string id in new[]{"memory","filesystem","context7","playwright"}){
   using var c=new McpClient(McpCatalog.Start(McpCatalog.Find(id),folder,folder,false));using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));await c.Initialize(timeout.Token);Check(c.Tools.Count>0,id+" live initialize + tools/list");
   File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"verification-3.5",id+"-tools.json"),JsonSerializer.Serialize(c.Tools.Select(t=>new{t.Name,t.Description}),Disk.Json));
   if(id=="memory"){
    string made=await c.Call("create_entities",J("{\"entities\":[{\"name\":\"NoraTest\",\"entityType\":\"test\",\"observations\":[\"roundtrip-ok\"]}]}"),timeout.Token);Check(!made.Contains("MCP TOOL FAILED"),"memory live create entity");Check((await c.Call("search_nodes",J("{\"query\":\"NoraTest\"}"),timeout.Token)).Contains("roundtrip-ok"),"memory live search stored observation");
   }
   if(id=="filesystem"){
    string test=Path.Combine(folder,"mcp-roundtrip.txt");string write=await c.Call("write_file",J(JsonSerializer.Serialize(new{path=test,content="nora-roundtrip"})),timeout.Token);Check(File.ReadAllText(test)=="nora-roundtrip"&&!write.Contains("MCP TOOL FAILED"),"filesystem live write");Check((await c.Call("read_text_file",J(JsonSerializer.Serialize(new{path=test})),timeout.Token)).Contains("nora-roundtrip"),"filesystem live read");
   }
  }
 }
}finally{Directory.Delete(folder,true);}
Console.WriteLine($"{passed} memory/MCP checks passed");
