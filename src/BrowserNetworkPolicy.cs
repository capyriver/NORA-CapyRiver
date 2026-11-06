using System.Net;
namespace LocalChat;
// The embedded browser uses Chromium/Windows proxy settings. Its DNS result may
// legitimately differ from System.Net.Dns (e.g. a fake-IP tunnel). Never silently
// treat a DNS error as proof that the public site itself is a private service.
sealed class BrowserNetworkPolicy {
 readonly Func<string,Task<IPAddress[]>> resolve;
 readonly Func<string,string,Task<bool>> approveCompatibility;
 readonly SemaphoreSlim consent=new(1,1);
 readonly Dictionary<string,DateTime> approved=new(StringComparer.OrdinalIgnoreCase);
 DateTime deniedUntil;
 public bool SystemNetworkApproved {get;private set;}
 public BrowserNetworkPolicy(Func<string,Task<IPAddress[]>> resolve,Func<string,string,Task<bool>> approveCompatibility){this.resolve=resolve;this.approveCompatibility=approveCompatibility;}
 public static Uri PublicAddress(string url){
  var uri=BrowserAddress.Resource(url);string host=uri.DnsSafeHost.TrimEnd('.');
  if(uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6){if(!IPAddress.TryParse(host,out var ip)||!WebReader.Public(ip))throw new Exception("Прямые локальные и служебные IP в браузере запрещены.");}
  else if(!host.Contains('.')||host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||new[]{".localhost",".local",".internal",".lan",".home",".home.arpa"}.Any(s=>host.EndsWith(s,StringComparison.OrdinalIgnoreCase)))throw new Exception("Локальные имена и служебные адреса в браузере запрещены.");
  return uri;
 }
 public async Task Validate(string url){
  var uri=PublicAddress(url);if(SystemNetworkApproved)return;
  if(approved.TryGetValue(uri.Host,out var until)&&until>DateTime.UtcNow)return;
  string problem;
  try{var ips=await resolve(uri.DnsSafeHost).WaitAsync(TimeSpan.FromSeconds(10));
   if(ips.Length>0&&ips.All(WebReader.Public)){if(approved.Count>512)approved.Clear();approved[uri.Host]=DateTime.UtcNow.AddMinutes(2);return;}
   problem=ips.Length==0?"Windows не вернул адреса сайта.":"Windows вернул: "+string.Join(", ",ips.Take(4).Select(a=>a.ToString()));
  }catch(Exception e){problem="Проверка DNS Windows не завершилась: "+e.GetType().Name;}
  await consent.WaitAsync();try{
   if(SystemNetworkApproved)return;
   if(deniedUntil>DateTime.UtcNow)throw new Exception("Системная сеть для браузера не разрешена. Повтори открытие через несколько секунд и подтверди запрос совместимости, если используешь прокси/VPN.");
   if(await approveCompatibility(uri.Host,problem)){SystemNetworkApproved=true;return;}
   deniedUntil=DateTime.UtcNow.AddSeconds(10);throw new Exception("Браузер не получил разрешение использовать системную сеть. Локальные файлы и служебные адреса не открывались.");
  }finally{consent.Release();}
 }
 public static string Notice(string host,string problem)=>"Не удалось надёжно проверить DNS для "+host+".\n"+problem+"\n\nПри VPN, прокси или fake-IP адреса Windows и Chromium могут различаться. Это не означает, что сам сайт локальный.\n\nРазрешить встроенному браузеру использовать системную сеть до закрытия NORA?\n\nДля доменных имён браузер будет доверять сети и прокси Windows, как обычный браузер, без предварительного DNS-фильтра NORA. В такой сети доменное имя может вести во внутреннюю сеть. Прямые локальные IP, локальные имена и file:// останутся заблокированы. Проверка сертификатов HTTPS не отключается. Фоновый загрузчик страниц остаётся в строгом режиме.\n\nРазрешай только в сети, которой доверяешь. Изменение не сохраняется на диск.";
}
