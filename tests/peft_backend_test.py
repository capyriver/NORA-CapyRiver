"""Actual tiny Qwen2/LoRA mechanics plus data-only security tests; not 7B quality evaluation."""
import importlib.util,json,pathlib,tempfile,hashlib,contextlib,io,copy
S=pathlib.Path(__file__).resolve().parents[1]/'src'
spec=importlib.util.spec_from_file_location('worker',S/'peft_backend.py');w=importlib.util.module_from_spec(spec);spec.loader.exec_module(w)
checks=[]
def check(value,name):
 assert value,name
 checks.append(name)
def rejects(fn,name):
 try:fn()
 except (ValueError,RuntimeError):checks.append(name);return
 raise AssertionError(name)
models,base=w.load_manifests(S)
check(len(models)==16,'16 unique audited adapters')
check(all(m['likes']>0 for m in models),'all have nonzero likes')
for m in models:
 w.validate_record(m)
check(sum(m['weightBytes'] for m in models)==3714874960,'adapter weights separate from base and prompt catalog')
for mutate,name in [(lambda m:m.update(revision='main'),'floating revisions rejected'),(lambda m:m['files'][0].update(name='../bad.py'),'path traversal and Python rejected'),(lambda m:m['files'][0].update(url='http://evil.test/pickle'),'untrusted URL rejected'),(lambda m:m.update(base='Qwen/Qwen2.5-Coder-7B'),'wrong base rejected')]:
 m=copy.deepcopy(models[0]);mutate(m);rejects(lambda:w.validate_record(m),name)
with tempfile.TemporaryDirectory() as d:
 p=pathlib.Path(d);f=p/'weights';f.write_bytes(b'actual-weights');record={'bytes':14,'sha256':hashlib.sha256(b'actual-weights').hexdigest()};check(w.verified(f,record),'correct SHA-256 accepted');f.write_bytes(b'changed-weight');check(not w.verified(f,record),'same-size tampering rejected')
 (p/'link').symlink_to(p,target_is_directory=True);rejects(lambda:w.safe_home(p/'link','base'),'linked cache rejected')
 (p/'sub').mkdir();(p/'linkparent').symlink_to(p/'sub',target_is_directory=True);rejects(lambda:w.safe_home(p/'linkparent'/'nested','base'),'linked ancestor rejected')
 config={'base_model_name_or_path':w.BASE_ID,'peft_type':'LORA','task_type':'CAUSAL_LM'}
 home=p/'testadapter';home.mkdir();(home/'adapter_config.json').write_text(json.dumps(config));(home/'adapter_model.safetensors').write_bytes(b'test-not-real-tensors')
 m=copy.deepcopy(models[0]);m['files']=[]
 for n in ['adapter_config.json','adapter_model.safetensors']:
  data=(home/n).read_bytes();m['files'].append({'name':n,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),'url':f"https://huggingface.co/{m['id']}/resolve/{m['revision']}/{n}"})
 w.verify_record(m,home);check(True,'pinned files verify before inference')
 (home/'model.py').write_text('raise Exception("must not run")');rejects(lambda:w.verify_record(m,home),'extra custom Python rejected');(home/'model.py').unlink()
 (home/'optimizer.pt').write_bytes(b'pickle');rejects(lambda:w.verify_record(m,home),'extra pickle rejected')
# Real trained nonzero LoRA on a tiny Qwen2, exact loader function used by runtime.
import torch
from transformers import Qwen2Config,Qwen2ForCausalLM,PreTrainedTokenizerFast
from tokenizers import Tokenizer
from tokenizers.models import WordLevel
from tokenizers.pre_tokenizers import Whitespace
from peft import LoraConfig,get_peft_model
from safetensors.torch import load_file

torch.manual_seed(21);torch.set_num_threads(2)
config=Qwen2Config(vocab_size=32,hidden_size=32,intermediate_size=64,num_hidden_layers=2,num_attention_heads=4,num_key_value_heads=2,max_position_embeddings=512,bos_token_id=1,eos_token_id=2,pad_token_id=0)
base_model=Qwen2ForCausalLM(config).eval();base_state=copy.deepcopy(base_model.state_dict());tokens=torch.tensor([[1,4,5,6,7]])
with torch.no_grad():before=base_model(tokens).logits.clone()
trained=get_peft_model(base_model,LoraConfig(r=4,lora_alpha=8,target_modules=['q_proj','v_proj'],task_type='CAUSAL_LM',lora_dropout=0))
optimizer=torch.optim.AdamW((p for p in trained.parameters() if p.requires_grad),lr=.05)
for _ in range(6):
 optimizer.zero_grad();loss=trained(tokens,labels=tokens).loss;loss.backward();optimizer.step()
trained.eval()
with tempfile.TemporaryDirectory() as d:
 home=pathlib.Path(d);trained.save_pretrained(home,safe_serialization=True)
 check((home/'adapter_model.safetensors').is_file(),'trained safetensors really saved')
 fresh=Qwen2ForCausalLM(config).eval();fresh.load_state_dict(base_state)
 loaded=w.attach_adapter(fresh,home).eval()
 with torch.no_grad():after=loaded(tokens).logits.clone()
 check(not torch.allclose(before,after),'actual LoRA changes Qwen logits (not a prompt)')
 with loaded.disable_adapter(),torch.no_grad():disabled=loaded(tokens).logits
 check(torch.allclose(before,disabled,atol=1e-6),'disabling LoRA restores original model output')
 clean=Qwen2ForCausalLM(config).eval();clean.load_state_dict(base_state)
 check(torch.allclose(before,clean(tokens).logits,atol=1e-6),'switching to clean base does not stack adapters')
 # Real end-to-end streaming through Runtime.reply with tiny model/tokenizer on CPU.
 tokenizer_raw=Tokenizer(WordLevel({'[PAD]':0,'[BOS]':1,'[EOS]':2,'[UNK]':3,'user':4,'assistant':5,'hello':6,'system':7},unk_token='[UNK]'));tokenizer_raw.pre_tokenizer=Whitespace()
 tokenizer=PreTrainedTokenizerFast(tokenizer_object=tokenizer_raw,bos_token='[BOS]',eos_token='[EOS]',pad_token='[PAD]',unk_token='[UNK]');tokenizer.chat_template="{% for m in messages %}{{ m['role'] }} {{ m['content'] }} {% endfor %}assistant"
 runtime=w.Runtime(home,S);runtime.model=loaded;runtime.key=models[0]['id'];runtime.tokenizer=tokenizer
 out=io.StringIO()
 with contextlib.redirect_stdout(out):runtime.reply({'adapter_id':models[0]['id'],'messages':[{'role':'user','content':'hello'}],'context_tokens':512,'max_tokens':5})
 events=[json.loads(l) for l in out.getvalue().splitlines()];check(any(x['type']=='token' for x in events) and events[-1]['type']=='done','actual Qwen+LoRA streams JSON tokens and completion')
 check('limit_reached' in events[-1],'token limit reported by real generation')
# Confirm all real downloaded configurations can be parsed by pinned PEFT version.
configs_path=S.parents[1]/'adapter-configs.json'
if configs_path.exists():
 from peft import LoraConfig
 configs=json.loads(configs_path.read_text())
 with tempfile.TemporaryDirectory() as d:
  for name,c in configs.items():
   (pathlib.Path(d)/'adapter_config.json').write_text(json.dumps(c));parsed=LoraConfig.from_pretrained(d);check(parsed.peft_type=='LORA','real config parses: '+name)
report={'passed':len(checks),'checks':checks,'scope':'tiny CPU Qwen2 mechanics; not 7B CUDA inference or adapter quality'}
print(json.dumps(report,ensure_ascii=False,indent=2))
