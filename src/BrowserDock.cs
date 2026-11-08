namespace LocalChat;
partial class MainWindow {
 readonly Panel browserDock=new(){Dock=DockStyle.Fill,BackColor=Color.FromArgb(25,25,25)};
 readonly Panel browserPane=new(){Dock=DockStyle.Fill,Visible=false};
 readonly SplitContainer workspaceSplit=new(){Dock=DockStyle.Fill,Size=new(1400,900),SplitterDistance=700,SplitterWidth=8,Panel1MinSize=360,Panel2MinSize=420,Panel2Collapsed=true,BackColor=Color.FromArgb(52,52,52)};
 readonly System.Windows.Forms.Timer browserHeartbeat=new(){Interval=1000};
 bool browserSplit,browserFresh=true,browserPreviewBusy;string browserPreview="";
 DateTime lastBrowserPreview=DateTime.MinValue;
 string BrowserUi(string ru,string en,string zh)=>settings.InterfaceLanguage=="en"?en:settings.InterfaceLanguage=="zh-CN"?zh:ru;
 void InitBrowserDock(){
  var bar=new Panel{Dock=DockStyle.Top,Height=54,BackColor=Color.FromArgb(32,32,32)};
  var close=new RoundedButton{Dock=DockStyle.Right,Width=44,Text="×",FlatStyle=FlatStyle.Flat,ForeColor=Color.Gainsboro};
  var title=new Label{Dock=DockStyle.Fill,Text=BrowserUi("Браузер NORA","NORA Browser","NORA 浏览器"),AutoEllipsis=true,Padding=new(14,16,0,0),ForeColor=Color.Gainsboro};
  bar.Controls.Add(title);bar.Controls.Add(close);browserPane.Controls.Add(browserDock);browserPane.Controls.Add(bar);var actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=54,BackColor=Color.FromArgb(25,25,25),Padding=new(10,5,8,5),WrapContents=false,AutoScroll=true};var diagnose=new RoundedButton{Text=BrowserUi("Проверить доступ","Check access","检查访问"),Width=158,Height=44};var capture=new RoundedButton{Text=BrowserUi("Снимок","Capture","截图"),Width=86,Height=44};actions.Controls.Add(diagnose);actions.Controls.Add(capture);browserPane.Controls.Add(actions);actions.BringToFront();bar.BringToFront();diagnose.Click+=async(_,_)=>{if(busy)return;try{EnableBrowserTools();await RunQuick(new("browser_snapshot"),BrowserUi("Проверка доступа: текст и элементы текущей страницы","Checking access: visible page text and elements","正在检查访问权限：页面可见文本和元素"));}catch(Exception e){Post("toast",e.Message);}};capture.Click+=async(_,_)=>{if(busy)return;try{await RunQuick(new("browser_capture"),"Снимок текущей страницы");}catch(Exception e){Post("toast",e.Message);}};workspaceSplit.Panel1.Controls.Add(view);workspaceSplit.Panel2.Controls.Add(browserPane);Controls.Add(workspaceSplit);workspaceSplit.BringToFront();
  close.AccessibleName=BrowserUi("Закрыть браузер","Close browser","关闭浏览器");close.Click+=(_,_)=>SetBrowserSplit(false);
  workspaceSplit.SplitterMoved+=(_,_)=>{view.PerformLayout();};
  browserHeartbeat.Tick+=async(_,_)=>{
   if(closing)return;
   if(browser!=null&&!browser.IsDisposed){PublishBrowser();if(browserSplit&&!browserPreviewBusy&&(DateTime.UtcNow-lastBrowserPreview).TotalSeconds>=3){browserPreviewBusy=true;lastBrowserPreview=DateTime.UtcNow;try{await UpdateBrowserPreview(browser,CancellationToken.None);}catch{}finally{browserPreviewBusy=false;}}}
  };browserHeartbeat.Start();FormClosed+=(_,_)=>{browserHeartbeat.Stop();browserHeartbeat.Dispose();};
 }
 void SetBrowserSplit(bool value){browserSplit=value;workspaceSplit.Panel2Collapsed=!value;browserPane.Visible=value;if(value&&workspaceSplit.Width>=788)workspaceSplit.SplitterDistance=Math.Clamp((int)(workspaceSplit.Width*.54),360,workspaceSplit.Width-428);workspaceSplit.PerformLayout();Post("browserSplit",value);PublishBrowser();}
 async Task WaitBrowserControl(CancellationToken token){token.ThrowIfCancellationRequested();await Task.CompletedTask;}
 void PublishBrowser(){if(browser==null||browser.IsDisposed)return;Post("browserState",new{url=browser.CurrentUrl,title=browser.CurrentTitle,preview=browserPreview,manual=false,remaining=0,split=browserSplit,tabs=browser.TabCount,toolsReady=settings.ToolMode&&settings.UnifiedAgent&&ExperimentalProfiles.CanUseModelTools(settings)});}
 async Task UpdateBrowserPreview(BrowserWindow b,CancellationToken token){token.ThrowIfCancellationRequested();try{browserPreview="data:image/png;base64,"+Convert.ToBase64String(PngPreview(await b.CapturePage(token)));}catch(OperationCanceledException){throw;}catch{browserPreview="";}PublishBrowser();}
}
