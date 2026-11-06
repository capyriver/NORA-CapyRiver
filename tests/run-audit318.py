import pathlib,subprocess,sys
import argparse,shutil
p=argparse.ArgumentParser();p.add_argument('--dotnet',default=shutil.which('dotnet') or 'dotnet');a=p.parse_args()
r=pathlib.Path(__file__).resolve().parents[1];out=r/'verification-3.18-rerun';out.mkdir(exist_ok=True)
suites='Cloudflare318 Retry317 Audit316 Update314 Api Connector MemoryMcp BrowserNetwork Workspace Prompt Core AgentLoop315 Catalog AirCatalog Direct PeftHost Experimental Skill Video Update313 Performance'.split()
failed=[]
for n in suites:
 with (out/(n+'.log')).open('w') as log:
  p=subprocess.run([a.dotnet,'run','--project',str(r/'tests'/(n+'Tests.csproj')),'--',str(r)],cwd=r,stdout=log,stderr=subprocess.STDOUT)
 print(n,'PASS' if p.returncode==0 else 'FAIL',flush=True)
 if p.returncode:failed.append(n)
print('FAILED',failed);sys.exit(bool(failed))
