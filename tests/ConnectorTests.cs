using LocalChat;
using System.Diagnostics;
using System.Text.Json;
int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}void Reject(Action action,string name){try{action();}catch{Check(true,name);return;}throw new Exception("Not rejected: "+name);}
Check(McpCatalog.All.Any(p=>p.Id=="github")&&!McpCatalog.All.Any(p=>p.Id=="vapi"),"GitHub added; calling provider removed");Check(McpCatalog.All.Length==16,"16 explicit connector presets, no inflated tool count");
var imported=McpCatalog.Import("""{"mcpServers":{"MyDocs":{"url":"https://learn.microsoft.com/api/mcp"},"MyGitHub":{"type":"http","url":"https://api.githubcopilot.com/mcp/x/all/readonly","headers":{"Authorization":"Bearer ${MY_GH_PAT}"}},"Local":{"command":"npx","args":["-y","@modelcontextprotocol/server-memory@2.0.1"],"env":{"MEMORY_FILE_PATH":"${NORA_MEMORY_PATH}"}}}}""");
Check(imported.Count==3&&imported[2].Command=="npx","standard remote + stdio MCP config imported");Check(imported.Select(x=>x.Id).Distinct().Count()==3,"stable independent imported IDs");Check(McpCatalog.Import("""{"servers":{"a":{"url":"https://example.com/mcp"}}}""").Count==1,"VS Code servers format supported");
Reject(()=>McpCatalog.Import("""{"mcpServers":{"bad":{"url":"https://example.com/mcp?token=abc"}}}"""),"URL credentials/query rejected");Reject(()=>McpCatalog.Import("""{"bad":{"url":"http://example.com/mcp"}}"""),"insecure HTTP config rejected");Reject(()=>McpCatalog.Import("""{"bad":{"command":"cmd.exe","args":["/c","echo hello"]}}"""),"shell wrapper config rejected");
Reject(()=>McpCatalog.Import("""{"bad":{"command":"node","env":{"API_KEY":"plaintext-secret"}}}"""),"plaintext env secrets rejected");Reject(()=>McpCatalog.Import("""{"bad":{"url":"https://example.com/mcp","headers":{"Authorization":"Bearer plaintext"}}}"""),"plaintext auth header rejected");Reject(()=>McpCatalog.Import("""{"bad":{"command":"npx","args":["package","--api-key","secret"]}}"""),"CLI secret flag rejected");
using(var schema=JsonDocument.Parse("{}")){
 var indexed=Enumerable.Range(0,450).Select(i=>("bulk","Bulk",new McpTool("test_"+i,"Test-only fixture",schema.RootElement.Clone()))).Concat(new[]{("github","GitHub",new McpTool("search_repositories","Find repository by name",schema.RootElement.Clone())),("linear","Linear",new McpTool("create_issue","Create task",schema.RootElement.Clone()))}).ToArray();
 Check(McpToolSearch.Find(indexed,"найди репозиторий").Any(t=>t.Name=="search_repositories"),"Russian query routes to real English tool name");
 Check(McpToolSearch.Find(indexed,"test_449").First().Name=="test_449","query finds function beyond 300 without loading schemas");
 Check(McpToolSearch.Find(indexed,"",limit:12).Length==12,"model search bounded to 12 matches");Check(McpToolSearch.Find(indexed,"",server:"linear").Single().Name=="create_issue","server search isolation");
}
string folder=Path.Combine(Path.GetTempPath(),"nora-connectors-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
try{
 var preset=McpCatalog.Find("github");var info=McpCatalog.Start(preset,folder,"",true,null,"test-only-not-a-real-token",false);Check(!info.Arguments.Contains("test-only"),"PAT absent from process command line");Check(info.Environment["NORA_MCP_AUTH"]=="Bearer test-only-not-a-real-token","PAT delivered only to connector env");Check(info.Arguments.Contains("/x/all/readonly"),"GitHub read-only by default");Check(McpCatalog.Start(preset,folder,"",true,null,"test-only",true).Arguments.Contains("/x/all\""),"GitHub write requires explicit mode");
 string mock=Path.Combine(folder,"large.cjs");File.WriteAllText(mock,"""
const rl=require('readline').createInterface({input:process.stdin});const send=o=>process.stdout.write(JSON.stringify(o)+'\n');
rl.on('line',s=>{const q=JSON.parse(s);if(!q.id)return;if(q.method==='initialize')send({jsonrpc:'2.0',id:q.id,result:{protocolVersion:'2024-11-05',capabilities:{tools:{}},serverInfo:{name:'large-test-only',version:'1'}}});if(q.method==='tools/list'){let offset=q.params.cursor?250:0;let tools=Array.from({length:offset?200:250},(_,i)=>({name:'fixture_'+(offset+i),description:'Test-only fixture, not an advertised feature',inputSchema:{type:'object'}}));send({jsonrpc:'2.0',id:q.id,result:{tools,...(!offset?{nextCursor:'page2'}:{})}})}if(q.method==='tools/call')send({jsonrpc:'2.0',id:q.id,result:{content:[{type:'text',text:q.params.name+' executed'}]}})});
""");
 var pi=new ProcessStartInfo("node"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};pi.ArgumentList.Add(mock);using(var client=new McpClient(pi)){await client.Initialize(CancellationToken.None);Check(client.Tools.Count==450,"450 mock tools loaded with pagination (not counted as shipped features)");Check(client.Describe("fixture_449").Name=="fixture_449","tool past first 300 remains discoverable");using var j=JsonDocument.Parse("{}");Check((await client.Call("fixture_449",j.RootElement,CancellationToken.None)).Contains("fixture_449 executed"),"tool #450 invocable by exact schema/name");}
 if(args.Contains("--live")){
  foreach(var id in new[]{"deepwiki","microsoft","cloudflare-docs"}){
   using var c=new McpClient(McpCatalog.Start(McpCatalog.Find(id),folder,"",false));using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));await c.Initialize(timeout.Token);Check(c.Tools.Count>0,id+" live remote initialize + discovery");
   File.WriteAllText("verification-3.6/remote-"+id+".json",JsonSerializer.Serialize(c.Tools.Select(t=>new{t.Name,t.Description}),Disk.Json));
   if(id=="microsoft"){
    var search=c.Describe("microsoft_docs_search");Check(search.Schema.GetProperty("properties").TryGetProperty("query",out _),"Microsoft search schema exposes query");using var j=JsonDocument.Parse("{\"query\":\"C# .NET 8 getting started\"}");string result=await c.Call(search.Name,j.RootElement,timeout.Token);Check(!result.StartsWith("MCP TOOL FAILED")&&result.Contains("learn.microsoft.com"),"Microsoft actual documentation search succeeds");
   }

  }
 }
}finally{Directory.Delete(folder,true);}
Console.WriteLine($"{passed} connector checks passed");
