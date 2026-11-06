"""Optional metadata-only live check; does not download weights."""
import json,pathlib,requests,urllib.parse,concurrent.futures
R=pathlib.Path(__file__).resolve().parent.parent
specs=json.loads((R/'verification-2.6/catalog-specs.json').read_text())
def check(s):
 u=urllib.parse.urlparse(s['url']);repo,rest=u.path.lstrip('/').split('/resolve/',1);revision,name=rest.split('/',1)
 r=requests.get(f'https://huggingface.co/api/models/{repo}/revision/{revision}?blobs=true',timeout=45);r.raise_for_status();j=r.json();f=next(x for x in j['siblings'] if x['rfilename']==name)
 assert j['sha']==revision,(s['id'],'commit')
 assert f['size']==s['bytes'] and f['lfs']['sha256']==s['sha256'],(s['id'],'hash/size')
 return {'id':s['id'],'repo':repo,'commit':revision,'file':name,'bytes':f['size'],'sha256':f['lfs']['sha256'],'license':j.get('cardData',{}).get('license'),'scope':'API metadata only, not complete download or generation'}
with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:rows=list(pool.map(check,specs))
(R/'verification-2.6/source-metadata.json').write_text(json.dumps({'passed':len(rows),'models':rows},ensure_ascii=False,indent=2));print(len(rows),'pinned source metadata checks passed')
