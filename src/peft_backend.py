"""NORA PEFT worker. Only pinned data files; never import code from model repos."""
import hashlib
import json
import os
from pathlib import Path
import queue
import shutil
import sys
import threading
import time
import urllib.request

BASE_ID = 'Qwen/Qwen2.5-Coder-7B-Instruct'
ADAPTER_FILES = {'README.md', 'LICENSE', 'LICENSE.md', 'LICENSE.txt', 'adapter_config.json', 'adapter_model.safetensors'}
BASE_FILES = {'LICENSE', 'config.json', 'generation_config.json', 'merges.txt', 'model.safetensors.index.json', 'tokenizer.json', 'tokenizer_config.json', 'vocab.json', *[f'model-{i:05d}-of-00004.safetensors' for i in range(1, 5)]}
VERSIONS = {'torch': '2.8.0', 'transformers': '4.56.2', 'peft': '0.17.1', 'accelerate': '1.10.1', 'safetensors': '0.6.2', 'huggingface_hub': '0.34.4', 'bitsandbytes': '0.47.0'}

def emit(kind, text='', **extra):
    print(json.dumps({'type': kind, 'text': text, **extra}, ensure_ascii=False), flush=True)

def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def safe_home(root, name):
    root = Path(root).absolute()
    if root.is_symlink() or (hasattr(root, 'is_junction') and root.is_junction()):
        raise ValueError('Cache is a link/junction')
    home = root / name
    for part in [*root.parents, root, home]:
        if part.is_symlink() or (hasattr(part, 'is_junction') and part.is_junction()):
            raise ValueError('Cache contains a link/junction')
    return home

def adapter_home(root, model):
    return safe_home(root, 'adapter-' + hashlib.sha256(model['id'].encode()).hexdigest()[:20])

def validate_record(record, base=False):
    import re
    allowed = BASE_FILES if base else ADAPTER_FILES
    if base and record['id'] != BASE_ID:
        raise ValueError('Wrong base model')
    if not base and record.get('base') != BASE_ID:
        raise ValueError('Adapter needs a different base')
    if not re.fullmatch(r'[a-f0-9]{40}', record['revision']):
        raise ValueError('Unpinned revision')
    names = [f['name'] for f in record['files']]
    required = BASE_FILES if base else {'adapter_config.json', 'adapter_model.safetensors'}
    if len(names) != len(set(names)) or not required.issubset(names):
        raise ValueError('Incomplete/duplicate manifest')
    for f in record['files']:
        if f['name'] not in allowed or not re.fullmatch(r'[a-f0-9]{64}', f['sha256']) or f['bytes'] <= 0:
            raise ValueError('Unsafe file entry')
        expected = f"https://huggingface.co/{record['id']}/resolve/{record['revision']}/{f['name']}"
        if f['url'] != expected:
            raise ValueError('Unpinned URL')

def safe_file(home, name):
    p = home / name
    if p.is_symlink() or (hasattr(p, 'is_junction') and p.is_junction()):
        raise ValueError('File is a link')
    return p

def verified(p, spec):
    if not p.is_file() or p.stat().st_size != spec['bytes']:
        return False
    h = hashlib.sha256()
    with p.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest() == spec['sha256']

def verify_record(record, home, base=False):
    validate_record(record, base)
    allowed = {f['name'] for f in record['files']} | {'verified.json'}
    # Model directories are data-only; reject additional pickle/custom Python files.
    if not home.is_dir() or any(p.name not in allowed for p in home.iterdir()):
        raise ValueError('Missing model directory or unexpected files in cache')
    for f in record['files']:
        if not verified(safe_file(home, f['name']), f):
            raise ValueError('SHA-256 failed: ' + f['name'])
    if not base:
        config = read_json(home / 'adapter_config.json')
        if config.get('peft_type') != 'LORA' or config.get('task_type') != 'CAUSAL_LM':
            raise ValueError('Only causal-LM LoRA supported')
        if config.get('base_model_name_or_path') != BASE_ID:
            raise ValueError('Adapter base mismatch')

def download_record(record, home, base=False):
    validate_record(record, base)
    home.mkdir(parents=True, exist_ok=True)
    allowed = {f['name'] for f in record['files']} | {'verified.json'}
    allowed |= {f['name'] + '.part' for f in record['files']}
    if any(p.name not in allowed for p in home.iterdir()):
        raise ValueError('Unexpected files in cache; use a clean NORA cache')
    for f in record['files']:
        path = safe_file(home, f['name'])
        if verified(path, f):
            emit('status', 'SHA-256 OK · ' + record['id'] + ' / ' + f['name'])
            continue
        part = safe_file(home, f['name'] + '.part')
        offset = part.stat().st_size if part.exists() else 0
        if offset > f['bytes']:
            part.unlink(); offset = 0
        if shutil.disk_usage(home).free < f['bytes'] - offset + 512 * 1024 * 1024:
            raise RuntimeError('Недостаточно диска для ' + f['name'])
        last_error = None
        for attempt in range(3):
            try:
                offset = part.stat().st_size if part.exists() else 0
                if offset < f['bytes']:
                    headers = {'User-Agent': 'NORA/3.10', 'Accept-Encoding': 'identity'}
                    if offset:
                        headers['Range'] = f'bytes={offset}-'
                    req = urllib.request.Request(f['url'], headers=headers)
                    with urllib.request.urlopen(req, timeout=90) as response:
                        if not response.geturl().startswith('https://'):
                            raise ValueError('Insecure download redirect')
                        if response.status == 206:
                            if not response.headers.get('Content-Range', '').startswith(f'bytes {offset}-'):
                                raise ValueError('Invalid resume range')
                        elif response.status == 200:
                            offset = 0
                        else:
                            raise ValueError('Unexpected HTTP status')
                        last = 0
                        with part.open('ab' if offset else 'wb') as stream:
                            while True:
                                block = response.read(2 * 1024 * 1024)
                                if not block:
                                    break
                                stream.write(block); offset += len(block)
                                if offset > f['bytes']:
                                    raise ValueError('Oversized download')
                                if time.monotonic() - last > 1:
                                    emit('status', f"{record['id']} · {f['name']} · {offset / 1e6:.0f}/{f['bytes'] / 1e6:.0f} МБ")
                                    last = time.monotonic()
                if not verified(part, f):
                    part.unlink(missing_ok=True)
                    raise ValueError('Downloaded SHA-256 mismatch')
                os.replace(part, path)
                last_error = None
                break
            except Exception as error:
                last_error = error
                if attempt < 2:
                    time.sleep(2 * (attempt + 1))
        if last_error:
            raise last_error
    verify_record(record, home, base)
    (home / 'verified.json').write_text(json.dumps({'id': record['id'], 'revision': record['revision']}), encoding='utf-8')

def load_manifests(folder):
    folder = Path(folder)
    catalog = read_json(folder / 'peft-adapters.json')['models']
    base = read_json(folder / 'peft-base.json')
    if len(catalog) != 16 or len({m['id'] for m in catalog}) != 16:
        raise ValueError('Invalid adapter catalog')
    for model in catalog:
        validate_record(model)
    validate_record(base, True)
    return catalog, base

def check_environment():
    from importlib.metadata import version
    for package, expected in VERSIONS.items():
        if version(package).split('+')[0] != expected:
            raise RuntimeError(f'{package}: нужна версия {expected}')
    import torch
    import bitsandbytes
    if not torch.cuda.is_available():
        raise RuntimeError('PEFT 4-bit требует NVIDIA CUDA. Установи совместимый драйвер; GGUF продолжит работать отдельно.')
    if torch.cuda.get_device_capability()[0] < 6:
        raise RuntimeError('GPU не поддерживает используемую NF4-квантизацию')
    emit('status', 'PEFT/CUDA готовы · ' + torch.cuda.get_device_name(0))

def attach_adapter(base_model, home):
    from peft import PeftModel, LoraConfig
    import inspect
    config = read_json(Path(home) / 'adapter_config.json')
    supported = set(inspect.signature(LoraConfig).parameters)
    for key, value in config.items():
        if key not in supported and key != 'peft_version' and value not in (None, False, {}, []):
            raise ValueError('Unsupported active PEFT option: ' + key)
    return PeftModel.from_pretrained(base_model, str(home), is_trainable=False, local_files_only=True)

class Runtime:
    def __init__(self, root, manifest_folder):
        self.root = root
        self.catalog, self.base = load_manifests(manifest_folder)
        self.model = None
        self.key = None

    def load(self, adapter_id):
        if self.key == adapter_id and self.model is not None:
            return
        check_environment()
        model = next((m for m in self.catalog if m['id'] == adapter_id), None)
        if model is None:
            raise ValueError('Unknown adapter')
        base_home = safe_home(self.root, 'base')
        home = adapter_home(self.root, model)
        emit('status', 'Проверка SHA-256 базы и адаптера перед загрузкой…')
        verify_record(self.base, base_home, True)
        verify_record(model, home)
        # Imports and inference are entirely offline. No custom model code or pickle.
        os.environ['HF_HUB_OFFLINE'] = '1'
        os.environ['TRANSFORMERS_OFFLINE'] = '1'
        import torch
        from transformers import AutoModelForCausalLM, AutoTokenizer, BitsAndBytesConfig
        from peft import PeftModel
        if self.model is not None:
            self.model = None
            import gc
            gc.collect(); torch.cuda.empty_cache()
        free_gpu, _ = torch.cuda.mem_get_info()
        # Windows physical RAM, not virtual commit/pagefile; leave space for browser and OS.
        import ctypes
        class Memory(ctypes.Structure):
            _fields_ = [('length', ctypes.c_ulong), ('load', ctypes.c_ulong), *[(n, ctypes.c_ulonglong) for n in ['total', 'available', 'total_page', 'available_page', 'total_virtual', 'available_virtual', 'extended']]]
        mem = Memory(); mem.length = ctypes.sizeof(Memory)
        if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(mem)):
            raise RuntimeError('Не удалось измерить свободную ОЗУ')
        cpu_budget = min(int(mem.available * .65), 24 * 1024**3)
        gpu_budget = int(free_gpu) - 768 * 1024**2
        if gpu_budget < 2 * 1024**3 or cpu_budget < 3 * 1024**3:
            raise RuntimeError('Для PEFT 7B недостаточно свободной VRAM/ОЗУ. Закрой приложения или используй GGUF без адаптера.')
        emit('status', 'Загрузка базы NF4 + одного LoRA. Первый запуск может занять несколько минут…')
        self.tokenizer = AutoTokenizer.from_pretrained(str(base_home), local_files_only=True, trust_remote_code=False)
        quant = BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_quant_type='nf4', bnb_4bit_use_double_quant=True, bnb_4bit_compute_dtype=torch.float16, llm_int8_enable_fp32_cpu_offload=True)
        base_model = AutoModelForCausalLM.from_pretrained(str(base_home), local_files_only=True, trust_remote_code=False, use_safetensors=True, torch_dtype=torch.float16, quantization_config=quant, device_map='auto', max_memory={0: gpu_budget, 'cpu': cpu_budget}, low_cpu_mem_usage=True, attn_implementation='sdpa')
        if any(str(device) == 'disk' for device in getattr(base_model, 'hf_device_map', {}).values()):
            raise RuntimeError('Не хватает памяти; выгрузка PEFT на диск не включена. Выбери GGUF.')
        self.model = attach_adapter(base_model, home)
        self.model.eval()
        self.key = adapter_id
        emit('status', 'LoRA действительно загружена · ' + adapter_id)

    def reply(self, req):
        self.load(req['adapter_id'])
        import torch
        from transformers import TextIteratorStreamer
        messages = req['messages']
        # C# serializer sends lower-case role/content for all existing backends.
        messages = [{'role': m.get('role', m.get('Role')), 'content': m.get('content', m.get('Content', ''))} for m in messages]
        context = int(req.get('context_tokens', 2048))
        maximum = int(req.get('max_tokens', 512))
        if not 512 <= context <= 24576 or not 1 <= maximum <= 8192:
            raise ValueError('Invalid context/output limit')
        encoded = self.tokenizer.apply_chat_template(messages, tokenize=True, add_generation_prompt=True, return_tensors='pt')
        available = context - encoded.shape[1]
        if req.get('fit_output'):
            maximum = min(maximum, available)
        if maximum < 1 or encoded.shape[1] + maximum > context:
            raise ValueError('Точный токенизатор: история и ответ не помещаются в контекст. Уменьши историю/ответ или увеличь контекст.')
        device = self.model.get_input_embeddings().weight.device
        encoded = encoded.to(device)
        streamer = TextIteratorStreamer(self.tokenizer, skip_prompt=True, skip_special_tokens=True, timeout=1.0)
        result = {}; errors = []
        def generate():
            try:
                with torch.inference_mode():
                    result['output'] = self.model.generate(input_ids=encoded, attention_mask=torch.ones_like(encoded), max_new_tokens=maximum, do_sample=True, temperature=max(.1, min(2., float(req.get('temperature', .7)))), top_p=.95, pad_token_id=self.tokenizer.eos_token_id, streamer=streamer, use_cache=True)
            except BaseException as error:
                errors.append(error)
        thread = threading.Thread(target=generate, daemon=True)
        thread.start()
        chunks = []
        while True:
            try:
                text = next(streamer)
                chunks.append(text); emit('token', text)
            except StopIteration:
                break
            except queue.Empty:
                if errors:
                    raise errors[0]
                if not thread.is_alive():
                    break
        thread.join()
        if errors:
            raise errors[0]
        output = result['output']
        limited = output.shape[1] - encoded.shape[1] >= maximum and int(output[0, -1]) != self.tokenizer.eos_token_id
        emit('done', ''.join(chunks), limit_reached=limited)

def main():
    # Setup operations use only stdlib; no repository-provided Python is executed.
    if len(sys.argv) < 4:
        raise ValueError('Usage: worker MANIFEST_FOLDER CACHE COMMAND [ADAPTER_ID]')
    manifest, root, command = sys.argv[1:4]
    catalog, base = load_manifests(manifest)
    if command == 'check':
        check_environment(); return
    if command in {'prepare', 'all', 'adapter'}:
        safe_home(root, 'base').parent.mkdir(parents=True, exist_ok=True)
        selected = catalog if command == 'all' else [next(m for m in catalog if m['id'] == sys.argv[4])]
        if command == 'prepare':
            check_environment()
            download_record(base, safe_home(root, 'base'), True)
        for model in selected:
            download_record(model, adapter_home(root, model))
        emit('done', 'Файлы проверены. Качество ответов этих 7B-адаптеров пока не оценено.')
        return
    if command != 'serve':
        raise ValueError('Unknown command')
    runtime = Runtime(root, manifest)
    for line in sys.stdin:
        try:
            runtime.reply(json.loads(line))
        except Exception as error:
            emit('error', str(error))
            # Do not reuse a partially loaded / OOM-damaged model.
            return

if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        emit('error', str(error))
        sys.exit(1)
