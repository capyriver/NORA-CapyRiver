using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text;
using System.Text.Json;
namespace LocalChat;
partial class MainWindow {
 readonly Panel startupPanel=new(){Dock=DockStyle.Fill,BackColor=Color.FromArgb(25,25,25)};
 readonly Label startupTitle=new(){AutoSize=true,Text="NORA · запуск интерфейса",Font=new Font("Segoe UI",18,FontStyle.Bold),ForeColor=Color.White,MaximumSize=new Size(760,0)};
 readonly Label startupDetail=new(){AutoSize=true,Text="Подготавливаю интерфейс…",ForeColor=Color.Silver,MaximumSize=new Size(760,0),Margin=new Padding(0,20,0,20)};
 readonly FlowLayoutPanel startupButtons=new(){AutoSize=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,MaximumSize=new Size(760,0)};
 bool startupReady,startupFailed;int startupAttempt;
 void InitStartupPanel(){
  var content=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(36,60,36,24)};
  content.Controls.Add(startupTitle);content.Controls.Add(startupDetail);content.Controls.Add(startupButtons);startupPanel.Controls.Add(content);Controls.Add(startupPanel);startupPanel.BringToFront();
  var retry=StartupButton("Повторить запуск");retry.Click+=async(_,_)=>await StartView();
  var copy=StartupButton("Копировать диагностику");copy.Click+=(_,_)=>{try{Clipboard.SetText(File.Exists(StartupLog.Path)?File.ReadAllText(StartupLog.Path):"Журнал ещё не создан");startupDetail.Text+="\nДиагностика скопирована.";}catch(Exception e){MessageBox.Show(e.Message,"NORA");}};
  var folder=StartupButton("Открыть папку журнала");folder.Click+=(_,_)=>{try{Directory.CreateDirectory(Path.GetDirectoryName(StartupLog.Path)!);Process.Start(new ProcessStartInfo("explorer.exe"){UseShellExecute=false,ArgumentList={Path.GetDirectoryName(StartupLog.Path)!}});}catch(Exception e){MessageBox.Show(e.Message,"NORA");}};
  startupButtons.Controls.AddRange(new Control[]{retry,copy,folder});startupButtons.Visible=false;
 }
 static Button StartupButton(string text)=>new(){Text=text,AutoSize=true,MinimumSize=new Size(180,42),ForeColor=Color.White,BackColor=Color.FromArgb(45,45,45),FlatStyle=FlatStyle.Flat,Margin=new Padding(0,0,12,12)};
 void StartupStage(int attempt,string text){if(attempt!=startupAttempt||closing||IsDisposed)return;StartupLog.Write(text);if(!startupFailed)startupDetail.Text=text+"\n\nМодели и чаты остаются на месте. Первый запуск может занять до минуты.";}
 void FailStartup(int attempt,string message){
  if(attempt!=startupAttempt||closing||IsDisposed||startupFailed)return;startupFailed=true;startupReady=false;StartupLog.Write("Startup failed",message);
  startupTitle.Text="Не удалось загрузить интерфейс NORA";startupDetail.Text=message+"\n\nДанные не удалены. Попробуй повторить запуск. Если ошибка останется, пришли диагностику.\n\nЖурнал: "+StartupLog.Path;
  startupButtons.Visible=true;startupPanel.Visible=true;startupPanel.BringToFront();
 }
 void CompleteStartup(){if(startupFailed||closing||IsDisposed||startupReady)return;startupReady=true;StartupLog.Write("UI painted and state applied");startupPanel.Visible=false;}
 async Task WatchStartup(int attempt){await Task.Delay(TimeSpan.FromSeconds(60));if(attempt==startupAttempt&&!startupReady)FailStartup(attempt,"Интерфейс не подтвердил загрузку за 60 секунд. Проверь наличие Microsoft Edge WebView2 Runtime.");}
 async Task StartView(){
  if(closing||IsDisposed)return;int attempt=++startupAttempt;startupReady=false;startupFailed=false;startupPanel.Visible=true;startupPanel.BringToFront();startupTitle.Text="NORA · запуск интерфейса";startupButtons.Visible=false;
  _=WatchStartup(attempt);
  try{
   // Recreate only the UI WebView, never the model process, user chats or browser cookies.
   var previous=view;view=new WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=Color.FromArgb(25,25,25)};workspaceSplit.Panel1.Controls.Add(view);view.SendToBack();previous.Dispose();var target=view;
   StartupStage(attempt,"Создание WebView2 · аппаратное ускорение по умолчанию");
   var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"webview-ui-3.2"),new CoreWebView2EnvironmentOptions("--disable-features=msEdgeSidebarV2")).WaitAsync(TimeSpan.FromSeconds(25));
   if(attempt!=startupAttempt||closing||IsDisposed)return;
   StartupStage(attempt,"Подключение WebView2 Runtime "+env.BrowserVersionString);
   await target.EnsureCoreWebView2Async(env).WaitAsync(TimeSpan.FromSeconds(25));
   if(attempt!=startupAttempt||closing||IsDisposed)return;
   var core=target.CoreWebView2;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreHostObjectsAllowed=false;core.Settings.IsStatusBarEnabled=false;
   core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;core.NewWindowRequested+=(_,e)=>e.Handled=true;core.DownloadStarting+=(_,e)=>e.Cancel=true;
   const string uiUrl="https://nora-ui.local/index.html";
   core.NavigationStarting+=(_,e)=>{if(e.Uri!=uiUrl){e.Cancel=true;StartupLog.Write("Blocked UI navigation");}};
   core.ProcessFailed+=(_,e)=>FailStartup(attempt,"Сбой процесса WebView2: "+e.ProcessFailedKind+". Можно повторить запуск интерфейса.");
   core.NavigationCompleted+=(_,e)=>{if(!e.IsSuccess)FailStartup(attempt,"Ошибка загрузки страницы: "+e.WebErrorStatus);else StartupStage(attempt,"Страница открыта · ожидание готовности интерфейса");};
   core.WebMessageReceived+=async(_,e)=>{
    if(attempt!=startupAttempt||closing||startupFailed||e.Source!=uiUrl)return;
    try{using var doc=JsonDocument.Parse(e.TryGetWebMessageAsString());await Dispatch(doc.RootElement.GetProperty("action").GetString()??"",doc.RootElement.GetProperty("data").Clone());}
    catch(Exception ex){StartupLog.Write("Host message",ex);if(!startupReady)FailStartup(attempt,"Ошибка подготовки интерфейса: "+ex.Message);else Post("toast",ex.Message);}
   };
   // A fixed HTTPS origin avoids opaque about:blank navigation and cache issues.
   string uiFolder=Path.Combine(root,"ui-3.2");Directory.CreateDirectory(uiFolder);File.WriteAllText(Path.Combine(uiFolder,"index.html"),Resource("ui.html"),new UTF8Encoding(false));
   core.SetVirtualHostNameToFolderMapping("nora-ui.local",uiFolder,CoreWebView2HostResourceAccessKind.Deny);
   StartupStage(attempt,"Загрузка встроенной страницы NORA");core.Navigate(uiUrl);
  }catch(Exception e){if(attempt!=startupAttempt||closing||IsDisposed)return;StartupLog.Write("WebView initialization",e);FailStartup(attempt,(e is TimeoutException?"WebView2 не ответил вовремя.":e.Message)+"\nЕсли WebView2 Runtime отсутствует, установи его с официального сайта Microsoft: https://developer.microsoft.com/microsoft-edge/webview2/");}
 }
}
