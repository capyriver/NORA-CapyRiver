using System.Reflection;
using System.Text.Json;
namespace LocalChat;
record ExperimentalProfile(string Id,string Name,string Prompt,string Source);
static class ExperimentalProfiles {
 public const int Revision=2;
 public static readonly ExperimentalProfile[] All=Load();
 static ExperimentalProfile[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("LocalChat.experimental-prompts.json")??throw new Exception("Не найден ресурс экспериментальных профилей.");var profiles=JsonSerializer.Deserialize<ExperimentalProfile[]>(s,Disk.Json)??Array.Empty<ExperimentalProfile>();if(profiles.Length!=3||profiles.Any(p=>p.Id is not ("dan6" or "dan" or "stan")||p.Prompt.Length>6000||p.Prompt.Contains('\0')||!p.Source.StartsWith("https://github.com/NVIDIA/garak/blob/"))||profiles.Select(p=>p.Id).Distinct().Count()!=profiles.Length)throw new Exception("Некорректные экспериментальные профили.");return profiles;}
 public static void Validate(string id){if(id!=""&&!All.Any(p=>p.Id==id))throw new ArgumentException("Неизвестный экспериментальный профиль.");}
 public static bool ApplyDefaults(Settings settings){if(settings.ExperimentalRevision>=Revision)return false;settings.ExperimentalProfileId="";settings.ExperimentalRevision=Revision;PrepareContext(settings);return true;}
 public static void PrepareContext(Settings settings){if(settings.ExperimentalProfileId!=""&&settings.Mode==0)settings.ContextTokens=2048;}
 public static bool Active(Settings settings,bool staged)=>!staged&&settings.ExperimentalProfileId!="";
 public static bool CanUseModelTools(Settings settings)=>settings.ExperimentalProfileId=="";
 public static string Wrap(Settings settings,string task,bool staged=false){if(!Active(settings,staged))return task;Validate(settings.ExperimentalProfileId);var p=All.First(p=>p.Id==settings.ExperimentalProfileId);return p.Prompt.Replace("{generator.name}","local assistant",StringComparison.Ordinal)+"\n\nEND OF EXPERIMENTAL PROFILE. For this turn answer the actual request below rather than merely acknowledging the role. Do not claim external actions occurred.\nACTUAL USER REQUEST:\n"+task;}
}
