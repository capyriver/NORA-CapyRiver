using LocalChat;
using System.Net;
using System.Security.Cryptography;
int passed=0;void Check(bool y,string n){if(!y)throw new Exception("FAIL "+n);passed++;Console.WriteLine("PASS "+n);}
string root=Path.Combine(Path.GetTempPath(),"nora-air-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try{
 Check(AirCatalog.Models.Length==4,"four pinned AirLLM downloads");Check(AirCatalog.Models.Select(s=>s.Id).Distinct().Count()==4,"unique ids");
 foreach(var s in AirCatalog.Models){Check(s.Repo.StartsWith("Qwen/")&&s.Revision.Length==40,"official pinned repository");Check(s.Files.Any(f=>f.Name=="config.json")&&s.Files.Any(f=>f.Name.EndsWith(".safetensors")),"config and weights present");Check(s.Files.All(f=>!f.Name.Contains('/')&&!f.Name.Contains('\\')&&f.Sha256.Length==64&&f.Bytes>0),"flat files with SHA256");Check(s.Bytes>10_000_000_000,"full weights not GGUF");}
 Check(AirCatalog.Get("air-coder-32b").Bytes>60_000_000_000,"32B download budget truthful");
 byte[] bytes="test weight"u8.ToArray();string hash=Convert.ToHexString(SHA256.HashData(bytes));var test=new AirSpec("test","Test","Qwen/test",new string('a',40),false,new[]{new AirFile("model.safetensors",bytes.Length,hash)});
 Check(!AirCatalog.Downloaded(root,test),"initially missing");using var client=new HttpClient(new Handler(bytes));await AirCatalog.Install(root,test,_=>{},default,client,false);Check(AirCatalog.Downloaded(root,test),"download marked verified only after checks");Check(File.Exists(Path.Combine(test.Home(root),"nora-verified.json")),"download receipt saved");
 var old=new Settings{ModelPath="external-path",Python="keep-python",SystemPrompt="keep-prompt",Reasoning=true,ResponseMode=2};AirCatalog.Apply(old,test.Home(root),false);Check(old.Mode==0&&old.ModelPath==test.Home(root),"switch to AirLLM folder");Check(old.ContextTokens==2048&&old.MaxTokens==1024,"useful code output budget");Check(!old.Reasoning&&old.ResponseMode==0,"coder disables unsupported thinking");Check(old.Python=="keep-python"&&old.SystemPrompt=="keep-prompt","keep user environment and prompt");
 File.WriteAllBytes(Path.Combine(test.Home(root),"model.safetensors"),"corrupt"u8.ToArray());Check(!AirCatalog.Downloaded(root,test),"incomplete file not installed");await AirCatalog.Install(root,test,_=>{},default,client,false);Check(AirCatalog.Downloaded(root,test),"corrupt file repaired");
 var bad=test with{Id="bad",Files=new[]{new AirFile("model.safetensors",bytes.Length,new string('0',64))}};bool refused=false;try{await AirCatalog.Install(root,bad,_=>{},default,client,false);}catch{refused=true;}Check(refused&&!AirCatalog.Downloaded(root,bad),"hash failure no receipt");
 using var ct=new CancellationTokenSource();ct.Cancel();refused=false;try{await AirCatalog.Install(root,test,_=>{},ct.Token,client,false);}catch(OperationCanceledException){refused=true;}Check(refused,"cancel honored");
 var owner=test.Owner;string outside=Path.Combine(root,"external");Directory.CreateDirectory(outside);File.WriteAllText(Path.Combine(outside,"keep"),"keep");ModelCatalog.Delete(root,new[]{owner});Check(!Directory.Exists(test.Home(root))&&File.Exists(Path.Combine(outside,"keep")),"delete only managed folder");
 var linked=AirCatalog.Get("air-qwen3-8b");Directory.CreateDirectory(Path.GetDirectoryName(linked.Home(root))!);Directory.CreateSymbolicLink(linked.Home(root),outside);refused=false;try{AirCatalog.Safe(root,linked);}catch(IOException){refused=true;}Check(refused,"symlink destination refused");Directory.Delete(linked.Home(root));
 Console.WriteLine($"{passed} Air catalog checks passed");
}finally{Directory.Delete(root,true);}
class Handler(byte[] content):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(content)});}}
