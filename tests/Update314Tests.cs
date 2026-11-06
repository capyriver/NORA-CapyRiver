using LocalChat;
using System.Text.Json;
int passed=0;void Check(bool b,string label){if(!b)throw new Exception("FAIL "+label);passed++;Console.WriteLine("PASS "+label);}
string root=args.Length>0?args[0]:Directory.GetCurrentDirectory();
var chat=new ChatSession();
chat.Messages.Add(new(){Role="user",Text="Создай сайт кафе Лаванда на Cloudflare"});chat.Messages.Add(new(){Role="assistant",Text="ПЕРВЫЙ УСТАРЕВШИЙ ОТВЕТ",State="complete"});
chat.Messages.Add(new(){Role="user",Text="Не сайт. Сколько будет 9+8?"});chat.Messages.Add(new(){Role="assistant",Text="17",State="complete"});
chat.Messages.Add(new(){Role="user",Text="Теперь объясни почему листья зелёные"});chat.Messages.Add(new(){Role="assistant",Text="",State="streaming"});
var history=Disk.History(chat);Check(history.Count==4,"streaming turn excluded from history");Check(history[0].content.Contains("Лаванда"),"history remains chronological");Check(history[^1].content=="17","latest complete answer retained");
Check(ChatMemory.Recall(chat,"Создай сайт кафе другой")=="","old commands not reinserted by term overlap");chat.MemoryNote="Имя пользователя: Иван";Check(ChatMemory.Recall(chat,"Новая задача").Contains("Иван"),"pinned note retained");Check(!ChatMemory.Recall(chat,"Новая задача").Contains("Лаванда"),"pinned note does not resurrect old task");Check(ChatMemory.Recall(chat,"Вспомни что я писал про кафе Лаванда").Contains("Лаванда"),"explicit recall remains available");
Check(ChatMemory.CrossRecall(new[]{chat},"other","Сайт кафе Лаванда")=="","other-chat commands not automatically injected");Check(ChatMemory.CrossRecall(new[]{chat},"other","Вспомни прошлый чат Лаванда").Contains("Лаванда"),"cross recall requires explicit intent");Check(!new Settings().CrossChatMemory,"cross chat memory defaults off");
var feedback=new[]{new FeedbackEntry{ChatId=chat.Id,Question="Python pytest",Answer="old code",Note="wrong import",Rating=-1}};Check(FeedbackMemory.Context(feedback,"Листья зелёные",chat.Id)=="","unrelated ratings do not poison context");Check(FeedbackMemory.Context(feedback,"Python pytest",chat.Id).Contains("wrong import"),"relevant ratings retained");
foreach(var current in new[]{"Не сайт. Сколько будет 9+8?","да, сделай","Нет, используй Workers, не Pages","Проверь текущую страницу браузера","Ответь только словом ДА"}){
 var messages=new List<ChatMessage>{new("system","base")};messages.AddRange(history);ConversationInput.AppendCurrent(messages,current,"OLD QUOTE: Создай сайт кафе Лаванда");Check(messages[^1].role=="user"&&messages[^1].content==current,"latest request exact: "+current);Check(messages[0].content.Contains(ConversationInput.Priority),"priority always present");Check(!messages[^1].content.Contains("OLD QUOTE"),"memory separated from current user");
 var tc=new ToolConversation(messages,ToolProtocol.Instructions);tc.Add("{\"tool\":\"browser_snapshot\"}","URL: https://example.com; OLD PAGE DATA");var built=tc.Build(24576,1024);Check(built[^1].content.EndsWith(current),"latest request reanchored after tool result");Check(built.Any(m=>m.content==current),"original task retained during tools");
}
var many=new List<ChatMessage>{new("system","short")};for(int i=0;i<30;i++){many.Add(new("user","OLD_"+i+new string('x',200)));many.Add(new("assistant",new string('y',200)));}ConversationInput.AppendCurrent(many,"CURRENT_LAST_QUESTION","");int removed=ContextWindow.Fit(many,2048,256);Check(removed>0,"old pairs trimmed when budget exceeded");Check(many[^1].content=="CURRENT_LAST_QUESTION","trimming never discards current question");Check(!many.Any(m=>m.content.StartsWith("OLD_0")),"oldest turn removed first");
var conv=new ToolConversation(many,"");for(int i=0;i<12;i++)conv.Add("action "+i,new string('x',300));var compact=conv.Build(2048,256);Check(compact[^1].content.EndsWith("CURRENT_LAST_QUESTION"),"tool truncation keeps last request anchor");Check(compact[^1].content.Contains(new string('x',300)),"newest result preserved");
var cases=new[]{
 """{"tool":"browser_type","index":4,"text":"Лаванда"}""",
 """{"tool":"browser_type","arguments":{"index":4,"text":"Лаванда"}}""",
 """{"name":"browser_type","arguments":{"index":4,"text":"Лаванда"}}""",
 """{"tool":"mcp_call","server":"cloudflare","name":"execute","arguments":{"code":"return 1"}}""",
 """{"tool":"mcp_call","arguments":{"server":"cloudflare","name":"execute","arguments":{"code":"return 1"}}}""",
 """{"name":"mcp_call","arguments":{"server":"cloudflare","name":"execute","arguments":{"code":"return 1"}}}""",
 """{"tool":"mcp_describe_tool","arguments":{"server":"cloudflare","name":"execute"}}""",
 """{"tool":"mcp_search_tools","arguments":{"query":"workers deploy"}}""",
 """{"tool":"mcp_call","server":"cloudflare","name":"execute","arguments":"{\"code\":\"return 1\"}"}"""
};
foreach(var json in cases){var call=WorkspaceCore.ParseTool(json);Check(call!=null,"tool envelope parsed");if(call!.tool=="browser_type")Check(call.index==4&&call.text=="Лаванда","browser values survive normalization");if(call.tool=="mcp_call")Check(call.server=="cloudflare"&&call.name=="execute"&&call.arguments.GetProperty("code").GetString()=="return 1","real MCP server/name/arguments preserved");}
Check(WorkspaceCore.ParseTool("""{"code":"import cloudflare","verification_steps":"fake"}""")==null,"sample code JSON is not a tool execution");Check(ToolProtocol.Instructions.Contains("browser_snapshot")&&ToolProtocol.Instructions.Contains("browser_type"),"DOM tools explicitly listed");Check(ToolProtocol.Instructions.Contains("FLAT")&&ToolProtocol.Instructions.Contains("arguments (JSON object"),"MCP envelope unambiguous");
foreach(var q in new[]{"сделай сайт и опубликуй на cloudflare","запусти его на временном домене","deploy сайт","создай сайт на клоудфлор"})Check(AgentRouting.RequiresExternalAction(q),"real operation required: "+q);
foreach(var q in new[]{"Напиши код сайта","Сколько будет 2+2?","объясни MCP"})Check(!AgentRouting.RequiresExternalAction(q),"ordinary answer not blocked: "+q);
string program=File.ReadAllText(Path.Combine(root,"src/Program.cs")),agent=File.ReadAllText(Path.Combine(root,"src/AgentLoop.cs"));Check(!program.Contains("if(AgentRouting.NeedsTools(question)&&"),"short followups cannot bypass enabled tools");Check(program.Contains("ConversationInput.AppendCurrent"),"host uses tested current-input builder");Check(program.Contains("settings.CrossChatMemory=false;settings.ConversationRevision=1"),"old settings migrate away from automatic cross-chat injection");Check(agent.Contains("ExecuteMcp(call,token)")&&agent.Contains("new ToolCall(\"mcp_describe_tool\""),"actual cached schemas supplied, not invented SDK");Check(agent.Contains("NO VERIFIED COMPLETION"),"nonaction reply triggers repair");Check(agent.Contains("модель не выполнила внешний вызов"),"failure is explicit rather than fake deployment");Check(program.Contains("!response.TaskPending&&!AgentRouting.RequiresExternalAction(activeAgentTask?.Goal??question)&&response.Text.Contains"),"deployment pseudocode does not auto-create ZIP");
var actualTools=new[]{(Server:"cloudflare",Name:"execute"),(Server:"cloudflare",Name:"search")};
foreach(var direct in new[]{"""{"tool":"execute","arguments":{"code":"return 1"}}""","""{"name":"execute","arguments":{"code":"return 1"}}""","""{"function":{"name":"execute","arguments":"{\"code\":\"return 1\"}"}}"""}){var parsed=WorkspaceCore.ParseTool(direct,actualTools);Check(parsed?.tool=="mcp_call"&&parsed.server=="cloudflare"&&parsed.name=="execute","direct real remote tool resolves to host MCP call");}
bool ambiguous=false;try{WorkspaceCore.ParseTool("""{"tool":"execute","arguments":{}}""",new[]{(Server:"one",Name:"execute"),(Server:"two",Name:"execute")});}catch(InvalidOperationException){ambiguous=true;}Check(ambiguous,"ambiguous remote tools cannot cross accounts");
Check(WorkspaceCore.ParseTool("""{"function":{"name":"browser_snapshot","arguments":{}}}""")?.tool=="browser_snapshot","native function envelope supported");
Check(File.ReadAllText(Path.Combine(root,"src/McpWorkspace.cs")).Contains("settings.ToolMode=true;settings.UnifiedAgent=true;settings.ExperimentalProfileId=\"\";SaveSettings();"),"successful connection enables agent tools without changing consent mode");
Check(!AgentRouting.NeedsBrowser("Создай сайт кафе и опубликуй на Cloudflare"),"MCP deployment does not inject unrelated open browser page");
Check(AgentRouting.NeedsBrowser("Что видишь в браузере?"),"explicit browser observation gets fresh context");
Check(AgentRouting.NeedsBrowser("Нажми кнопку на текущей странице"),"browser action gets fresh context");
Check(File.ReadAllText(Path.Combine(root,"src/BrowserContext.cs")).Contains("NeedsBrowser(question)"),"host separates browser observation from MCP tasks");
Console.WriteLine($"{passed} update314 checks passed");
