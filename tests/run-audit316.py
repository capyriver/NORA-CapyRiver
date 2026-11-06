"""Re-run selected release suites; developer dependencies required. No actual model/account tests."""
import argparse,os,pathlib,shutil,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('--dotnet',default=shutil.which('dotnet') or 'dotnet');p.add_argument('--skip-browser',action='store_true');a=p.parse_args()
r=pathlib.Path(__file__).resolve().parents[1];out=r/'verification-3.16-rerun';out.mkdir(exist_ok=True)
suites='Audit316 Update314 Api Connector MemoryMcp BrowserNetwork Workspace Prompt Core AgentLoop315 Catalog AirCatalog Direct PeftHost Experimental Skill Video Update313 Performance'.split()
jobs=[(n,[a.dotnet,'run','--project',str(r/'tests'/(n+'Tests.csproj')),'--',str(r)]) for n in suites]
jobs += [(n,[sys.executable,str(r/'tests'/f)]) for n,f in [('backend','test_backend.py'),('context-backend','context_backend314_test.py')]]
if not a.skip_browser:jobs += [(n,[sys.executable,str(r/'tests'/f)]) for n,f in [('BrowserRead','browser_read316.py'),('UI','agent316_ui.py')]]
for name,cmd in jobs:
 with (out/(name+'.log')).open('w') as log: result=subprocess.run(cmd,cwd=r,stdout=log,stderr=subprocess.STDOUT)
 print(name, 'PASS' if result.returncode==0 else 'FAIL',flush=True)
 if result.returncode:sys.exit(result.returncode)
print('All selected suites passed. Logs:',out)
