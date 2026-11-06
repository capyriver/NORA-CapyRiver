using LocalChat;
using System.Text.Json;
using System.Net;
int passed=0;void Check(bool ok,string n){if(!ok)throw new Exception(n);passed++;Console.WriteLine("PASS "+n);}
void Reject(Action a,string n){try{a();}catch{Check(true,n);return;}throw new Exception("Not rejected: "+n);}
Check(BrowserAddress.Input("github.com/a/b")=="https://github.com/a/b","bare domain navigates rather than searches");
Check(BrowserAddress.Input("http://github.com")=="https://github.com/","typed HTTP upgrades to HTTPS");
Check(BrowserAddress.Input("как создать репозиторий").Contains("google.com/search"),"words remain a search");
foreach(var url in new[]{"javascript:alert(1)","file:///C:/Windows","data:text/html,test","https://name:pass@example.com","https://example.com:8443"})Reject(()=>BrowserAddress.Input(url),"unsafe address rejected "+url);
Check(BrowserAddress.Resource("wss://example.com/socket").Scheme=="https","secure WebSocket validates same host policy");
foreach(var ip in new[]{"127.0.0.1","192.168.1.1","10.0.0.1","::1","fe80::1","::ffff:192.168.1.1"})Check(!WebReader.Public(IPAddress.Parse(ip)),"private address blocked "+ip);
Check(WebReader.Public(IPAddress.Parse("8.8.8.8"))&&WebReader.Public(IPAddress.Parse("2606:4700:4700::1111")),"public IPv4 and IPv6 accepted");
var chat=new ChatSession();chat.Messages.Add(new(){Role="user",Text=new string('x',2500)+" Моя видеокарта RTX 4070, рабочая папка Alpha."});chat.Messages.Add(new(){Role="assistant",Text="Понял"});
Check(ChatMemory.Recall(chat,"Какая видеокарта?").Contains("RTX 4070"),"recall finds actual match after long prefix even in recent history");
Check(ChatMemory.Search(chat,"видеокарты").Contains("RTX 4070"),"Russian inflection and centered excerpt");Check(!ChatMemory.Recall(new(),"видеокарта").Contains("4070"),"no cross-chat leakage");
var list=new List<ChatMessage>{new("system","rules"),new("user",new string('a',20000))};Check(ContextWindow.Fit(list,24576,8192)==0,"24K context accommodates long prompt plus 8K output");Reject(()=>ContextWindow.Fit(list,512,512),"invalid output/context budget rejected");
var config=new Settings{Mode=2,ContextTokens=4096,MaxTokens=512};Check(TokenProfile.Apply(config)&&config.MaxTokens==1024&&config.ContextTokens==4096,"one-time output upgrade does not increase context/RAM");config.MaxTokens=512;Check(!TokenProfile.Apply(config)&&config.MaxTokens==512,"later user token settings preserved");var air=new Settings{Mode=0,MaxTokens=512};TokenProfile.Apply(air);Check(air.MaxTokens==512,"AirLLM token defaults not raised");
using(var empty=JsonDocument.Parse("{}")){var index=new[]{("aim-memory","AIM",new McpTool("aim_memory_store","Store memories",empty.RootElement.Clone()))};Check(McpToolSearch.Find(index,"запомни факт").Length==1,"Russian memory query finds persistent tool");}
string root=Path.Combine(Path.GetTempPath(),"nora37-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try{
 foreach(string id in new[]{"memory-bank","aim-memory"}){var info=McpCatalog.Start(McpCatalog.Find(id),root,root,false);Check(info.ArgumentList.Any(x=>x.Contains("@"+McpCatalog.Find(id).Version)),id+" pinned package");}
 Check(Directory.Exists(Path.Combine(root,"aim-memory"))&&Directory.Exists(Path.Combine(root,"memory-bank")),"dedicated persistence roots created");
 if(args.Contains("--live"))foreach(string id in new[]{"memory-bank","aim-memory"}){
  using var ct=new CancellationTokenSource(TimeSpan.FromMinutes(3));
  using(var client=new McpClient(McpCatalog.Start(McpCatalog.Find(id),root,root,false))){await client.Initialize(ct.Token);Check(client.Tools.Count>0,id+" live discovery");File.WriteAllText("verification-3.7/"+id+"-schema.json",JsonSerializer.Serialize(client.Tools,Disk.Json));
   string tool=id=="memory-bank"?"memory_bank_write":"aim_memory_store";
   string json=id=="memory-bank"?"{\"projectName\":\"NoraRegression\",\"fileName\":\"context.md\",\"content\":\"Nora37-persistent-test\"}":"{\"entities\":[{\"name\":\"NoraRegression\",\"entityType\":\"test\",\"observations\":[\"Nora37-persistent-test\"]}]}";
   using var doc=JsonDocument.Parse(json);string written=await client.Call(tool,doc.RootElement,ct.Token);Console.WriteLine(id+" write "+written);Check(!written.Contains("MCP TOOL FAILED"),id+" live write");}
  using(var client=new McpClient(McpCatalog.Start(McpCatalog.Find(id),root,root,false))){await client.Initialize(ct.Token);using var doc=JsonDocument.Parse(id=="memory-bank"?"{\"projectName\":\"NoraRegression\",\"fileName\":\"context.md\"}":"{\"query\":\"NoraRegression\"}");string found=await client.Call(id=="memory-bank"?"memory_bank_read":"aim_memory_search",doc.RootElement,ct.Token);Console.WriteLine(id+" read "+found);Check(found.Contains("Nora37-persistent-test"),id+" persisted across process restart");}
 }
}finally{Directory.Delete(root,true);}
Console.WriteLine($"{passed} regression checks passed");
