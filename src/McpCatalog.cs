using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json;
namespace LocalChat;
sealed record McpPreset(string Id,string Name,string Description,string Source,string Package,string Version,string Requirement,string Url="",string Auth="none",string TokenEnv="");
sealed class McpCustom {
 public string Id {get;set;}="";public string Name {get;set;}="";public string Url {get;set;}="";public string Command {get;set;}="";public string[] Args {get;set;}=Array.Empty<string>();
 public Dictionary<string,string> Env {get;set;}=new();public Dictionary<string,string> Headers {get;set;}=new();
 public McpPreset Preset()=>new(Id,Name,"Импортированный MCP: список функций определяется сервером.",Url==""?"https://modelcontextprotocol.io/":Url,Command,"","Пользовательская конфигурация. Проверь источник, права и авторизацию.",Url,"auto");
}
sealed class McpConfiguration {public string[] Enabled {get;set;}=Array.Empty<string>();public string Folder {get;set;}="";public List<McpCustom> Custom {get;set;}=new();public bool GitHubWrite {get;set;}=false;}
static class McpCatalog {
 public const string Bridge="mcp-remote@0.15.2";
 public static readonly McpPreset[] All={
  new("cloudflare","Cloudflare · API аккаунта","Workers, DNS, R2 и другие операции, доступные аккаунту. Подтверждение зависит от режима доступа. В режиме автодействий операции выполняются без запроса.","https://developers.cloudflare.com/agents/model-context-protocol/mcp-servers-for-cloudflare","","","OAuth в браузере; нужны права на аккаунт Cloudflare. Это управление аккаунтом, не только поиск документации.","https://mcp.cloudflare.com/mcp","oauth"),
  new("github","GitHub","Репозитории, файлы, issues, pull requests, Actions и другие доступные toolsets.","https://github.com/github/github-mcp-server","","","Личный GitHub PAT с нужными правами. По умолчанию сервер только для чтения; запись включается отдельно.","https://api.githubcopilot.com/mcp/x/all/readonly","bearer","GITHUB_PERSONAL_ACCESS_TOKEN"),
  new("notion","Notion","Страницы, базы и поиск в разрешённом рабочем пространстве.","https://developers.notion.com/guides/mcp/get-started-with-mcp","","","Вход в Notion и согласие на доступ в браузере на твоём ПК. Доступ зависит от workspace policy.","https://mcp.notion.com/mcp","oauth"),
  new("linear","Linear","Задачи, проекты, комментарии и рабочие процессы команды.","https://linear.app/docs/mcp","","","OAuth в браузере на твоём ПК; права аккаунта Linear.","https://mcp.linear.app/mcp","oauth"),
  new("atlassian","Jira + Confluence","Задачи Jira, проекты и страницы Confluence через Atlassian Rovo MCP.","https://support.atlassian.com/atlassian-rovo-mcp-server/","","","Atlassian OAuth. Администратор может ограничить подключения и доступные инструменты.","https://mcp.atlassian.com/v2/mcp","oauth"),
  new("sentry","Sentry","Ошибки приложений, события и диагностика проектов.","https://docs.sentry.io/product/sentry-mcp/","","","OAuth Sentry; нужны доступы к организации и проектам.","https://mcp.sentry.dev/mcp","oauth"),
  new("huggingface","Hugging Face","Поиск моделей, datasets, статей и доступных AI-приложений.","https://github.com/huggingface/hf-mcp-server","","","OAuth/токен Hugging Face по требованиям сервиса. Внешние приложения могут иметь отдельные лимиты.","https://huggingface.co/mcp","oauth"),
  new("deepwiki","DeepWiki","Документация и вопросы о публичных GitHub-репозиториях.","https://docs.devin.ai/work-with-devin/deepwiki-mcp","","","Публичный удалённый сервер, без ключа. Запросы передаются DeepWiki.","https://mcp.deepwiki.com/mcp"),
  new("microsoft","Microsoft Learn","Поиск и чтение официальной документации Microsoft.","https://learn.microsoft.com/en-us/training/support/mcp","","","Публичный удалённый сервер, без ключа. Действуют условия Microsoft Learn.","https://learn.microsoft.com/api/mcp"),
  new("cloudflare-docs","Cloudflare Docs","Поиск по документации Cloudflare.","https://github.com/cloudflare/mcp-server-cloudflare","","","Публичный сервер документации. Это не управление аккаунтом Cloudflare.","https://docs.mcp.cloudflare.com/mcp"),
  new("context7","Context7","Актуальная документация библиотек для кода.","https://github.com/upstash/context7","@upstash/context7-mcp","4.4.4","Node.js 24+. Интернет; у сервиса могут быть лимиты."),
  new("memory","Память · граф знаний","Сущности, наблюдения и связи.","https://github.com/modelcontextprotocol/servers/tree/main/src/memory","@modelcontextprotocol/server-memory","2.0.1","Node.js 24+. Локальная база отдельно от истории чата."),
  new("memory-bank","Memory Bank · память проектов","Постоянные файлы памяти по проектам: читать, записывать и обновлять заметки.","https://github.com/alioshr/memory-bank-mcp","@allpepper/memory-bank-mcp","0.2.2","Node.js 24+. Локальная папка memory-bank. Включается отдельно; запись — с подтверждением."),
  new("aim-memory","AIM · постоянный граф памяти","Сущности, факты и связи; отдельные хранилища и поиск без облачной LLM.","https://github.com/shaneholloman/mcp-knowledge-graph","mcp-knowledge-graph","1.3.2","Node.js 24+. Локальная папка aim-memory; не обучение весов. Включается отдельно."),
  new("filesystem","Файлы · рабочая папка","Чтение, поиск и запись в выбранной папке.","https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem","@modelcontextprotocol/server-filesystem","2.1.0","Node.js 24+. Выбери отдельную рабочую папку, не весь диск."),
  new("playwright","Playwright · браузер","Навигация, формы, клики и тестирование в отдельном браузере.","https://github.com/microsoft/playwright-mcp","@playwright/mcp","0.0.83","Node.js 24+ и Google Chrome. Не управляет встроенным WebView NORA.")
 };
 public static McpPreset Find(string id)=>All.SingleOrDefault(p=>p.Id==id)??throw new Exception("Неизвестное MCP-подключение");
 public static Uri ValidateUrl(string text){
  if(!Uri.TryCreate(text,UriKind.Absolute,out var u)||u.Scheme!="https"||!u.IsDefaultPort||u.UserInfo!=""||u.Fragment!=""||u.Query!=""||Uri.CheckHostName(u.Host)!=UriHostNameType.Dns||u.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||!u.Host.Contains('.')||u.Host.EndsWith(".local",StringComparison.OrdinalIgnoreCase))throw new Exception("Нужен публичный HTTPS MCP URL без токена, query, нестандартного порта или fragment.");return u;
 }
 public static ProcessStartInfo Npx(string root,List<string> args,bool windows){
  var portable=McpRuntime.BundledDirectory(windows);
  if(portable!=null){
   var direct=new ProcessStartInfo(Path.Combine(portable,"node.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new System.Text.UTF8Encoding(false),StandardOutputEncoding=new System.Text.UTF8Encoding(false),StandardErrorEncoding=new System.Text.UTF8Encoding(false),WorkingDirectory=root};
   direct.ArgumentList.Add(Path.Combine(portable,"node_modules","npm","bin","npx-cli.js"));foreach(var a in args)direct.ArgumentList.Add(a);
   CleanEnvironment(direct);direct.Environment["PATH"]=portable+Path.PathSeparator+((direct.Environment.TryGetValue("PATH",out var oldPath)?oldPath:"")??"");return direct;
  }
  var info=new ProcessStartInfo{FileName=windows?"cmd.exe":"npx",UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new System.Text.UTF8Encoding(false),StandardOutputEncoding=new System.Text.UTF8Encoding(false),StandardErrorEncoding=new System.Text.UTF8Encoding(false),WorkingDirectory=root};
  if(windows){if(args.Any(a=>a.IndexOfAny(new[]{'"','%','!','&','|','<','>','^','\r','\n'})>=0))throw new Exception("Аргумент содержит символы, небезопасные для Windows cmd. Используй env-ссылки для секретов.");info.Arguments="/d /s /c \"npx "+string.Join(" ",args.Select(a=>"\""+a+"\""))+"\"";}else foreach(var a in args)info.ArgumentList.Add(a);
  CleanEnvironment(info);return info;
 }
 static void CleanEnvironment(ProcessStartInfo info){foreach(var key in info.Environment.Keys.Where(k=>Regex.IsMatch(k,"TOKEN|SECRET|API_KEY|PASSWORD|AUTH_HEADER",RegexOptions.IgnoreCase)).ToArray())info.Environment.Remove(key);}
 public static ProcessStartInfo Start(McpPreset p,string root,string folder,bool windows)=>Start(p,root,folder,windows,null,"",false);
 public static ProcessStartInfo Start(McpPreset p,string root,string folder,bool windows,McpCustom? custom,string bearer,bool githubWrite){
  ProcessStartInfo info;
  if(p.Url!=""){
   string url=p.Id=="github"&&githubWrite?"https://api.githubcopilot.com/mcp/x/all":p.Url;ValidateUrl(url);
   var args=new List<string>{"-y",Bridge,url,"--transport","http-first","--auth-timeout","240","--silent"};
   if(new[]{"HTTPS_PROXY","HTTP_PROXY","https_proxy","http_proxy"}.Any(k=>!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(k))))args.Add("--enable-proxy");
   var headers=custom?.Headers??new();int index=0;var bound=new Dictionary<string,string>();
   if(bearer!=""){args.AddRange(new[]{"--header","Authorization:${NORA_MCP_AUTH}"});bound["NORA_MCP_AUTH"]="Bearer "+bearer;}
   foreach(var pair in headers){string key="NORA_MCP_HEADER_"+index++;args.AddRange(new[]{"--header",pair.Key+":${"+key+"}"});bound[key]=Expand(pair.Value);}
   info=Npx(root,args,windows);foreach(var kv in bound)info.Environment[kv.Key]=kv.Value;
  }else if(custom!=null){
   if(Path.GetFileNameWithoutExtension(custom.Command).Equals("npx",StringComparison.OrdinalIgnoreCase))info=Npx(root,custom.Args.ToList(),windows);
   else{info=new(){FileName=custom.Command,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root,StandardInputEncoding=new System.Text.UTF8Encoding(false),StandardOutputEncoding=new System.Text.UTF8Encoding(false),StandardErrorEncoding=new System.Text.UTF8Encoding(false)};foreach(string a in custom.Args)info.ArgumentList.Add(a);CleanEnvironment(info);}
  }else{
   var args=new List<string>{"-y",p.Package+"@"+p.Version};
   if(p.Id=="filesystem"){if(!Directory.Exists(folder)||Path.GetFullPath(folder)==Path.GetPathRoot(Path.GetFullPath(folder)))throw new Exception("Выбери существующую отдельную рабочую папку, не весь диск");args.Add(Path.GetFullPath(folder));}
   if(p.Id=="aim-memory"){string home=Path.Combine(root,"aim-memory");Directory.CreateDirectory(home);args.AddRange(new[]{"--memory-path",home});}
   if(p.Id=="playwright")args.AddRange(new[]{"--headless","--isolated"});info=Npx(root,args,windows);
  }
  if(custom!=null)foreach(var pair in custom.Env)info.Environment[pair.Key]=Expand(pair.Value);
  if(p.Url!="")info.Environment["MCP_REMOTE_CONFIG_DIR"]=McpRuntime.AuthDirectory(root,p.Id);
  if(p.Id=="memory-bank"){string home=Path.Combine(root,"memory-bank");Directory.CreateDirectory(home);info.Environment["MEMORY_BANK_ROOT"]=home;}
  if(p.Id=="memory")info.Environment["MEMORY_FILE_PATH"]=Path.Combine(root,"mcp-memory.jsonl");return info;
 }
 public static string Expand(string value)=>Regex.Replace(value,@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}",m=>Environment.GetEnvironmentVariable(m.Groups[1].Value)??throw new Exception("Не задана переменная окружения "+m.Groups[1].Value+". Задай её в Windows и перезапусти NORA; не вводи секрет в чат."));
 public static List<McpCustom> Import(string json){
  if(json.Length>300000)throw new Exception("Конфигурация больше 300 КБ");using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
  if(root.TryGetProperty("mcpServers",out var servers)||root.TryGetProperty("servers",out servers))root=servers;
  if(root.ValueKind!=JsonValueKind.Object)throw new Exception("Нужен объект mcpServers / servers");var result=new List<McpCustom>();
  foreach(var property in root.EnumerateObject()){
   if(result.Count>=50)throw new Exception("Не больше 50 серверов за импорт");var d=property.Value;if(d.ValueKind!=JsonValueKind.Object)throw new Exception("Некорректная запись MCP");
   var c=new McpCustom{Name=property.Name};if(c.Name.Length>80||c.Name.Length==0)throw new Exception("Некорректное имя коннектора");c.Id="custom-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(c.Name)))[..16].ToLowerInvariant();
   if(d.TryGetProperty("url",out var u)||d.TryGetProperty("serverUrl",out u)){c.Url=u.GetString()??"";ValidateUrl(c.Url);}else{
    c.Command=d.GetProperty("command").GetString()??"";if(c.Command.Length==0||c.Command.Length>500||Regex.IsMatch(Path.GetFileNameWithoutExtension(c.Command),@"^(cmd|powershell|pwsh|bash|sh|wscript|cscript)$",RegexOptions.IgnoreCase))throw new Exception("Укажи исполняемый MCP сервер, node/npx/uvx/docker, не оболочку cmd/powershell/sh.");
    if(d.TryGetProperty("args",out var a))c.Args=a.EnumerateArray().Select(x=>x.GetString()??"").ToArray();if(c.Args.Any(a=>Regex.IsMatch(a,@"(?i)(?:--(?:api[-_]?key|token|password|secret)|authorization:|--header-file|--debug)")))throw new Exception("Передавай секреты через env или headers, не args; debug/header-file не импортируются.");if(c.Args.Length>80||c.Args.Any(a=>a.Length>3000||Regex.IsMatch(a,@"(?i)(?:Bearer\s+|gh[pousr]_[A-Za-z0-9]|github_pat_)")))throw new Exception("Слишком длинные аргументы или секрет в args. Перенеси секрет в переменную окружения.");
   }
   if(d.TryGetProperty("env",out var env))foreach(var e in env.EnumerateObject()){
    string value=e.Value.GetString()??"";if(!Regex.IsMatch(e.Name,@"^[A-Za-z_][A-Za-z0-9_]*$")||!Regex.IsMatch(value,@"^\$\{[A-Za-z_][A-Za-z0-9_]*\}$"))throw new Exception("env должен содержать ссылки ${ENV_NAME}, не значения/токены.");c.Env[e.Name]=value;
   }
   if(d.TryGetProperty("headers",out var headers))foreach(var h in headers.EnumerateObject()){
    string value=h.Value.GetString()??"";if(!Regex.IsMatch(h.Name,@"^[A-Za-z][A-Za-z0-9-]{0,70}$")||value.Length>500||value.Contains('\n')||value.Contains('\r'))throw new Exception("Некорректный заголовок MCP");
    if(!new[]{"X-MCP-Toolsets","X-MCP-Tools","X-MCP-Readonly","X-MCP-Exclude-Tools","X-MCP-Lockdown","X-MCP-Features"}.Contains(h.Name,StringComparer.OrdinalIgnoreCase)&&!Regex.IsMatch(value,@"^(?:Bearer )?\$\{[A-Za-z_][A-Za-z0-9_]*\}$"))throw new Exception("Секретные headers должны ссылаться на ${ENV_NAME}; не сохраняй ключ в JSON.");c.Headers[h.Name]=value;
   }
   result.Add(c);
  }
  if(result.Count==0)throw new Exception("Серверы в конфигурации не найдены");return result;
 }
}
