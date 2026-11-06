import sys, types, contextlib, os, pathlib, json
class FakeTensor:
    shape=(1,2048 if os.environ.get('FAKE_OVERFLOW') else 440 if os.environ.get('FAKE_ALMOST_OVERFLOW') else 3)
    def to(self, device): return self
class Layer:
    def register_forward_pre_hook(self, fn): self.hook=fn
class Tokenizer:
    eos_token_id=2
    def apply_chat_template(self, messages, **kw):
        assert kw.get('enable_thinking') is (os.environ.get('FAKE_THINKING')=='1')
        if os.environ.get('FAKE_EXPECT_SYSTEM'):
            assert messages[0]['role']=='system' and messages[0]['content']==os.environ['FAKE_EXPECT_SYSTEM']
        return 'mock prompt'
    def __call__(self, *args, **kw): return {'input_ids':FakeTensor(), 'attention_mask':FakeTensor()}
    def decode(self, tokens, **kw): return 'Привет, Вась!'
class Model:
    def __init__(self):
        self.tokenizer=Tokenizer(); self.device='cuda:0'
        self.model=types.SimpleNamespace(model=types.SimpleNamespace(layers=[Layer(),Layer()]))
    def generate(self, **kw):
        for layer in self.model.model.layers: layer.hook(layer,())
        kw['streamer'].on_finalized_text('Привет, ')
        kw['streamer'].on_finalized_text('Вась!',True)
        if os.environ.get('FAKE_AT_LIMIT'):
            return [[0,1,2]+[7]*(kw['max_new_tokens']-1)+[2 if os.environ.get('FAKE_END_EOS') else 8]]
        return [[0,1,2,7,8]]
class AutoModel:
    @classmethod
    def get_module_class(cls,path):
        return ('airllm',os.environ.get('FAKE_AIR_CLASS','AirLLMBaseModel'))
    @classmethod
    def from_pretrained(cls, path, **kw):
        assert os.environ['HF_HUB_OFFLINE']=='1'
        assert os.environ['TRANSFORMERS_OFFLINE']=='1'
        assert kw['delete_original'] is False
        assert kw['prefetching'] is False
        assert kw['max_seq_len'] == 512
        if os.environ.get('FAKE_WRITE_SHARDS'):
            p=pathlib.Path(path)/'splitted_model';p.mkdir(exist_ok=True)
            for name in ['model.embed_tokens','model.norm','lm_head','model.layers.0','model.layers.1']:
                for ext in ['.safetensors','.safetensors.done']:(p/(name+ext)).write_bytes(b'mock prepared')
        print('mock library noise')
        return Model()
class TextStreamer:
    def __init__(self,*args,**kw): pass
sys.modules['torch']=types.SimpleNamespace(cuda=types.SimpleNamespace(is_available=lambda:not os.environ.get('FAKE_NO_CUDA')),inference_mode=contextlib.nullcontext)
sys.modules['airllm']=types.SimpleNamespace(AutoModel=AutoModel)
class FakeConfig: pass
class AutoConfig:
    @staticmethod
    def from_pretrained(path,**kw):
        assert kw['trust_remote_code'] is False and kw['local_files_only'] is True
        return FakeConfig()
sys.modules['transformers']=types.SimpleNamespace(TextStreamer=TextStreamer,AutoConfig=AutoConfig,AutoModelForCausalLM=types.SimpleNamespace(_model_mapping={FakeConfig:object}))
