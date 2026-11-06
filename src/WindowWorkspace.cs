using System.Runtime.InteropServices;
namespace LocalChat;
partial class MainWindow {
 bool fullscreen=true;Rectangle restoredBounds;ChatSession? deletedChat;
 bool SupportsThinking()=>settings.Mode is not (3 or 4)&&AirCatalog.Active(root,settings.Mode,settings.ModelPath) is not {Thinking:false}&&ModelCatalog.Active(root,settings.Mode,settings.GgufModel) is not {Thinking:false};
 [DllImport("user32.dll")]static extern bool ReleaseCapture();
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hWnd,int msg,IntPtr wParam,IntPtr lParam);
 void InitWindowChrome(){FormBorderStyle=FormBorderStyle.None;KeyPreview=true;restoredBounds=new(80,60,1280,820);Bounds=Screen.FromControl(this).Bounds;
  KeyDown+=(_,e)=>{if(e.KeyCode==Keys.F11){ToggleFullscreen();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape&&fullscreen){ToggleFullscreen();e.SuppressKeyPress=true;}};
 }
 void ToggleFullscreen(){if(fullscreen){WindowState=FormWindowState.Normal;Bounds=restoredBounds;fullscreen=false;}else{restoredBounds=Bounds;WindowState=FormWindowState.Normal;Bounds=Screen.FromControl(this).Bounds;fullscreen=true;}Post("windowState",new{fullscreen});}
 void WindowCommand(string command){switch(command){case "fullscreen":ToggleFullscreen();break;case "minimize":WindowState=FormWindowState.Minimized;break;case "close":Close();break;case "drag":if(!fullscreen){ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}break;}}
 void RenameChat(string id,string title){title=title.Trim();if(title.Length is <1 or >100)throw new Exception("Название: от 1 до 100 символов");var chat=chats.SingleOrDefault(c=>c.Id==id)??throw new Exception("Чат не найден");chat.Title=title;Disk.Save(ChatPath(chat.Id),chat);State();}
 void DeleteChat(string id){var chat=chats.SingleOrDefault(c=>c.Id==id);if(chat==null)return;string trash=Path.Combine(root,"chat-trash");Directory.CreateDirectory(trash);Disk.Save(Path.Combine(trash,Disk.Id(id)+".json"),chat);if(File.Exists(AgentTaskPath(id)))File.Move(AgentTaskPath(id),Path.Combine(trash,Disk.Id(id)+".agent-task.json"),true);File.Delete(ChatPath(id));chats.Remove(chat);deletedChat=chat;feedback.RemoveAll(e=>e.ChatId==id);Disk.Save(Path.Combine(root,"feedback.json"),feedback);
  if(current.Id==id){current=chats.OrderByDescending(c=>c.UpdatedAt).FirstOrDefault()??new();if(!chats.Contains(current))chats.Add(current);SaveCurrent();}SaveSettings();State();Post("chatDeleted",new{id});
 }
 void RestoreDeletedChat(){if(deletedChat==null)return;var chat=deletedChat;deletedChat=null;if(!chats.Any(c=>c.Id==chat.Id)){chats.Add(chat);Disk.Save(ChatPath(chat.Id),chat);}File.Delete(Path.Combine(root,"chat-trash",Disk.Id(chat.Id)+".json"));string checkpoint=Path.Combine(root,"chat-trash",Disk.Id(chat.Id)+".agent-task.json");if(File.Exists(checkpoint)){Directory.CreateDirectory(Path.GetDirectoryName(AgentTaskPath(chat.Id))!);File.Move(checkpoint,AgentTaskPath(chat.Id),true);}current=chat;foreach(var message in chat.Messages.Where(m=>m.Role=="assistant"&&m.State=="complete"&&m.Rating!=0))FeedbackMemory.Set(feedback,chat,message.Id,message.Rating,message.FeedbackNote);Disk.Save(Path.Combine(root,"feedback.json"),feedback);SaveSettings();State();Post("toast","Чат восстановлен");}
}
