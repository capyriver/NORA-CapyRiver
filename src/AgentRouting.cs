using System.Text.Json;
using System.Text.RegularExpressions;
namespace LocalChat;
static class AgentRouting {
 public static string? ConnectionIntent(string question){
  string q=question.Trim().ToLowerInvariant();
  if(!Regex.IsMatch(q,@"^(?:(?:пожалуйста|давай)\s+)?(?:подключи(?:ть)?|соедини|connect|reconnect|переподключи)\b"))return null;
  if(Regex.IsMatch(q,@"созда|загрузи|опублику|deploy|сделай"))return null;
  foreach(var (pattern,id) in new[]{("cloudflare|клоудфл|клаудфл","cloudflare"),("github|гитхаб","github"),("notion|ноушен|нотион","notion"),("linear|линеар","linear"),("memory bank|мемори банк","memory-bank"),("aim|граф памяти","aim-memory")})if(Regex.IsMatch(q,pattern))return id;
  return null;
 }
 public static bool RequiresExternalAction(string question)=>Regex.IsMatch(question,@"(?is)опублику|запусти.*(?:домен|cloudflare|клоуд|клауд)|(?:загрузи|разверни|deploy|publish).*(?:сайт|cloudflare|клоуд|клауд|workers|pages)|(?:сделай|создай).*(?:на\s+(?:cloudflare|клоуд|клауд))|(?:напиши|отправь).*в\s+(?:телеграм|telegram)");
 public static bool NeedsBrowser(string question)=>Regex.IsMatch(question,@"(?is)браузер|browser|вкладк|текущ.*страниц|на.*странице|(?:прочитай|посмотри).*страниц|что.*видишь|нажми|кликни|введи|прокрут|current page|\bclick\b|\bscroll\b");
 public static bool NeedsTools(string question){
  bool external=Regex.IsMatch(question,@"(?is)cloudflare|клоудфл|клаудфл|github|гитхаб|notion|ноушен|mcp|мсп|мцп|браузер|browser|что.*видишь|на.*странице|открой|отправь|найди|поищи|погугли|опублику|загрузи|deploy|publish|сохрани|запомни|вспомни|скопируй|снимок|скриншот|snapshot|screenshot|вкладк|текущ.*страниц|страниц.*браузер|current page|dns|домен|workers|\br2\b|репозитор|google drive|гугл драйв|проверь.*(?:файл|код)|create_file");
  return external;
 }

}
sealed class AgentProgress {
 string last="";int repeats,invalid,failures;
 public bool Accept(ToolCall call){string key=JsonSerializer.Serialize(new{call.tool,call.query,call.url,call.index,call.artifactId,call.server,call.name,call.text,args=call.arguments.ValueKind==JsonValueKind.Undefined?"":call.arguments.GetRawText()},Disk.Json);repeats=key==last?repeats+1:1;last=key;return repeats<=2;}
 public bool Invalid()=>++invalid<3;
 public bool Result(string result,int maximumFailures=3){bool failed=result.StartsWith("TOOL FAILED:")||result.StartsWith("MCP TOOL FAILED");failures=failed?failures+1:0;return failures<maximumFailures;}
}
