using LocalChat;
using System.Diagnostics;
using System.Text.Json;
int passed=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
void Reject(Action a,string label){try{a();}catch{Check(true,label);return;}throw new Exception("Not rejected: "+label);}
var root=Path.Combine(Path.GetTempPath(),"nora311-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try{
 await McpRuntime.CheckNode(false,CancellationToken.None);Check(true,"actual Node version preflight");
 string a=McpRuntime.AuthDirectory(root,"cloudflare"),b=McpRuntime.AuthDirectory(root,"notion");Check(a!=b,"separate OAuth storage per connector");
 if(!OperatingSystem.IsWindows())Check(File.GetUnixFileMode(a)==(UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute),"OAuth directory user-only Unix permission");
 Reject(()=>McpRuntime.AuthDirectory(root,"../escape"),"auth path traversal rejected");Reject(()=>McpRuntime.AuthDirectory(root,"cloudflare/child"),"auth slash rejected");
 var pi=McpCatalog.Start(McpCatalog.Find("cloudflare"),root,"",false);Check(pi.Environment["MCP_REMOTE_CONFIG_DIR"]==a,"bridge receives isolated auth directory");Check(pi.ArgumentList.Contains("https://mcp.cloudflare.com/mcp"),"Cloudflare API endpoint preconfigured");Check(pi.ArgumentList.Contains("mcp-remote@0.15.2"),"pinned OAuth bridge not latest");Check(pi.ArgumentList.Contains("--silent")&&!pi.ArgumentList.Contains("--debug"),"no auth debug logging");
 File.WriteAllText(Path.Combine(a,"mock-token.json"),"fixture only");McpRuntime.ForgetAuth(root,"cloudflare");Check(!Directory.Exists(a)&&Directory.Exists(b),"forget only chosen server");
 string target=Path.Combine(root,"outside");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"keep"),"keep");Directory.CreateSymbolicLink(Path.Combine(root,"mcp-auth","unsafe"),target);Reject(()=>McpRuntime.AuthDirectory(root,"unsafe"),"symlink auth directory rejected");Reject(()=>McpRuntime.ForgetAuth(root,"unsafe"),"forget symlink blocked");Check(File.Exists(Path.Combine(target,"keep")),"unrelated data preserved");Directory.Delete(Path.Combine(root,"mcp-auth","unsafe"));
 // Exercise the exact portable-node branch without executing a Windows binary on Linux.
 var portable=Path.Combine(AppContext.BaseDirectory,"runtime","node");Directory.CreateDirectory(Path.Combine(portable,"node_modules","npm","bin"));File.WriteAllText(Path.Combine(portable,"node.exe"),"test fixture");File.WriteAllText(Path.Combine(portable,"node_modules","npm","bin","npx-cli.js"),"test fixture");
 try{var info=McpCatalog.Npx(root,new(){"-y",McpCatalog.Bridge,"https://mcp.cloudflare.com/mcp"},true);Check(info.FileName==Path.Combine(portable,"node.exe"),"portable node selected before system");Check(info.ArgumentList[0].EndsWith("npx-cli.js"),"npm invoked directly, no cmd quoting");Check(info.Environment["PATH"]!.StartsWith(portable),"MCP children find portable Node in PATH");}finally{Directory.Delete(Path.Combine(AppContext.BaseDirectory,"runtime"),true);}
 string mock=Path.Combine(root,"mock.cjs");File.WriteAllText(mock,"""
const rl=require('readline').createInterface({input:process.stdin});rl.on('line',s=>{const q=JSON.parse(s);if(q.method==='initialize')console.log(JSON.stringify({jsonrpc:'2.0',id:q.id,result:{protocolVersion:'2025-11-25',capabilities:{tools:{}},serverInfo:{name:'test',version:'1'}}}));if(q.method==='tools/list')console.log(JSON.stringify({jsonrpc:'2.0',id:q.id,result:{tools:[{name:'echo',inputSchema:{type:'object',required:['text']}}]}}));if(q.method==='tools/call')console.log(JSON.stringify({jsonrpc:'2.0',id:q.id,result:{content:[{type:'text',text:q.params.arguments.text}]}}));});
""");
 ProcessStartInfo Start(){var i=new ProcessStartInfo("node"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};i.ArgumentList.Add(mock);return i;}
 using(var c=new McpClient(Start())){await c.Initialize(CancellationToken.None);Check(c.Active&&c.Tools.Count==1,"real stdio handshake accepts 2025-11-25");using var j=JsonDocument.Parse("{\"text\":\"hello\"}");Check((await c.Call("echo",j.RootElement,CancellationToken.None)).Contains("hello"),"actual stdio tool call");using var bad=JsonDocument.Parse("{}");bool rejected=false;try{await c.Call("echo",bad.RootElement,CancellationToken.None);}catch{rejected=true;}Check(rejected,"required argument validation before invocation");}
 File.WriteAllText(mock,"setInterval(()=>{},1000)");using(var c=new McpClient(Start())){using var cancel=new CancellationTokenSource(200);try{await c.Initialize(cancel.Token);throw new Exception("Cancellation did not occur");}catch(OperationCanceledException){Check(!c.Active,"cancel kills pending MCP process");}}
 using(var c=new McpClient(Start())){try{await c.Request("initialize",new{},CancellationToken.None,1);throw new Exception("Timeout did not occur");}catch(TimeoutException){Check(!c.Active,"timeout kills MCP without fake connected state");}}
}finally{Directory.Delete(root,true);}
Console.WriteLine($"{passed} update311 checks passed");
