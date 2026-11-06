namespace LocalChat;
partial class MainWindow {
 readonly Panel browserDock=new(){Dock=DockStyle.Fill,BackColor=Color.FromArgb(25,25,25)};
 readonly Panel browserPane=new(){Dock=DockStyle.Fill,Visible=false};
 readonly SplitContainer workspaceSplit=new(){Dock=DockStyle.Fill,Size=new(1400,900),SplitterDistance=700,SplitterWidth=8,Panel1MinSize=300,Panel2MinSize=300,Panel2Collapsed=true,BackColor=Color.FromArgb(52,52,52)};
 readonly BrowserControlLease browserLease=new();
 readonly System.Windows.Forms.Timer browserHeartbeat=new(){Interval=1000};
 bool browserManual,browserSplit,browserFresh=true,browserPreviewBusy;string browserPreview="";
 DateTime lastBrowserPreview=DateTime.MinValue;
 readonly Button takeover=new RoundedButton{Dock=DockStyle.Right,Width=215,FlatStyle=FlatStyle.Flat,ForeColor=Color.Gainsboro,BackColor=Color.FromArgb(32,32,32),Text="Управляет NORA"};
 void InitBrowserDock(){
  var bar=new Panel{Dock=DockStyle.Top,Height=54,BackColor=Color.FromArgb(32,32,32)};
  var close=new RoundedButton{Dock=DockStyle.Right,Width=44,Text="×",FlatStyle=FlatStyle.Flat,ForeColor=Color.Gainsboro};
  var title=new Label{Dock=DockStyle.Fill,Text="Браузер NORA",AutoEllipsis=true,Padding=new(14,16,0,0),ForeColor=Color.Gainsboro};
  bar.Controls.Add(title);bar.Controls.Add(takeover);bar.Controls.Add(close);browserPane.Controls.Add(browserDock);browserPane.Controls.Add(bar);var actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=54,BackColor=Color.FromArgb(25,25,25),Padding=new(10,5,8,5),WrapContents=false,AutoScroll=true};var diagnose=new RoundedButton{Text="Проверить доступ",Width=158,Height=44};var capture=new RoundedButton{Text="Снимок",Width=86,Height=44};actions.Controls.Add(diagnose);actions.Controls.Add(capture);browserPane.Controls.Add(actions);actions.BringToFront();bar.BringToFront();diagnose.Click+=async(_,_)=>{if(busy)return;try{EnableBrowserTools();ResumeBrowserControl();await RunQuick(new("browser_snapshot"),"Проверка доступа: текст и элементы текущей страницы");}catch(Exception e){Post("toast",e.Message);}};capture.Click+=async(_,_)=>{if(busy)return;try{await RunQuick(new("browser_capture"),"Снимок текущей страницы");}catch(Exception e){Post("toast",e.Message);}};workspaceSplit.Panel1.Controls.Add(view);workspaceSplit.Panel2.Controls.Add(browserPane);Controls.Add(workspaceSplit);workspaceSplit.BringToFront();
  close.Click+=(_,_)=>SetBrowserSplit(false);takeover.Click+=(_,_)=>{if(browserManual)ResumeBrowserControl();else TakeBrowserControl();};
  workspaceSplit.SplitterMoved+=(_,_)=>{view.PerformLayout();};
  browserHeartbeat.Tick+=async(_,_)=>{
   if(closing)return;if(browserLease.Tick(DateTime.UtcNow)){browserManual=false;RecordWork("10 секунд без действий · NORA продолжает браузерную задачу");}
   if(browser!=null&&!browser.IsDisposed){PublishBrowser();if(browserSplit&&!browserPreviewBusy&&(DateTime.UtcNow-lastBrowserPreview).TotalSeconds>=3){browserPreviewBusy=true;lastBrowserPreview=DateTime.UtcNow;try{await UpdateBrowserPreview(browser,CancellationToken.None);}catch{}finally{browserPreviewBusy=false;}}}
  };browserHeartbeat.Start();FormClosed+=(_,_)=>{browserHeartbeat.Stop();browserHeartbeat.Dispose();};
 }
 void SetBrowserSplit(bool value){browserSplit=value;workspaceSplit.Panel2Collapsed=!value;browserPane.Visible=value;if(value&&workspaceSplit.Width>620)workspaceSplit.SplitterDistance=Math.Clamp(workspaceSplit.Width/2,300,workspaceSplit.Width-308);workspaceSplit.PerformLayout();Post("browserSplit",value);PublishBrowser();}
 void BrowserHumanActivity(){bool first=!browserManual;browserLease.Activity(DateTime.UtcNow);browserManual=true;if(first){RecordWork("Управление у пользователя · браузерные действия NORA приостановлены");PublishBrowser();}}
 void TakeBrowserControl(){BrowserHumanActivity();SetBrowserSplit(true);}
 void ResumeBrowserControl(){browserLease.Resume();browserManual=false;RecordWork("Управление браузером возвращено NORA");PublishBrowser();}
 async Task WaitBrowserControl(CancellationToken token){while(browserManual){token.ThrowIfCancellationRequested();if(browserLease.Tick(DateTime.UtcNow)){browserManual=false;PublishBrowser();break;}await Task.Delay(100,token);}}
 void PublishBrowser(){if(browser==null||browser.IsDisposed)return;takeover.Text=browserManual?$"Управляешь ты · возврат через {browserLease.Remaining(DateTime.UtcNow)} с":"NORA · перехватить";Post("browserState",new{url=browser.CurrentUrl,title=browser.CurrentTitle,preview=browserPreview,manual=browserManual,remaining=browserLease.Remaining(DateTime.UtcNow),split=browserSplit,tabs=browser.TabCount,toolsReady=settings.ToolMode&&settings.UnifiedAgent&&ExperimentalProfiles.CanUseModelTools(settings)});}
 async Task UpdateBrowserPreview(BrowserWindow b,CancellationToken token){token.ThrowIfCancellationRequested();try{browserPreview="data:image/png;base64,"+Convert.ToBase64String(PngPreview(await b.CapturePage(token)));}catch(OperationCanceledException){throw;}catch{browserPreview="";}PublishBrowser();}
}
