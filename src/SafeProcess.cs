using System.Diagnostics;
using System.Text;
namespace LocalChat;
sealed record ProcessResult(int Exit,string Output,string Error);
static class SafeProcess {
 static async Task<string> Drain(StreamReader reader,int maximum){var s=new StringBuilder();char[] b=new char[4096];int n;while((n=await reader.ReadAsync(b))>0)if(s.Length<maximum)s.Append(b,0,Math.Min(n,maximum-s.Length));return s.ToString();}
 public static async Task<ProcessResult> Run(string executable,IEnumerable<string> arguments,int seconds,CancellationToken ct){
  if(!Path.IsPathFullyQualified(executable)||!File.Exists(executable))throw new Exception("Укажи существующую локальную программу.");var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in arguments)info.ArgumentList.Add(a);
  using var p=Process.Start(info)??throw new Exception("Не удалось запустить программу");using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(seconds));using var stop=timeout.Token.Register(()=>{try{if(!p.HasExited)p.Kill(true);}catch{}});var output=Drain(p.StandardOutput,300_000);var error=Drain(p.StandardError,30_000);try{await p.WaitForExitAsync(timeout.Token);return new(p.ExitCode,await output,await error);}catch{try{if(!p.HasExited)p.Kill(true);await p.WaitForExitAsync();await Task.WhenAll(output,error);}catch{}throw;}
 }
}
