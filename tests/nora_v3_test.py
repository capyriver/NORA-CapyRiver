"""NORA AI v3 adapter provenance and new desktop wiring regression tests."""
import importlib.util
import json
from pathlib import Path
import tempfile
import sys
from types import SimpleNamespace
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]

class NoraV3Tests(unittest.TestCase):
    def test_backend_verifies_original_adapter(self):
        spec = importlib.util.spec_from_file_location('nora_v3_backend', ROOT/'src'/'nora_v3_backend.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder)
            (p/'adapter_config.json').write_bytes((ROOT/'nora-v3'/'adapter_config.json').read_bytes())
            (p/'adapter_model.safetensors').write_bytes((ROOT/'nora-v3'/'adapter_model.safetensors').read_bytes())
            module.verify_adapter(p)
            (p/'adapter_model.safetensors').write_bytes(b'incorrect')
            with self.assertRaises(ValueError): module.verify_adapter(p)

    def test_backend_pins_exact_base_and_limits_generation(self):
        spec = importlib.util.spec_from_file_location('nora_v3_backend', ROOT/'src'/'nora_v3_backend.py')
        module = importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        self.assertEqual(module.BASE_ID, 'Qwen/Qwen3.5-4B')
        self.assertEqual(module.REVISION, '851bf6e806efd8d0a36b00ddf55e13ccb7b8cd0a')
        with self.assertRaises(ValueError): module.validate_request({'messages':[], 'context_tokens':32768, 'max_tokens':512})
        self.assertEqual(module.validate_request({'messages':[{'role':'user','content':'Hello'}], 'context_tokens':4096,'max_tokens':512})[1:], (4096,512))

    def test_model_download_not_ready_until_complete_and_all_shards_present(self):
        spec = importlib.util.spec_from_file_location('nora_v3_backend', ROOT/'src'/'nora_v3_backend.py')
        module = importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            base = Path(folder)
            def partial_download(**kwargs):
                (base/'config.json').write_text('{}')
                (base/'model-00001-of-00002.safetensors').write_bytes(b'mock')
                (base/'model.safetensors.index.json').write_text(json.dumps({'weight_map':{'a':'model-00001-of-00002.safetensors','b':'model-00002-of-00002.safetensors'}}))
            with patch.dict(sys.modules, {'huggingface_hub': SimpleNamespace(snapshot_download=partial_download)}):
                with self.assertRaises(ValueError):module.prepare(base)
            self.assertFalse((base/'.nora-v3-base-complete').exists())
            def complete_download(**kwargs):
                (base/'model-00002-of-00002.safetensors').write_bytes(b'mock')
            with patch.dict(sys.modules, {'huggingface_hub': SimpleNamespace(snapshot_download=complete_download)}):
                module.prepare(base)
            self.assertEqual((base/'.nora-v3-base-complete').read_text().strip(), module.REVISION)

    def test_desktop_and_ui_refer_to_new_mode(self):
        program=(ROOT/'src'/'Program.cs').read_text(encoding='utf-8')
        ui=(ROOT/'src'/'ui.html').read_text(encoding='utf-8')
        agent=(ROOT/'src'/'AgentLoop.cs').read_text(encoding='utf-8')
        project=(ROOT/'src'/'LocalChat.csproj').read_text(encoding='utf-8')
        self.assertIn('case "chooseNoraV3":await ChooseNoraV3()',program)
        self.assertIn('settings.Mode is 0 or 3 or 4',program)
        self.assertIn('settings.Mode is 0 or 3 or 4',agent)
        self.assertIn('id="nora-v3-btn"',ui)
        self.assertIn("host('chooseNoraV3')",ui)
        self.assertIn('nora_v3_backend.py',project)
        self.assertIn('adapter_model.safetensors',project)
        self.assertIn('.nora-v3-base-complete',(ROOT/'src'/'NoraV3.cs').read_text(encoding='utf-8'))

if __name__=='__main__':unittest.main()
