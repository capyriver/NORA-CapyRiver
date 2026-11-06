using LocalChat;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));var chunks=new StringBuilder();var timer=Stopwatch.StartNew();
string answer=await LocalApi.ChatAsync(LocalApi.Endpoint(args.Length>0?args[0]:"http://127.0.0.1:12346/v1"),"local-model",new(){new("system",DirectSetup.DirectPrompt),new("user","Ответь одним словом: готов.")},32,t=>chunks.Append(t),timeout.Token,.1);
if(string.IsNullOrWhiteSpace(answer)||answer!=chunks.ToString())throw new Exception("Real stream was empty or inconsistent");
Console.WriteLine(JsonSerializer.Serialize(new{passed=true,scope="Real C# streaming and Direct self-test request; Linux CPU, not Windows",reply=answer,seconds=timer.Elapsed.TotalSeconds,modelSha256=DirectSetup.ModelHash},new JsonSerializerOptions{WriteIndented=true}));
