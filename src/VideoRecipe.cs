using System.Diagnostics;
using System.Text;
namespace LocalChat;
static class VideoRecipe {
 public const int MaxFrames=60;
 public const long MaxBytes=256L*1024*1024;
 public static string Manifest(int count){if(count<1||count>MaxFrames)throw new ArgumentOutOfRangeException(nameof(count),"Выбери от 1 до 60 изображений.");var s=new StringBuilder("ffconcat version 1.0\n");for(int i=0;i<count;i++)s.Append($"file frame-{i:D3}.png\nduration 3\n");s.Append($"file frame-{count-1:D3}.png\n");return s.ToString();}
 public static string[] Arguments(string manifest,string output,int count){Manifest(count);if(!Path.IsPathFullyQualified(manifest)||!Path.IsPathFullyQualified(output)||!output.EndsWith(".mp4",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Нужны абсолютные пути к manifest и MP4.");return new[]{"-hide_banner","-nostdin","-n","-protocol_whitelist","file","-safe","1","-f","concat","-i",manifest,"-t",(count*3).ToString(System.Globalization.CultureInfo.InvariantCulture),"-an","-c:v","libx264","-preset","veryfast","-crf","23","-pix_fmt","yuv420p","-r","24","-movflags","+faststart","-fs",MaxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),output};}
 public static async Task Render(string executable,string manifest,string output,int count,CancellationToken token){
  if(!Path.IsPathFullyQualified(executable)||!File.Exists(executable))throw new Exception("Выбери установленный FFmpeg.");if(File.Exists(output))throw new IOException("Выходной файл уже существует.");
  var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=Path.GetDirectoryName(manifest)!};foreach(var a in Arguments(manifest,output,count))info.ArgumentList.Add(a);
  using var p=new Process{StartInfo=info};using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(10));
  bool started=false;try{started=p.Start();if(!started)throw new Exception("Не удалось запустить FFmpeg.");using var stop=timeout.Token.Register(()=>{try{if(!p.HasExited)p.Kill(true);}catch{}});var errors=p.StandardError.ReadToEndAsync();var stdout=p.StandardOutput.ReadToEndAsync();await p.WaitForExitAsync(timeout.Token);string detail=await errors;await stdout;timeout.Token.ThrowIfCancellationRequested();if(p.ExitCode!=0)throw new Exception("FFmpeg не создал MP4: "+detail[..Math.Min(detail.Length,1200)]);var f=new FileInfo(output);if(!f.Exists||f.Length<32||f.Length>=MaxBytes-65536)throw new Exception("Видео пустое либо достигло лимита 256 МБ.");using var stream=File.OpenRead(output);byte[] header=new byte[12];if(stream.Read(header)!=12||Encoding.ASCII.GetString(header,4,4)!="ftyp")throw new Exception("FFmpeg вернул не MP4.");}
  catch{if(started){try{if(!p.HasExited)p.Kill(true);await p.WaitForExitAsync();}catch{}try{File.Delete(output);}catch{}}throw;}
 }
}
