namespace LocalChat;
static class StartupLog {
 internal static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NORA","logs","startup.log");
 static readonly object gate=new();
 internal static void Write(string stage,Exception error)=>Write(stage,error.GetType().Name+" (0x"+error.HResult.ToString("X8")+"): "+error.Message);
 internal static void Write(string stage,string detail=""){
  try{lock(gate){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);if(File.Exists(Path)&&new FileInfo(Path).Length>256*1024)File.Move(Path,Path+".previous",true);File.AppendAllText(Path,DateTimeOffset.Now.ToString("O")+" | "+stage+" | "+detail.Replace('\r',' ').Replace('\n',' ')[..Math.Min(detail.Length,2000)]+Environment.NewLine);}}catch{}
 }
}
