namespace LocalChat;
partial class MainWindow {
 void EndWork(Message message){
  var errors=OperationCleanup.Run(
   ()=>{if(activeAgentTask is {Status:"running"}){activeAgentTask.Status="paused";activeAgentTask.Reason="Выполнение прервано. Результат незавершённого вызова необходимо проверить перед повтором.";message.TaskPending=true;message.TaskStatus="paused";SaveAgentTask();}},
   ()=>{activeMediaContext="";activeBrowserContext="";if(message.StartedAt is DateTime at)message.ElapsedSeconds=Math.Max(0,(DateTime.UtcNow-at).TotalSeconds);if(workMessage==message)workMessage=null;},
   ()=>{if(!closing&&browser is {IsDisposed:false})browser.SetManual(true);},
   ()=>{if(!closing)PublishBrowser();});
  if(errors.Count>0)throw new AggregateException("Не удалось полностью сохранить/завершить операцию",errors);
 }
 void CompleteWork(Message response){
  var errors=OperationCleanup.Run(()=>EndWork(response),()=>SaveCurrent(),()=>{busy=false;},()=>{var old=generation;generation=null;old?.Dispose();},()=>{if(!closing)State();},()=>{if(!closing)Post("busy",false);});
  if(!closing&&errors.Count>0)try{Post("toast","Операция остановлена, но при сохранении/обновлении произошла ошибка: "+string.Join("; ",errors.Select(e=>e.GetBaseException().Message)));}catch{} // Cleanup must not lock the UI.
 }
}
