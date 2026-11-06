"""LocalChat AirLLM bridge. Offline only; one retained model per process.
Protocol: stdin JSON lines; stdout JSON events. Library chatter goes to stderr.
"""
import os
os.environ.update(HF_HUB_OFFLINE='1', TRANSFORMERS_OFFLINE='1', HF_HUB_DISABLE_TELEMETRY='1', DO_NOT_TRACK='1')
import sys, json, time, traceback, contextlib
from pathlib import Path
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace', line_buffering=True)
    sys.stderr.reconfigure(encoding='utf-8', errors='replace', line_buffering=True)
    sys.stdin.reconfigure(encoding='utf-8', errors='strict')
protocol_out = sys.stdout

def emit(kind, **data):
    protocol_out.write(json.dumps(dict(type=kind, **data), ensure_ascii=False) + '\n')
    protocol_out.flush()

def validate_model(path, allow_unprepared=False):
    p = Path(path).resolve()
    if not (p / 'config.json').is_file():
        raise ValueError('Не найден config.json. Укажи папку модели Qwen3-8B, не папку splitted_model.')
    cfg = json.loads((p / 'config.json').read_text(encoding='utf-8'))
    if cfg.get('model_type') not in ('qwen2', 'qwen3') or cfg.get('auto_map'):
        raise ValueError('Этот режим рассчитан на стандартную текстовую Qwen2/Qwen3 без стороннего Python-кода. Фото — через vision-модель в LM Studio.')
    if allow_unprepared:
        return str(p)
    shards = p / 'splitted_model'
    if not shards.is_dir() or not any(shards.iterdir()):
        raise ValueError('Не найдены готовые слои splitted_model. Укажи ту же папку Qwen3-8B, которую использовал diagnose_airllm.py. Чат не скачивает и не пересобирает модель.')
    count = int(cfg.get('num_hidden_layers', 0))
    if count <= 0:
        raise ValueError('В config.json отсутствует число слоёв Qwen3.')
    required = ['model.embed_tokens', 'model.norm'] + [f'model.layers.{i}' for i in range(count)]
    if not cfg.get('tie_word_embeddings', False):
        required.append('lm_head')
    missing = [name for name in required if not (shards / (name + '.safetensors')).is_file()
               or not (shards / (name + '.safetensors.done')).is_file()]
    if missing:
        raise ValueError('Слои модели подготовлены не полностью: ' + ', '.join(missing[:3]) + '. Сначала проверь diagnose_airllm.py. Автоматическая пересборка отключена.')
    return str(p)

def prepare_inputs(tokenizer, messages, maximum, device, context=512, thinking=False):
    messages = list(messages)
    while True:
        prompt = tokenizer.apply_chat_template(messages, tokenize=False, add_generation_prompt=True, enable_thinking=thinking)
        inputs = tokenizer(prompt, return_tensors='pt')
        if inputs['input_ids'].shape[-1] + maximum <= context:
            break
        if len(messages) > 3:
            del messages[1:3]
            emit('status', text='Старая история исключена из контекста, но сохранена в чате.')
        else:
            raise ValueError(f'Превышен контекст {context} токенов. Сократи инструкции или вопрос, убери вложения; увеличение контекста требует больше VRAM.')
    return {k: v.to(device) for k, v in inputs.items()}

def prepare_with_budget(tokenizer, messages, maximum, device, context, thinking, fit_output=False):
    while True:
        try:
            return prepare_inputs(tokenizer, messages, maximum, device, context, thinking), maximum
        except ValueError as e:
            if not fit_output or 'Превышен контекст' not in str(e) or maximum <= 64:
                raise
            maximum = max(64, maximum // 2)
            emit('status', text=f'Этап команды: лимит ответа снижен до {maximum} токенов, чтобы сохранить входной код в контексте.')

def main():
    model = None
    loaded_path = None
    for line in sys.stdin:
        try:
            req = json.loads(line)
            allow_prepare = req.get('allow_prepare') is True
            path = validate_model(req['model_path'], allow_unprepared=allow_prepare)
            maximum = max(1, min(1024, int(req.get('max_tokens', 16))))
            context = int(req.get('context_tokens', 512))
            if context not in (512, 1024, 2048):
                raise ValueError('Допустимый контекст: 512, 1024 или 2048 токенов.')
            with contextlib.redirect_stdout(sys.stderr):
                import torch
                from airllm import AutoModel
                from transformers import TextStreamer
                if not torch.cuda.is_available():
                    raise RuntimeError('CUDA недоступна в выбранном Python. Нужна та же .venv, где работал diagnose_airllm.py.')
                if model is None or loaded_path != (path, context):
                    emit('status', text='Загрузка локальной Qwen. Скачивание отключено…')
                    if allow_prepare:
                        emit('status', text='AirLLM: подготовка/проверка послойного кэша на диске. Первый запуск может занять много времени; исходные веса сохраняются.')
                    model = AutoModel.from_pretrained(path, device='cuda:0', max_seq_len=context, prefetching=False, delete_original=False)
                    validate_model(path)
                    loaded_path = (path, context)
                    layers = model.model.model.layers
                    total = len(layers)
                    progress = {'pass': 0}
                    def before_layer(mod, args):
                        idx = getattr(mod, '_localchat_layer_index')
                        if idx == 0:
                            progress['pass'] += 1
                        emit('status', text=f'Проход {progress["pass"]} · слой {idx + 1}/{total}. AirLLM может отвечать медленно.')
                    for idx, layer in enumerate(layers):
                        layer._localchat_layer_index = idx
                        layer.register_forward_pre_hook(before_layer)
                progress['pass'] = 0
                inputs, maximum = prepare_with_budget(model.tokenizer, req['messages'], maximum, model.device, context, bool(req.get('thinking', False)), req.get('fit_output') is True)
                class Stream(TextStreamer):
                    def on_finalized_text(self, text, stream_end=False):
                        if text:
                            emit('token', text=text)
                streamer = Stream(model.tokenizer, skip_prompt=True, skip_special_tokens=True)
                started = time.monotonic()
                emit('status', text='Генерация началась. Кнопка «Стоп» остановит процесс и освободит модель.')
                with torch.inference_mode():
                    output = model.generate(**inputs, max_new_tokens=maximum, do_sample=True, temperature=max(0.1,min(2.0,float(req.get('temperature',0.7)))),
                        top_p=0.9, use_cache=True, streamer=streamer, pad_token_id=model.tokenizer.eos_token_id)
                tokens = output[0][inputs['input_ids'].shape[-1]:]
                text = model.tokenizer.decode(tokens, skip_special_tokens=True)
                eos = model.tokenizer.eos_token_id
                eos_ids = eos if isinstance(eos, (list, tuple)) else [eos]
                limited = len(tokens) >= maximum and (not len(tokens) or int(tokens[-1]) not in eos_ids)
                if limited:
                    emit('status', text='Достигнут лимит вывода. Ответ может быть неполным — проверь код и при необходимости запроси продолжение.')
                emit('done', text=text, seconds=round(time.monotonic()-started, 1), tokens=len(tokens), limit_reached=limited)
        except Exception as e:
            traceback.print_exc(file=sys.stderr)
            emit('error', text=f'{type(e).__name__}: {e}')
            # A failed inference may leave streamed layers half-loaded; restart safely.
            return 1
    return 0

def check_environment(path):
    path = validate_model(path, allow_unprepared=True)
    with contextlib.redirect_stdout(sys.stderr):
        import torch
        if not torch.cuda.is_available():
            raise RuntimeError('CUDA недоступна в выбранном Python AirLLM.')
        from transformers import AutoConfig, AutoModelForCausalLM
        from airllm import AutoModel
        config = AutoConfig.from_pretrained(path, trust_remote_code=False, local_files_only=True)
        if type(config) not in AutoModelForCausalLM._model_mapping:
            raise RuntimeError('Версия transformers не поддерживает эту архитектуру.')
        module, cls = AutoModel.get_module_class(path)
        # Old AirLLM can silently fall back to a Llama implementation. Reject that.
        if cls not in ('AirLLMBaseModel', 'AirLLMQWen2', 'AirLLMQwen2', 'AirLLMQwen3', 'AirLLMQWen3'):
            raise RuntimeError('AirLLM не объявил совместимый текстовый backend Qwen2/Qwen3: ' + cls)
    emit('status', text='Python/CUDA/архитектура доступны. Это не тест полной генерации.')

if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--check':
        try:
            check_environment(sys.argv[2])
        except Exception as e:
            emit('error', text=f'{type(e).__name__}: {e}')
            raise SystemExit(1)
        raise SystemExit(0)
    raise SystemExit(main())
