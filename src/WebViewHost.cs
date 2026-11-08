using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LocalChat;

partial class MainWindow {
 bool startupReady,startupFailed;int startupAttempt;

 void StartupStage(int attempt,string text){if(attempt==startupAttempt&&!closing&&!IsDisposed)StartupLog.Write(text);}
 void FailStartup(int attempt,string message){
  if(attempt!=startupAttempt||closing||IsDisposed||startupFailed)return;
  startupFailed=true;startupReady=false;StartupLog.Write("UI startup failed",message);
  var language=settings.InterfaceLanguage;
  var title=language switch{"en"=>"NORA — startup error","zh-CN"=>"NORA — 启动错误",_=>"NORA — ошибка запуска"};
  var detail=language switch{"en"=>"NORA could not open its interface. Your chats and model files were not changed.","zh-CN"=>"NORA 无法打开界面。聊天记录和模型文件均未更改。",_=>"Не удалось открыть интерфейс NORA. Чаты и файлы моделей не изменены。"};
  var logLabel=language switch{"en"=>"Log:","zh-CN"=>"日志：",_=>"Журнал:"};
  MessageBox.Show(this,$"{detail}\n\n{message}\n\n{logLabel} {StartupLog.Path}",title,MessageBoxButtons.OK,MessageBoxIcon.Error);
 }
 void CompleteStartup(){if(closing||IsDisposed||startupReady||startupFailed)return;startupReady=true;StartupLog.Write("UI ready");}

 static bool IsLocalUiAddress(string address){
  return Uri.TryCreate(address,UriKind.Absolute,out var uri)
   &&uri.Scheme==Uri.UriSchemeHttps
   &&uri.Host.Equals("nora-ui.local",StringComparison.OrdinalIgnoreCase)
   &&uri.AbsolutePath.Equals("/index.html",StringComparison.OrdinalIgnoreCase);
 }

 async Task StartView(){
  if(closing||IsDisposed)return;
  int attempt=++startupAttempt;startupReady=false;startupFailed=false;
  try{
   StartupStage(attempt,"Initializing WebView2");
   var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"webview-ui-4.0"),new CoreWebView2EnvironmentOptions("--disable-features=msEdgeSidebarV2")).WaitAsync(TimeSpan.FromSeconds(25));
   if(attempt!=startupAttempt||closing||IsDisposed)return;
   await view.EnsureCoreWebView2Async(env).WaitAsync(TimeSpan.FromSeconds(25));
   if(attempt!=startupAttempt||closing||IsDisposed)return;

   var core=view.CoreWebView2;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreHostObjectsAllowed=false;core.Settings.IsStatusBarEnabled=false;
   core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
   core.NewWindowRequested+=(_,e)=>e.Handled=true;
   core.DownloadStarting+=(_,e)=>e.Cancel=true;
   const string uiUrl="https://nora-ui.local/index.html";
   core.NavigationStarting+=(_,e)=>{
    StartupLog.Write("Navigation starting",e.Uri);
    if(!IsLocalUiAddress(e.Uri)){e.Cancel=true;StartupLog.Write("Blocked navigation",e.Uri);}
   };
   core.ProcessFailed+=(_,e)=>FailStartup(attempt,"WebView2 stopped unexpectedly: "+e.ProcessFailedKind);
   core.NavigationCompleted+=(_,e)=>{if(!e.IsSuccess)FailStartup(attempt,"The built-in page failed to load ("+e.WebErrorStatus+").");};
   core.WebMessageReceived+=async(_,e)=>{
    if(attempt!=startupAttempt||closing||startupFailed||!IsLocalUiAddress(e.Source))return;
    try{using var doc=JsonDocument.Parse(e.TryGetWebMessageAsString());await Dispatch(doc.RootElement.GetProperty("action").GetString()??"",doc.RootElement.GetProperty("data").Clone());}
    catch(Exception ex){StartupLog.Write("Host message",ex);if(!startupReady)FailStartup(attempt,"The interface reported an initialization error: "+ex.Message);else Post("toast",ex.Message);}
   };

   string uiFolder=Path.Combine(root,"ui-4.0");Directory.CreateDirectory(uiFolder);File.WriteAllText(Path.Combine(uiFolder,"index.html"),Resource("ui.html"),new UTF8Encoding(false));
   core.SetVirtualHostNameToFolderMapping("nora-ui.local",uiFolder,CoreWebView2HostResourceAccessKind.DenyCors);
   StartupStage(attempt,"Opening bundled interface");core.Navigate(uiUrl);
  }catch(Exception e){if(attempt!=startupAttempt||closing||IsDisposed)return;StartupLog.Write("WebView initialization",e);FailStartup(attempt,(e is TimeoutException?"WebView2 did not respond in time.":e.Message)+"\nInstall Microsoft Edge WebView2 Runtime if it is missing: https://developer.microsoft.com/microsoft-edge/webview2/");}
 }
}
