using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using System.Text.RegularExpressions;
namespace LocalChat;
class Attachment {
 public string Id {get;set;}=Guid.NewGuid().ToString("N");public string Name {get;set;}="";public string Kind {get;set;}="text";public string Text {get;set;}="";public string DataUrl {get;set;}="";
}
class AgentProfile {public string Category {get;set;}="Свои / базовые";public string Description {get;set;}="";public string Id {get;set;}=Guid.NewGuid().ToString("N");public string Name {get;set;}="";public string Prompt {get;set;}="";public string Source {get;set;}="";public string TeamSlot {get;set;}="";}
static class Profiles {
 public static List<AgentProfile> BuiltIn()=>new(){
  new(){Id="direct",Name="NORA · Прямой помощник",Prompt="Отвечай прямо и подробно на языке пользователя. Следуй выбранному стилю, избегай ненужных вводных и повторов. Не выдумывай выполненные действия."},
  new(){Id="designer",Name="Дизайнер",Prompt="Ты дизайнер интерфейсов. Уточни пользовательский сценарий, предложи структуру экрана, визуальную иерархию, размеры и доступность. Дай конкретную спецификацию. Не выдумывай созданные картинки или результаты проверок."},
  new(){Id="coder",Name="Разработчик",Prompt="Ты разработчик. Предложи минимальную рабочую реализацию задачи, код с понятными именами и способы проверки. Не утверждай, что код запущен, если инструментов выполнения нет. Отделяй факты от предположений."},
  new(){Id="security",Name="Проверка безопасности",Prompt="Ты специалист по защите приложений. Проверь предложенный код на уязвимости, утечки секретов, неконтролируемое выполнение команд и ошибки проверки входных данных. Укажи риски и конкретные исправления. Не утверждай, что провёл пентест или запускал сканер."},
  new(){Id="analyst",Name="Аналитик рынков",Prompt="Ты помощник по анализу рынков и управлению риском. Не выдумывай актуальные котировки, доходность или совершённые сделки. Работай с данными пользователя; объясняй неопределённость и риск потерь. У тебя нет доступа к брокеру и права совершать сделки."}
 };
}
static class FileTools {
 public static string ReadText(string path){
  var info=new FileInfo(path);if(info.Length>25*1024*1024)throw new Exception("Файл больше 25 МБ: "+info.Name);
  var ext=System.IO.Path.GetExtension(path).ToLowerInvariant();
  if(ext==".pdf"){using var pdf=UglyToad.PdfPig.PdfDocument.Open(path);var text=new StringBuilder();foreach(var page in pdf.GetPages()){if(text.Length>60000)break;text.AppendLine(page.Text);}if(text.Length==0)throw new Exception("В PDF нет текстового слоя. Сканам нужен отдельный OCR, которого в этой сборке нет.");return Limit(text.ToString());}
  if(new[]{".docx",".pptx",".xlsx",".odt",".zip"}.Contains(ext))return ReadArchive(path,ext);
  if(!new[]{".txt",".md",".csv",".tsv",".json",".xml",".yaml",".yml",".toml",".py",".cs",".js",".jsx",".ts",".tsx",".html",".css",".rs",".go",".cpp",".c",".h",".sql",".log",".ini"}.Contains(ext))throw new Exception("Формат "+ext+" пока не читается. Поддерживаются текст/код, PDF с текстом, DOCX, PPTX, XLSX, ODT, ZIP и изображения через vision-модель LM Studio.");
  var bytes=File.ReadAllBytes(path);if(bytes.Contains((byte)0))throw new Exception("Это бинарный файл или текст не в UTF-8. Сохрани текст в UTF-8.");
  try{return Limit(new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF'));}catch(DecoderFallbackException){throw new Exception("Не удалось прочитать текст как UTF-8: "+info.Name);}
 }
 static string Limit(string s)=>s.Length<=60000?s:s[..60000]+"\n[Текст ограничен 60 000 символами]";
 static string ReadArchive(string path,string ext){using var zip=ZipFile.OpenRead(path);if(zip.Entries.Count>10000)throw new Exception("Слишком много записей в архиве.");var text=new StringBuilder();int files=0;foreach(var entry in zip.Entries.OrderBy(x=>x.FullName,StringComparer.Ordinal)){
   var name=entry.FullName.Replace('\\','/');if(name.StartsWith('/')||name.Split('/').Any(x=>x is ".." or "."||x.Contains(':'))||entry.Length==0||entry.Length>200000)continue;
   bool office=ext!=".zip";bool selected=ext switch{".docx"=>name=="word/document.xml",".pptx"=>Regex.IsMatch(name,@"^ppt/slides/slide\d+\.xml$"),".xlsx"=>name=="xl/sharedStrings.xml"||Regex.IsMatch(name,@"^xl/worksheets/sheet\d+\.xml$"),".odt"=>name=="content.xml",_=>new[]{".txt",".md",".json",".csv",".py",".js",".ts",".cs",".html",".css",".yaml",".yml"}.Contains(System.IO.Path.GetExtension(name).ToLowerInvariant())};
   if(!selected||name.Split('/').Any(x=>x.StartsWith('.')||x is "node_modules" or "vendor"))continue;
   using var stream=entry.Open();using var reader=new StreamReader(stream,new UTF8Encoding(false,true));char[] chars=new char[60001];int got=0;while(got<chars.Length){int n=reader.Read(chars,got,chars.Length-got);if(n==0)break;got+=n;}var body=new string(chars,0,Math.Min(got,60000));if(office){try{var xml=XDocument.Parse(body);body=string.Join(" ",xml.Descendants().Where(x=>!x.HasElements).Select(x=>x.Value));}catch{continue;}}
   text.AppendLine("FILE: "+name);text.AppendLine(body);if(++files>=100||text.Length>=60000)break;
  }if(text.Length==0)throw new Exception("Не найдено подходящего текстового содержимого.");return Limit(text.ToString());}
 public static List<(string name,string text)> Artifacts(string answer){
  var list=new List<(string,string)>{("answer.md",answer)};int idx=0;
  foreach(Match match in Regex.Matches(answer,@"```([^\r\n]*)\r?\n([\s\S]*?)```")){
   var lang=match.Groups[1].Value.Trim().Split(' ')[0].ToLowerInvariant();string ext=lang switch{"python" or "py"=>"py","javascript" or "js"=>"js","typescript" or "ts"=>"ts","tsx"=>"tsx","jsx"=>"jsx","csharp" or "cs"=>"cs","html"=>"html","css"=>"css","json"=>"json","sql"=>"sql","bash" or "sh"=>"sh","powershell" or "ps1"=>"ps1","yaml" or "yml"=>"yml","bat" or "batch" or "cmd"=>"bat",_=>"txt"};
   string body=match.Groups[2].Value;if(ext=="bat"){ArtifactWriter.Validate("script.bat",body);body=body.Replace("\r\n","\n").Replace("\n","\r\n");}list.Add(($"code-{++idx:00}.{ext}",body));if(idx>=50)break;
  }return list;
 }
 public static void WriteZip(string path,string answer){using var file=File.Create(path);using var zip=new ZipArchive(file,ZipArchiveMode.Create);foreach(var(name,text)in Artifacts(answer)){using var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false));writer.Write(text);}}
}
