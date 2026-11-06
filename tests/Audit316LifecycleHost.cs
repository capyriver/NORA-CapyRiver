namespace LocalChat;
partial class MainWindow {
 bool busy,closing;Message? workMessage;string activeMediaContext="",activeBrowserContext="";public bool FailState,FailPublish;public List<(string Type,object Value)> Posts=new();
 void State(){if(FailState)throw new Exception("UI state failed");}
 void Post(string type,object value)=>Posts.Add((type,value));
 void PublishBrowser(){if(FailPublish)throw new Exception("browser publish failed");}
 public bool CleanupReleased=>!busy&&generation==null&&workMessage==null&&activeMediaContext==""&&activeBrowserContext=="";
 public void FaultInjectionCleanup(){busy=true;generation=new();var message=new Message{StartedAt=DateTime.UtcNow};workMessage=message;activeMediaContext="media";activeBrowserContext="page";
 activeAgentTask=new(){ChatId=current.Id,Goal="in progress"};Directory.CreateDirectory(AgentTaskPath(current.Id));Directory.CreateDirectory(Path.Combine(root,"chat.json"));browser=new(){FailManual=true};FailState=true;FailPublish=true;CompleteWork(message);
 }
}
