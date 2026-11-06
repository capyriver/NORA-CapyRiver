using System.Text.RegularExpressions;
namespace LocalChat;
sealed record McpMatch(string Server,string ServerName,string Name,string Description);
static class McpToolSearch {
 public static McpMatch[] Find(IEnumerable<(string Server,string ServerName,McpTool Tool)> index,string query,string server="",int limit=12){
  var q=query.ToLowerInvariant();var terms=Regex.Matches(q,@"[\p{L}\p{N}_-]{2,}").Select(m=>m.Value).Distinct().Take(16).ToList();
  if(Regex.IsMatch(q,"cloudflare|клоудфл|клаудфл|опублику|деплой|разверни"))terms.AddRange(new[]{"cloudflare","workers","pages","deploy","publish","execute","search"});
  if(Regex.IsMatch(q,"памят|запомн|вспомн|факт|сохрани"))terms.AddRange(new[]{"memory","store","facts","observations"});
  if(Regex.IsMatch(q,"репозитор|гитхаб|файл|код"))terms.AddRange(new[]{"repository","file","code"});if(Regex.IsMatch(q,"задач|ошиб|баг"))terms.AddRange(new[]{"issue","task","error"});if(Regex.IsMatch(q,"сообщен|коммент"))terms.AddRange(new[]{"message","comment"});if(Regex.IsMatch(q,"найди|поиск|искать"))terms.Add("search");if(Regex.IsMatch(q,"созда|добав"))terms.Add("create");if(Regex.IsMatch(q,"измен|обнов"))terms.Add("update");if(Regex.IsMatch(q,"прочита|чита"))terms.AddRange(new[]{"read","get"});
  return index.Where(x=>server==""||x.Server==server).Select(x=>new{x,score=terms.Sum(w=>(x.Tool.Name.Contains(w,StringComparison.OrdinalIgnoreCase)?8:0)+(x.Tool.Description.Contains(w,StringComparison.OrdinalIgnoreCase)?1:0)+(x.Server.Contains(w,StringComparison.OrdinalIgnoreCase)?3:0))}).Where(x=>terms.Count==0||x.score>0).OrderByDescending(x=>x.score).ThenBy(x=>x.x.Server).ThenBy(x=>x.x.Tool.Name).Take(Math.Clamp(limit,1,50)).Select(x=>new McpMatch(x.x.Server,x.x.ServerName,x.x.Tool.Name,x.x.Tool.Description[..Math.Min(220,x.x.Tool.Description.Length)])).ToArray();
 }
}
