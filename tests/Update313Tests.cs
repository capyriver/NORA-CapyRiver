using LocalChat;
using System.Text.Json;
int passed=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
foreach(var (q,id) in new[]{("подключи Cloudflare","cloudflare"),("Connect GitHub","github"),("давай подключи ноушен","notion"),("переподключи Cloudflare","cloudflare"),("подключить Cloudflare","cloudflare")})Check(AgentRouting.ConnectionIntent(q)==id,"explicit connection: "+q);
foreach(var q in new[]{"ты уже подключен к клоудфлор?","создай сайт и загрузи на клоудфлор ты подключен уже","привет ты подключен к Cloudflare через MCP верно?","не подключай Cloudflare","Do not connect GitHub","создай сайт","Cloudflare подключён","подключи Cloudflare и создай сайт"})Check(AgentRouting.ConnectionIntent(q)==null,"task/status not swallowed: "+q);
foreach(var q in new[]{"Привет","Сколько будет 2+2?","Объясни рекурсию","создай сайт и дай готовый ZIP","напиши код калькулятора","сделай скрипт Python","create website for cafe"})Check(!AgentRouting.NeedsTools(q),"code generation does not browse: "+q);
foreach(var q in new[]{"создай сайт и загрузи на Cloudflare","напиши код и опубликуй сайт","открой браузер","создай сайт используя браузер"})Check(AgentRouting.NeedsTools(q),"real external tasks keep tools: "+q);
foreach(var tool in WorkspaceCore.Tools){Check(WorkspaceCore.NeedsApproval(0,tool,true),"always ask: "+tool);Check(!WorkspaceCore.NeedsApproval(2,tool,true),"auto really allows: "+tool);}
foreach(var tool in new[]{"browser_click","browser_type","browser_look","code_check","mcp_call"})Check(WorkspaceCore.NeedsApproval(1,tool,true),"review retains sensitive approval: "+tool);
Check(WorkspaceCore.NeedsApproval(2,"screen_capture",false),"whole screen independently gated");
var progress=new AgentProgress();var action=new ToolCall("browser_snapshot");Check(progress.Accept(action),"first call with undefined arguments serialized safely");Check(progress.Accept(action),"one retry allowed");Check(!progress.Accept(action),"third identical call prevented");Check(progress.Accept(new("browser_open",url:"https://example.com")),"distinct call permitted");Check(progress.Invalid()&&progress.Invalid()&&!progress.Invalid(),"malformed tool retries bounded");
Check(progress.Accept(action),"snapshot after a distinct action is permitted");Check(progress.Accept(new("browser_click",index:2))&&progress.Accept(action),"real navigation resets consecutive repeat guard");Check(progress.Result("TOOL FAILED: one")&&progress.Result("TOOL FAILED: two")&&!progress.Result("MCP TOOL FAILED: three"),"three failed actions stop the loop");Check(progress.Result("MCP TOOL RESULT: ok"),"success resets failure counter");
using var json=JsonDocument.Parse("{\"key\":1}");var mcp=new ToolCall("mcp_call",server:"cloudflare",name:"execute",arguments:json.RootElement.Clone());Check(progress.Accept(mcp)&&progress.Accept(mcp)&&!progress.Accept(mcp),"MCP repetition guarded");
string src=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../src"));
if(!Directory.Exists(src))src=Path.GetFullPath("src");
var host=File.ReadAllText(Path.Combine(src,"Program.cs"));var agent=File.ReadAllText(Path.Combine(src,"AgentWorkspace.cs"));var browser=File.ReadAllText(Path.Combine(src,"BrowserActions.cs"));
Check(!agent.Contains("MessageBox.Show"),"agent uses no native approval dialogs");Check(!File.ReadAllText(Path.Combine(src,"McpWorkspace.cs")).Contains("MessageBox.Show"),"MCP uses no native approval dialogs");
Check(browser.Contains("if(CurrentUrl!=\"about:blank\")await Validate(CurrentUrl)"),"controlled blank start readable without HTTPS network validation");
Check(!File.ReadAllText(Path.Combine(src,"StartupView.cs")).Contains("--disable-gpu"),"UI acceleration not forced off");Check(!agent.Contains("\\\"text\\\":\\\"hello\\\""),"no literal demo typing instruction");
Check(host.Contains("mode==1&&settings.Mode==3")&&!host.Contains("mode>0&&settings.Mode==3"),"automation not blocked by nonthinking model");
Check(File.ReadAllText(Path.Combine(src,"WindowWorkspace.cs")).Contains("FormBorderStyle=FormBorderStyle.None"),"native white frame removed");Check(host.Contains("TotalMilliseconds>=80"),"host stream events throttled");
Console.WriteLine($"{passed} update 3.13 checks passed");
