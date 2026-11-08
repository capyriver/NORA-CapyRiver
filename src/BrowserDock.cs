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
  var bar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.FromArgb(32,32,32),Padding=new(8,4,8,4),Margin=Padding.Empty};bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));bar.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  var close=new RoundedButton{Dock=DockStyle.Fill,Text="×",FlatStyle=FlatStyle.Flat,ForeColor=Color.Gainsboro,Margin=new(2)};
  var title=new Label{Dock=DockStyle.Fill,Text=BrowserUi("Браузер NORA","NORA Browser","NORA 浏览器"),AutoEllipsis=true,Padding=new(10,12,0,0),ForeColor=Color.Gainsboro};
  bar.Controls.Add(title,0,0);bar.Controls.Add(close,1,0);var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.FromArgb(25,25,25),Padding=new(8,4,8,4),Margin=Padding.Empty};actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));var diagnose=new RoundedButton{Text=BrowserUi("Проверить доступ","Check access","检查访问"),Dock=DockStyle.Fill,Margin=new(3)};var capture=new RoundedButton{Text=BrowserUi("Снимок","Capture","截图"),Dock=DockStyle.Fill,Margin=new(3)};actions.Controls.Add(diagnose,0,0);actions.Controls.Add(capture,1,0);var browserLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=Color.FromArgb(25,25,25),Margin=Padding.Empty,Padding=Padding.Empty};browserLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));browserLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));browserLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));browserLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));browserLayout.Controls.Add(bar,0,0);browserLayout.Controls.Add(actions,0,1);browserLayout.Controls.Add(browserDock,0,2);browserPane.Controls.Add(browserLayout);diagnose.Click+=async(_,_)=>{if(busy)return;try{EnableBrowserTools();await RunQuick(new("browser_snapshot"),BrowserUi("Проверка доступа: текст и элементы текущей страницы","Checking access: visible page text and elements","正在检查访问权限：页面可见文本和元素"));}catch(Exception e){Post("toast",e.Message);}};capture.Click+=async(_,_)=>{if(busy)return;try{await RunQuick(new("browser_capture"),"Снимок текущей страницы");}catch(Exception e){Post("toast",e.Message);}};workspaceSplit.Panel1.Controls.Add(view);workspaceSplit.Panel2.Controls.Add(browserPane);Controls.Add(workspaceSplit);workspaceSplit.BringToFront();
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
