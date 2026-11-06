namespace LocalChat;
partial class MainWindow {
 readonly SemaphoreSlim approvalGate=new(1,1);
 readonly Dictionary<string,TaskCompletionSource<bool>> approvals=new();
 async Task<bool> ConfirmAsync(string title,string description,CancellationToken ct=default){
  await approvalGate.WaitAsync(ct);string id=Guid.NewGuid().ToString("N");
  try{ct.ThrowIfCancellationRequested();var decision=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);approvals[id]=decision;
   Post("approval",new{id,title,description});
   return await decision.Task.WaitAsync(TimeSpan.FromMinutes(5),ct);
  }catch(TimeoutException){Post("toast","Запрос доступа истёк. Действие не выполнено.");return false;}
  finally{approvals.Remove(id);Post("approvalDone",new{id});approvalGate.Release();}
 }
 Task<bool> ApproveAsync(string tool,string description,CancellationToken ct)=>WorkspaceCore.NeedsApproval(settings.AccessMode,tool,settings.ScreenAllowed)?ConfirmAsync("Разрешить действие?",description,ct):Task.FromResult(true);
 void ResolveApproval(string id,bool allow){if(approvals.TryGetValue(id,out var decision))decision.TrySetResult(allow);}
 void CancelApprovals(){foreach(var pending in approvals.Values)pending.TrySetResult(false);}
}
