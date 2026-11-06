using System.Reflection;
using System.Text.Json;
namespace LocalChat;
sealed class NoraAgentPackage {public string Name {get;set;}="";public string Instructions {get;set;}="";public List<AgentProfile> Specializations {get;set;}=new();}
static class BundledAgents {
 public static string Instructions {get;private set;}="";
 public static List<AgentProfile> Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("LocalChat.nora-agent.json");if(s==null)return new();var package=JsonSerializer.Deserialize<NoraAgentPackage>(s,Disk.Json)??throw new Exception("Нет пакета НОРА Агент");if(package.Name!="НОРА Агент"||package.Specializations.Count!=429||package.Instructions.Length>2000)throw new Exception("Повреждён единый пакет НОРА Агент");Instructions=package.Instructions;var agents=package.Specializations;if(agents.Count>600||agents.Any(a=>!(a.Id.StartsWith("agency-")||a.Id.StartsWith("volt-"))||a.Prompt.Length>1800||a.Name.Length>180||a.Category.Length>80||!(a.Source.StartsWith("https://github.com/msitarzewski/agency-agents/blob/")||a.Source.StartsWith("https://github.com/VoltAgent/awesome-claude-code-subagents/blob/")))||agents.Select(a=>a.Id).Distinct().Count()!=agents.Count)throw new Exception("Некорректный встроенный каталог агентов.");return agents;}
}
