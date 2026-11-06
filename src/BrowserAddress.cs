namespace LocalChat;
static class BrowserAddress {
 public static string Input(string text){
  text=text.Trim();if(text.Length==0)throw new Exception("Введи адрес сайта или поисковый запрос.");
  if(text.StartsWith("http://",StringComparison.OrdinalIgnoreCase))return WebReader.Parse("https://"+text[7..]).AbsoluteUri;
  if(text.Contains("://")||text.StartsWith("javascript:",StringComparison.OrdinalIgnoreCase)||text.StartsWith("file:",StringComparison.OrdinalIgnoreCase)||text.StartsWith("data:",StringComparison.OrdinalIgnoreCase))return WebReader.Parse(text).AbsoluteUri;
  if(!text.Any(char.IsWhiteSpace)&&text.Split('/')[0].Contains('.'))return WebReader.Parse("https://"+text).AbsoluteUri;
  return "https://www.google.com/search?q="+Uri.EscapeDataString(text[..Math.Min(text.Length,1000)]);
 }
 // Secure WebSocket subresources use exactly the same public-host policy as HTTPS.
 public static Uri Resource(string url)=>WebReader.Parse(url.StartsWith("wss://",StringComparison.OrdinalIgnoreCase)?"https://"+url[6..]:url);
}
