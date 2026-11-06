"""NORA AI v3 · verified 3.5-4B LoRA / 4-bit local inference.

prepare is the only network-enabled operation. serve is strictly offline.
Original LoRA SHA256 recorded in the 2026-11-04 training evaluation report.
"""
import hashlib
import json
import os
from pathlib import Path
import queue
import sys
import threading

BASE_ID = 'Qwen/Qwen3.5-4B'
REVISION = '851bf6e806efd8d0a36b00ddf55e13ccb7b8cd0a'
ADAPTER_SHA256 = '80120cb91816658e67bc3cbbd7f0d751d217923109cf50f99c656083f00a1139'
ADAPTER_SIZE = 3674728


def emit(kind, text='', **extra):
    print(json.dumps({'type': kind, 'text': text, **extra}, ensure_ascii=False), flush=True)


def verify_adapter(home):
    home = Path(home)
    config_path = home/'adapter_config.json'
    weight_path = home/'adapter_model.safetensors'
    for path in (home, config_path, weight_path):
        if path.is_symlink() or (hasattr(path, 'is_junction') and path.is_junction()):
            raise ValueError('Adapter folder contains a link')
    if not config_path.is_file() or not weight_path.is_file():
        raise ValueError('NORA v3 LoRA files are missing')
    config = json.loads(config_path.read_text(encoding='utf-8'))
    if (config.get('base_model_name_or_path') != BASE_ID or
        config.get('peft_type') != 'LORA' or config.get('task_type') != 'CAUSAL_LM' or
        config.get('r') != 8 or set(config.get('target_modules', [])) != {'q_proj', 'v_proj'}):
        raise ValueError('NORA v3 LoRA does not match its original base')
    if weight_path.stat().st_size != ADAPTER_SIZE:
        raise ValueError('NORA v3 LoRA file size mismatch')
    sha = hashlib.sha256()
    with weight_path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            sha.update(block)
    digest = sha.hexdigest()
    if digest != ADAPTER_SHA256:
        raise ValueError('NORA v3 LoRA SHA256 mismatch')
    return True


def validate_request(req):
    context = int(req.get('context_tokens', 4096))
    maximum = int(req.get('max_tokens', 512))
    if context not in (512, 1024, 2048, 4096, 8192) or maximum < 1 or maximum > 2048:
        raise ValueError('NORA v3: context <= 8192 and output <= 2048')
    messages = req.get('messages')
    if not isinstance(messages, list) or not messages or len(messages) > 50:
        raise ValueError('Invalid NORA v3 messages')
    result=[]
    for m in messages:
        if not isinstance(m, dict) or m.get('role') not in ('system', 'user', 'assistant') or not isinstance(m.get('content'), str):
            raise ValueError('Invalid chat role or content')
        result.append({'role': m['role'], 'content': m['content']})
    return result, context, maximum


def prepare(home):
    # This is called only on explicit user consent; inference never accesses the network.
    from huggingface_hub import snapshot_download
    home=Path(home)
    if not home.is_dir(): home.mkdir(parents=True)
    marker = home/'.nora-v3-base-complete'
    marker.unlink(missing_ok=True)
    emit('status', 'Downloading original Qwen3.5-4B base (~9.4 GB), pinned to revision…')
    snapshot_download(repo_id=BASE_ID, revision=REVISION, local_dir=str(home),
                      allow_patterns=['*.safetensors', '*.json', '*.txt', '*.jinja'],
                      ignore_patterns=['*.py', '*.bin', '*.pt', '*.pkl'])
    if not (home/'config.json').is_file() or not list(home.glob('model*.safetensors')):
        raise ValueError('Incomplete Qwen3.5-4B base')
    index=home/'model.safetensors.index.json'
    if index.is_file():
        weight_map=json.loads(index.read_text(encoding='utf8')).get('weight_map',{})
        shards=set(weight_map.values())
        if not shards or any('/' in part or '..' in part or not (home/part).is_file() for part in shards):
            raise ValueError('Incomplete Qwen3.5-4B weight shards')
    marker.write_text(REVISION+'\n',encoding='utf8')
    emit('status', 'Qwen3.5-4B downloaded. Generation remains offline.')


class Runtime:
    def __init__(self, root):
        self.root=Path(root)
        self.model=None
        self.tokenizer=None

    def load(self):
        if self.model is not None: return
        verify_adapter(self.root/'adapter')
        base=self.root/'base'
        marker=base/'.nora-v3-base-complete'
        if (not (base/'config.json').is_file() or not list(base.glob('model*.safetensors'))
            or not marker.is_file() or marker.read_text(encoding='utf8').strip()!=REVISION):
            raise ValueError('Base model missing. Click Install NORA v3 in the model catalog.')
        os.environ['HF_HUB_OFFLINE']='1'
        os.environ['TRANSFORMERS_OFFLINE']='1'
        import torch
        from transformers import AutoTokenizer, AutoModelForImageTextToText, BitsAndBytesConfig
        from peft import PeftModel
        if not torch.cuda.is_available():
            raise RuntimeError('NORA v3 NF4 requires NVIDIA CUDA; use a GGUF model if unavailable.')
        free_vram, _=torch.cuda.mem_get_info()
        if free_vram < 4 * 1024**3:
            raise RuntimeError('Not enough free VRAM: NORA v3 needs ~4 GB free before loading. Close GPU programs.')
        emit('status','Loading Qwen3.5-4B NF4 + verified NORA v3 LoRA into GPU…')
        self.tokenizer=AutoTokenizer.from_pretrained(str(base), local_files_only=True, trust_remote_code=False)
        quant=BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_quant_type='nf4',
                                bnb_4bit_use_double_quant=True, bnb_4bit_compute_dtype=torch.float16)
        model=AutoModelForImageTextToText.from_pretrained(
            str(base), local_files_only=True, trust_remote_code=False, use_safetensors=True,
            dtype=torch.float16, quantization_config=quant, device_map={'': 0}, attn_implementation='sdpa')
        self.model=PeftModel.from_pretrained(model,str(self.root/'adapter'),is_trainable=False,
                                             local_files_only=True)
        self.model.eval()
        emit('status','Original NORA v3 LoRA loaded. Offline inference ready.')

    def reply(self, req):
        messages, context, maximum=validate_request(req)
        self.load()
        import torch
        from transformers import TextIteratorStreamer
        prompt=self.tokenizer.apply_chat_template(messages, tokenize=False,
                         add_generation_prompt=True, enable_thinking=False)
        encoded=self.tokenizer(prompt, add_special_tokens=False, return_tensors='pt')['input_ids'].to('cuda:0')
        remaining=context - encoded.shape[-1]
        if req.get('fit_output'): maximum=min(maximum, remaining)
        if remaining < maximum or maximum < 1:
            raise ValueError('Prompt and answer exceed NORA v3 context. Reduce history or output tokens.')
        streamer=TextIteratorStreamer(self.tokenizer, skip_prompt=True, skip_special_tokens=True, timeout=1)
        result={}; errors=[]
        def work():
            try:
                with torch.inference_mode():
                    result['tokens']=self.model.generate(input_ids=encoded, attention_mask=torch.ones_like(encoded),
                        max_new_tokens=maximum, do_sample=float(req.get('temperature',.7))>0,
                        temperature=max(.1,min(2.,float(req.get('temperature',.7)))),
                        top_p=.95,pad_token_id=self.tokenizer.eos_token_id, streamer=streamer)
            except BaseException as e: errors.append(e)
        thread=threading.Thread(target=work,daemon=True);thread.start()
        parts=[]
        while thread.is_alive() or not streamer.text_queue.empty():
            try:
                token=next(streamer);parts.append(token);emit('token',token)
            except StopIteration: break
            except queue.Empty:
                if errors:raise errors[0]
        thread.join()
        if errors: raise errors[0]
        output=''.join(parts)
        used=result.get('tokens')
        limited=bool(used is not None and used.shape[-1]-encoded.shape[-1]>=maximum)
        emit('done',output,limit_reached=limited)


def main():
    if len(sys.argv)!=3: raise ValueError('usage: nora_v3_backend.py ROOT prepare|serve|check')
    root=Path(sys.argv[1]);action=sys.argv[2]
    if action=='check':verify_adapter(root/'adapter');emit('done','NORA v3 adapter verified');return
    if action=='prepare':verify_adapter(root/'adapter');prepare(root/'base');return
    if action!='serve':raise ValueError('Invalid action')
    os.environ['HF_HUB_OFFLINE']='1';os.environ['TRANSFORMERS_OFFLINE']='1'
    runtime=Runtime(root)
    for line in sys.stdin:
        try:runtime.reply(json.loads(line))
        except Exception as e:
            emit('error',f'{type(e).__name__}: {e}')

if __name__=='__main__':
    try:main()
    except Exception as exc:
        emit('error',f'{type(exc).__name__}: {exc}')
        sys.exit(1)
