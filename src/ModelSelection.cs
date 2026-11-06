namespace LocalChat;
static class ModelSelection {
 public static void Apply(Settings next,CatalogModel spec,DirectSetup.Result paths){next.Mode=2;next.GgufModel=paths.Model;next.LlamaServer=paths.Server;next.CpuFallbackServer=paths.CpuServer;next.FastQwenMode=true;next.VulkanFallbackServer=paths.Server;next.CpuOnly=false;next.Reasoning=false;next.ResponseMode=0;if(next.ContextTokens<4096)next.ContextTokens=4096;if(next.MaxTokens<512)next.MaxTokens=512;if(!spec.Thinking){next.Reasoning=false;next.ResponseMode=0;}}
}
