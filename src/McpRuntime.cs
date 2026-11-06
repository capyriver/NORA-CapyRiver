using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
namespace LocalChat;
sealed class McpRuntimeException(string message):Exception(message);
static class McpRuntime {
 public static string? BundledDirectory(bool windows){
  if(!windows)return null;var path=Path.Combine(AppContext.BaseDirectory,"runtime","node");
  return File.Exists(Path.Combine(path,"node.exe"))&&File.Exists(Path.Combine(path,"node_modules","npm","bin","npx-cli.js"))?path:null;
 }
 public static async Task CheckNode(bool windows,CancellationToken ct){
  var portable=BundledDirectory(windows);var info=new ProcessStartInfo(portable==null?"node":Path.Combine(portable,"node.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};info.ArgumentList.Add("--version");
  Process process;try{process=Process.Start(info)??throw new Exception();}catch{throw new McpRuntimeException("Node.js не найден. Распакуй ВЕСЬ архив NORA 3.11 вместе с папкой runtime рядом с EXE либо установи Node.js 24 LTS с nodejs.org и перезапусти NORA.");}
  using var p=process;using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(10));using var stop=timeout.Token.Register(()=>{try{if(!p.HasExited)p.Kill(true);}catch{}});
  var output=p.StandardOutput.ReadToEndAsync(timeout.Token);var error=p.StandardError.ReadToEndAsync(timeout.Token);await p.WaitForExitAsync(timeout.Token);string v=(await output).Trim();await error;
  var m=Regex.Match(v,@"^v(\d+)\.");if(p.ExitCode!=0||!m.Success||!int.TryParse(m.Groups[1].Value,out int major)||major<24)throw new McpRuntimeException("Нужен Node.js 24+. Распакуй папку runtime из архива рядом с EXE либо обнови Node.js и перезапусти NORA.");
 }
 static string AuthPath(string root,string id){if(!Regex.IsMatch(id,@"^[a-z0-9-]{1,80}$"))throw new ArgumentException("Некорректный ID подключения");return Path.Combine(root,"mcp-auth",id);}
 static void NoLinks(string path){for(var p=new DirectoryInfo(path);p!=null;p=p.Parent)if(p.Exists&&(p.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("OAuth-каталог не может находиться в папке-ссылке");}
 public static string AuthDirectory(string root,string id){var path=AuthPath(root,id);NoLinks(path);Directory.CreateDirectory(path);
  if(OperatingSystem.IsWindows()){
   var user=WindowsIdentity.GetCurrent().User??throw new IOException("Не определён пользователь Windows");var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(user);security.AddAccessRule(new FileSystemAccessRule(user,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));new DirectoryInfo(path).SetAccessControl(security);
  }else File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
  return path;
 }
 public static void ForgetAuth(string root,string id){var path=AuthPath(root,id);NoLinks(path);if(!Directory.Exists(path))return;var pending=new Stack<string>();pending.Push(path);while(pending.Count>0)foreach(var entry in Directory.EnumerateFileSystemEntries(pending.Pop())){var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Удаление OAuth-входа остановлено: обнаружена ссылка");if((attributes&FileAttributes.Directory)!=0)pending.Push(entry);}Directory.Delete(path,true);}
}
