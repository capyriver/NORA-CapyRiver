using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
namespace LocalChat;
record ChatMessage(string role, string content);
static class LocalApi {
    public static Uri Endpoint(string text) {
        if (!Uri.TryCreate(text.Trim().TrimEnd('/')+"/",UriKind.Absolute,out var uri) || uri.Scheme!="http" ||
            !new[]{"localhost","127.0.0.1","[::1]","::1"}.Contains(uri.Host.ToLowerInvariant()) ||
            uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0)
            throw new ArgumentException("Разрешён только локальный HTTP-сервер: например http://127.0.0.1:1234/v1. Облачные адреса не поддерживаются.");
        return uri;
    }
    public static HttpClient Client() => new(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
    public static async Task<string> ChatAsync(Uri endpoint,string model,List<ChatMessage> messages,int maxTokens,Action<string> onToken,CancellationToken token,double temperature=0.7,List<string>? images=null,bool thinking=false,Action<string>? onFinish=null,JsonElement? responseSchema=null) {
        using var overall=CancellationTokenSource.CreateLinkedTokenSource(token);overall.CancelAfter(TimeSpan.FromMinutes(5));
        using var stall=CancellationTokenSource.CreateLinkedTokenSource(overall.Token);stall.CancelAfter(TimeSpan.FromSeconds(90));
        try{return await StreamReply(endpoint,model,messages,maxTokens,onToken,stall,temperature,images,thinking,onFinish,responseSchema);}
        catch(OperationCanceledException)when(!token.IsCancellationRequested){throw new TimeoutException("Модель не ответила вовремя (90 с до первого токена, 30 с простоя, максимум 5 минут). Выбери меньшую GGUF-модель или уменьши контекст.");}
    }
    static async Task<string> StreamReply(Uri endpoint,string model,List<ChatMessage> messages,int maxTokens,Action<string> onToken,CancellationTokenSource stall,double temperature,List<string>? images,bool thinking,Action<string>? onFinish,JsonElement? responseSchema){
        var token=stall.Token;
        using var client=Client();using var request=new HttpRequestMessage(HttpMethod.Post,new Uri(endpoint,"chat/completions"));
        object[] payload=messages.Select((m,i)=>(object)new{m.role,content=(object)m.content}).ToArray();
        if(images is {Count:>0}){
            int last=messages.FindLastIndex(m=>m.role=="user");if(last<0)throw new Exception("Нет пользовательского сообщения для изображения.");
            var parts=new List<object>{new{type="text",text=messages[last].content}};parts.AddRange(images.Select(url=>(object)new{type="image_url",image_url=new{url}}));payload[last]=new{role="user",content=parts};
        }
        var requestBody=new Dictionary<string,object>{["model"]=model,["messages"]=payload,["stream"]=true,["temperature"]=temperature,["max_tokens"]=maxTokens,["chat_template_kwargs"]=new{enable_thinking=thinking}};
        if(responseSchema.HasValue)requestBody["response_format"]=new{type="json_schema",json_schema=new{name="nora_mcp_call",strict=false,schema=responseSchema.Value}};
        request.Content=JsonContent.Create(requestBody);
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
        if(!response.IsSuccessStatusCode){using var errorStream=await response.Content.ReadAsStreamAsync(token);using var errorReader=new StreamReader(errorStream);char[] errorChars=new char[1200];int got=await errorReader.ReadBlockAsync(errorChars.AsMemory(),token);var body=new string(errorChars,0,got);throw new Exception($"LM Studio HTTP {(int)response.StatusCode}: "+body[..Math.Min(body.Length,1200)]);}
        using var stream=await response.Content.ReadAsStreamAsync(token);using var reader=new StreamReader(stream);bool finished=false;var result=new StringBuilder();
        while(await reader.ReadLineAsync(token) is string line){
            if(!line.StartsWith("data:"))continue;string data=line[5..].Trim();
            if(data=="[DONE]"){finished=true;break;}if(data.Length==0)continue;
            using var d=JsonDocument.Parse(data);var root=d.RootElement;
            if(root.TryGetProperty("error",out var err))throw new Exception(err.ToString());
            if(!root.TryGetProperty("choices",out var choices)||choices.GetArrayLength()==0)continue;
            var choice=choices[0];
            if(choice.TryGetProperty("delta",out var delta)&&delta.TryGetProperty("content",out var content)&&content.ValueKind==JsonValueKind.String){var text=content.GetString()??"";if(text.Length>0){stall.CancelAfter(TimeSpan.FromSeconds(30));if(result.Length+text.Length>1_000_000)throw new Exception("Ответ модели превышает 1 млн символов; выполнение остановлено.");result.Append(text);onToken(text);}}
            if(choice.TryGetProperty("delta",out var thought)&&thought.TryGetProperty("reasoning_content",out var reasoning)&&reasoning.ValueKind==JsonValueKind.String&&reasoning.GetString()?.Length>0)stall.CancelAfter(TimeSpan.FromSeconds(30));
            if(choice.TryGetProperty("finish_reason",out var reason)&&reason.ValueKind==JsonValueKind.String){string finish=reason.GetString()??"";if(finish is not("stop" or "length"))throw new Exception("Сервер не завершил текстовый ответ: finish_reason="+finish+". Ответ не считается завершённым.");finished=true;onFinish?.Invoke(finish);}
        }
        if(!finished)throw new Exception("Сервер оборвал поток до завершения ответа. Неполный ответ не добавлен в контекст.");
        if(result.Length==0)throw new Exception("Сервер не вернул текст. Проверь модель или увеличь лимит токенов (возможно, он ушёл на thinking).");
        return result.ToString();
    }
}
