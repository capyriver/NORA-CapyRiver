import re, unittest, subprocess, tempfile, pathlib, json, os, sys, hashlib
HERE=pathlib.Path(__file__).parent
BACKEND=HERE.parent/'src'/'air_backend.py'
class BackendTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.p=pathlib.Path(self.tmp.name)
        self.cfg={'model_type':'qwen3','num_hidden_layers':2,'tie_word_embeddings':False}
        self.write_config();(self.p/'splitted_model').mkdir()
        for name in ['model.embed_tokens','model.norm','lm_head','model.layers.0','model.layers.1']:
            for ext in ['.safetensors','.safetensors.done']:(self.p/'splitted_model'/(name+ext)).write_bytes(b'mock')
    def tearDown(self):self.tmp.cleanup()
    def write_config(self):(self.p/'config.json').write_text(json.dumps(self.cfg))
    def run_backend(self,n=1,**extra):
        env={**os.environ,**extra,'PYTHONPATH':str(HERE)}
        messages=[{'role':'user','content':'Привет'}]
        if extra.get('FAKE_EXPECT_SYSTEM'):messages.insert(0,{'role':'system','content':extra['FAKE_EXPECT_SYSTEM']})
        req=json.dumps({'model_path':str(self.p),'messages':messages,'max_tokens':int(extra.get('FAKE_MAX_TOKENS','64')),'fit_output':extra.get('FAKE_FIT_OUTPUT')=='1','allow_prepare':extra.get('FAKE_ALLOW_PREPARE')=='1','thinking':extra.get('FAKE_THINKING')=='1'},ensure_ascii=False)+'\n'
        code='import fake_modules,runpy;runpy.run_path('+repr(str(BACKEND))+',run_name="__main__")'
        r=subprocess.run([sys.executable,'-c',code],input=req*n,text=True,encoding='utf-8',capture_output=True,env=env,timeout=10)
        return r,[json.loads(s) for s in r.stdout.splitlines()]
    def test_two_turns_stream_offline_reuse_no_model_writes(self):
        def digest():return {str(f):hashlib.sha256(f.read_bytes()).hexdigest() for f in self.p.rglob('*') if f.is_file()}
        before=digest();r,events=self.run_backend(2)
        self.assertEqual(r.returncode,0,r.stderr)
        self.assertEqual(len([e for e in events if e['type']=='done']),2)
        self.assertEqual(len([e for e in events if e['type']=='token']),4)
        self.assertEqual(len([e for e in events if e['type']=='status' and 'Загрузка локальной' in e['text']]),1)
        self.assertEqual([e['text'] for e in events if e['type']=='done'],['Привет, Вась!']*2)
        self.assertIn('mock library noise',r.stderr);self.assertNotIn('mock library noise',r.stdout)
        self.assertEqual(before,digest())
    def test_qwen_identity_reaches_template_on_each_turn(self):
        source=(HERE.parent/'src'/'QwenPrompt.cs').read_text()
        prompt=json.loads(re.search(r'public const string Prompt=("[^\n]+");',source).group(1))
        r,events=self.run_backend(2,FAKE_EXPECT_SYSTEM=prompt)
        self.assertEqual(r.returncode,0,r.stderr)
        self.assertEqual(len([e for e in events if e['type']=='done']),2)
    def test_thinking_switch_reaches_tokenizer(self):
        r,events=self.run_backend(FAKE_THINKING='1')
        self.assertEqual(r.returncode,0,r.stderr)
        self.assertEqual(events[-1]['type'],'done')
    def test_remote_code_refused(self):
        self.cfg['auto_map']={'AutoModel':'modeling.evil'};self.write_config()
        r,events=self.run_backend();self.assertEqual(r.returncode,1);self.assertEqual(events[-1]['type'],'error');self.assertIn('стороннего',events[-1]['text'])
    def test_incomplete_shards_refused(self):
        (self.p/'splitted_model'/'model.layers.1.safetensors.done').unlink()
        r,events=self.run_backend();self.assertEqual(r.returncode,1);self.assertIn('не полностью',events[-1]['text'])
    def test_no_cuda(self):
        r,events=self.run_backend(FAKE_NO_CUDA='1');self.assertEqual(r.returncode,1);self.assertIn('CUDA недоступна',events[-1]['text'])
    def test_long_context_refused(self):
        r,events=self.run_backend(FAKE_OVERFLOW='1');self.assertEqual(r.returncode,1);self.assertIn('Превышен контекст',events[-1]['text'])
    def test_qwen2_coder_supported(self):
        self.cfg['model_type']='qwen2';self.write_config()
        r,events=self.run_backend();self.assertEqual(r.returncode,0,r.stderr);self.assertEqual(events[-1]['type'],'done')
    def test_vision_architecture_not_mislabeled_as_text(self):
        self.cfg['model_type']='qwen2_vl';self.write_config()
        r,events=self.run_backend();self.assertEqual(r.returncode,1);self.assertIn('vision',events[-1]['text'])
    def test_managed_prepare_requested(self):
        r,events=self.run_backend(FAKE_ALLOW_PREPARE='1')
        self.assertEqual(r.returncode,0,r.stderr);self.assertTrue(any('послойного' in e.get('text','') for e in events))
    def test_remote_code_still_refused_with_prepare(self):
        self.cfg['auto_map']={'AutoModel':'evil'};self.write_config()
        r,events=self.run_backend(FAKE_ALLOW_PREPARE='1');self.assertEqual(r.returncode,1);self.assertIn('стороннего',events[-1]['text'])
    def test_prepare_creates_missing_layers_for_managed_only(self):
        (self.p/'splitted_model'/'model.layers.1.safetensors.done').unlink()
        r,events=self.run_backend(FAKE_ALLOW_PREPARE='1',FAKE_WRITE_SHARDS='1')
        self.assertEqual(r.returncode,0,r.stderr);self.assertEqual(events[-1]['type'],'done')
        self.assertTrue((self.p/'splitted_model'/'model.layers.1.safetensors.done').exists())
    def check_backend(self,**extra):
        code='import sys,fake_modules,runpy;sys.argv=["air_backend.py","--check",'+repr(str(self.p))+'];runpy.run_path('+repr(str(BACKEND))+',run_name="__main__")'
        return subprocess.run([sys.executable,'-c',code],text=True,capture_output=True,env={**os.environ,'PYTHONPATH':str(HERE),**extra},timeout=10)
    def test_preflight_checks_cuda_and_architecture(self):
        r=self.check_backend();self.assertEqual(r.returncode,0,r.stdout+r.stderr);self.assertIn('CUDA',r.stdout)
    def test_preflight_rejects_old_llama_fallback(self):
        r=self.check_backend(FAKE_AIR_CLASS='AirLLMLlama2');self.assertEqual(r.returncode,1);self.assertIn('совместимый',r.stdout)
    def test_preflight_no_cuda_prevents_download(self):
        r=self.check_backend(FAKE_NO_CUDA='1');self.assertEqual(r.returncode,1);self.assertIn('CUDA недоступна',r.stdout)
    def test_three_sequential_stages_reuse_one_process_model(self):
        r,events=self.run_backend(3,FAKE_FIT_OUTPUT='1')
        self.assertEqual(r.returncode,0,r.stderr)
        self.assertEqual(len([e for e in events if e['type']=='done']),3)
        self.assertEqual(len([e for e in events if e['type']=='status' and 'Загрузка локальной' in e['text']]),1)
    def test_stage_output_shrinks_but_input_is_not_silently_truncated(self):
        r,events=self.run_backend(FAKE_FIT_OUTPUT='1',FAKE_ALMOST_OVERFLOW='1',FAKE_MAX_TOKENS='128')
        self.assertEqual(r.returncode,0,r.stderr)
        self.assertTrue(any('лимит ответа снижен' in e.get('text','') for e in events))
    def test_even_team_rejects_oversized_current_input(self):
        r,events=self.run_backend(FAKE_FIT_OUTPUT='1',FAKE_OVERFLOW='1')
        self.assertEqual(r.returncode,1);self.assertIn('Превышен контекст',events[-1]['text'])
    def test_limit_flag_reaches_host(self):
        r,events=self.run_backend(FAKE_AT_LIMIT='1',FAKE_MAX_TOKENS='16')
        self.assertEqual(r.returncode,0,r.stderr)
        self.assertIs(events[-1].get('limit_reached'),True)
    def test_eos_at_limit_is_not_truncation(self):
        r,events=self.run_backend(FAKE_AT_LIMIT='1',FAKE_END_EOS='1',FAKE_MAX_TOKENS='16')
        self.assertEqual(r.returncode,0,r.stderr)
        self.assertIs(events[-1].get('limit_reached'),False)
    def test_missing_config(self):
        (self.p/'config.json').unlink();r,events=self.run_backend();self.assertEqual(r.returncode,1);self.assertIn('config.json',events[-1]['text'])
if __name__=='__main__':unittest.main(verbosity=2)
