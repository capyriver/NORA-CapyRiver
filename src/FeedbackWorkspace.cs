namespace LocalChat;
partial class MainWindow {
 List<FeedbackEntry> feedback=new();string activeMediaContext="",activeBrowserContext="";
 void LoadFeedback(){feedback=Disk.Read<List<FeedbackEntry>>(Path.Combine(root,"feedback.json"))??new();}
 void RateMessage(string id,int rating,string note){FeedbackMemory.Set(feedback,current,id,rating,note);Disk.Save(Path.Combine(root,"feedback.json"),feedback);SaveCurrent();State();Post("toast",rating==0?"Оценка снята":"Оценка сохранена и будет учтена в следующих ответах через память. Веса модели не менялись.");}
 void CopyMessage(string id){var message=current.Messages.SingleOrDefault(m=>m.Id==id)??throw new Exception("Сообщение не найдено");Clipboard.SetText(message.Text.Length==0?" ":message.Text);Post("toast","Текст скопирован");}
}
