"""Exact AirLLM prepare_inputs code tested with a counting tokenizer, not real weights."""
import ast,pathlib,unittest,math
source=pathlib.Path(__file__).resolve().parents[1]/'src/air_backend.py'
tree=ast.parse(source.read_text());defs=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ['prepare_inputs','prepare_with_budget']]
class Tensor:
 def __init__(self,n):self.shape=(1,n)
 def to(self,device):return self
class Tokenizer:
 def __init__(self):self.seen=[]
 def apply_chat_template(self,messages,**kw):
  self.seen.append([dict(m) for m in messages]);return sum(len(m['content']) for m in messages)
 def __call__(self,prompt,**kw):return {'input_ids':Tensor(prompt),'attention_mask':Tensor(prompt)}
ns={'emit':lambda *a,**k:None};exec(compile(ast.Module(body=defs,type_ignores=[]),str(source),'exec'),ns)
class ContextTests(unittest.TestCase):
 def test_latest_turn_preserved_at_exact_token_budget(self):
  t=Tokenizer();m=[{'role':'system','content':'rules'},{'role':'user','content':'old'*100},{'role':'assistant','content':'old-answer'*100},{'role':'user','content':'LAST QUESTION'}];ns['prepare_inputs'](t,m,64,'cpu',512);self.assertEqual(t.seen[-1][-1]['content'],'LAST QUESTION');self.assertEqual(len(t.seen[-1]),2);self.assertEqual(len(m),4)
 def test_tool_result_last_request_anchor_preserved(self):
  t=Tokenizer();m=[{'role':'system','content':'rules'},{'role':'user','content':'ORIGINAL REQUEST'},{'role':'assistant','content':'old-action'*100},{'role':'user','content':'TOOL RESULT; CURRENT USER REQUEST: LAST QUESTION'}];ns['prepare_inputs'](t,m,64,'cpu',512);self.assertTrue(t.seen[-1][-1]['content'].endswith('LAST QUESTION'))
 def test_oversized_current_request_errors_not_truncates(self):
  t=Tokenizer();m=[{'role':'system','content':'rules'},{'role':'user','content':'LAST'*1000}]
  with self.assertRaises(ValueError):ns['prepare_inputs'](t,m,64,'cpu',512)
  self.assertEqual(t.seen[-1][-1]['content'],m[-1]['content'])
if __name__=='__main__':unittest.main(verbosity=2)
