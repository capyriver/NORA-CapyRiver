using System.Text.RegularExpressions;
namespace LocalChat;
partial class MainWindow {
 readonly Dictionary<string,RegistryEntry> registryEntries=new();
 string? ConnectionRequest(string question)=>AgentRouting.ConnectionIntent(question);
 async Task ConnectFromCard(string id){GetMcp(id);await ConnectMcp(id);}
 async Task SearchRegistry(string query,string cursor){Post("registryBusy",true);try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));var result=await ConnectionRegistry.Search(query,cursor,timeout.Token);if(cursor=="")registryEntries.Clear();foreach(var e in result.Entries)registryEntries[e.Id]=e;Post("registryResults",result);}finally{Post("registryBusy",false);}}
 async Task AddRegistryEntry(string id){if(!registryEntries.TryGetValue(id,out var entry))throw new Exception("Сначала найди сервер в каталоге");if(entry.Url=="")throw new Exception("У сервера нет готового HTTPS endpoint. Используй JSON-конфигурацию из документации сервера; каталог не придумывает команды запуска.");await AddMcpUrl(entry.Name,entry.Url);}
}
