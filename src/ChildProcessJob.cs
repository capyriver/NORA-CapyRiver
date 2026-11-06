using System.Runtime.InteropServices;
using System.Diagnostics;
namespace LocalChat;
// Own only NORA's child processes. Windows closes the job handle on app exit/crash.
sealed class ChildProcessJob:IDisposable {
 IntPtr handle;
 [StructLayout(LayoutKind.Sequential)]struct Basic {public long Time1,Time2;public uint Flags;public UIntPtr Working1,Working2;public uint Count;public UIntPtr Affinity;public uint Priority,Scheduling;}
 [StructLayout(LayoutKind.Sequential)]struct Io {public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
 [StructLayout(LayoutKind.Sequential)]struct Extended {public Basic Basic;public Io Io;public UIntPtr ProcessLimit,JobLimit,ProcessPeak,JobPeak;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateJobObject(IntPtr security,string? name);
 [DllImport("kernel32.dll")]static extern bool SetInformationJobObject(IntPtr job,int infoClass,ref Extended info,uint length);
 [DllImport("kernel32.dll")]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 public ChildProcessJob(){handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)return;var info=new Extended{Basic=new Basic{Flags=0x2000}};if(!SetInformationJobObject(handle,9,ref info,(uint)Marshal.SizeOf<Extended>())){CloseHandle(handle);handle=IntPtr.Zero;}}
 public void Attach(Process process){if(handle!=IntPtr.Zero)AssignProcessToJobObject(handle,process.Handle);}
 public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}GC.SuppressFinalize(this);}
}
