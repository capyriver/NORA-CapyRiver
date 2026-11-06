import asyncio,json,pathlib,shutil,zipfile,re
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parents[1];O=R/'verification-3.16';checks=[]
def check(v,n):
 assert v,n
 checks.append(n)
async def main():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox']);page=await b.new_page();errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.set_content('''<title>Audit browser</title><body><h1>VISIBLE_HEADING</h1><div style="display:none">SECRET_DISPLAY <a href="https://hidden.example/">SECRET_LINK</a></div><div hidden>SECRET_HIDDEN</div><div aria-hidden="true">SECRET_ARIA</div><div style="visibility:hidden">SECRET_VISIBILITY</div><div style="opacity:0">SECRET_OPACITY</div><div style="content-visibility:hidden">SECRET_CONTENT_VISIBILITY</div><textarea>SECRET_TEXTAREA</textarea><input value="SECRET_INPUT"><div contenteditable>SECRET_EDITABLE</div><script>window.privatePhrase="SECRET_SCRIPT"</script><style>/* SECRET_STYLE */</style><div style="display:contents"><span>VISIBLE_CONTENTS</span></div><a href="https://visible.example/">VISIBLE_LINK</a><a href="http://insecure.example/">NONHTTPS_LINK</a><p>VISIBLE_TAIL</p></body>''')
  # Reproduce old detached-clone leak using the original shipped code, not a fabricated surrogate.
  old_script=(R/'tests/fixtures316/browser-read315.js').read_text()
  old=await page.evaluate(old_script)
  check('SECRET_DISPLAY' in old['text'],'old shipped reader reproduces hidden-text leak')
  new=await page.evaluate((R/'src/browser-read.js').read_text())
  for phrase in ['SECRET_DISPLAY','SECRET_LINK','SECRET_HIDDEN','SECRET_ARIA','SECRET_VISIBILITY','SECRET_OPACITY','SECRET_CONTENT_VISIBILITY','SECRET_TEXTAREA','SECRET_INPUT','SECRET_EDITABLE','SECRET_SCRIPT','SECRET_STYLE']:
   check(phrase not in new['text'],'exclude '+phrase)
  for phrase in ['VISIBLE_HEADING','VISIBLE_CONTENTS','VISIBLE_LINK','VISIBLE_TAIL']:
   check(phrase in new['text'],'retain '+phrase)
  check(new['links']==[dict(text='VISIBLE_LINK',url='https://visible.example/')],'only visible HTTPS links returned')
  check(new['title']=='Audit browser','page title retained')
  check(await page.locator('textarea').input_value()=='SECRET_TEXTAREA','reader does not mutate controls')
  old_dom=(R/'tests/fixtures316/browser-dom315.js').read_text()
  old_snapshot=await page.evaluate(old_dom.replace('__NORA_NONCE__',json.dumps('before')))
  check('SECRET_OPACITY' in old_snapshot['text'],'old shipped snapshot reproduces transparent-text leak')
  snapshot=await page.evaluate((R/'src/browser-dom.js').read_text().replace('__NORA_NONCE__',json.dumps('audit')))
  for phrase in ['SECRET_DISPLAY','SECRET_HIDDEN','SECRET_ARIA','SECRET_VISIBILITY','SECRET_OPACITY','SECRET_CONTENT_VISIBILITY','SECRET_TEXTAREA','SECRET_EDITABLE']:
   check(phrase not in snapshot['text'],'snapshot excludes '+phrase)
  check('VISIBLE_CONTENTS' in snapshot['text'],'snapshot reads visible display:contents text')
  check(all('SECRET_INPUT' not in json.dumps(e) for e in snapshot['elements']),'snapshot never returns input values')
  check(all('index' in e and 'tag' in e for e in snapshot['elements']),'snapshot still supplies actionable control indices')
  await page.set_content('<p>'+('visible '*5000)+'</p>');limited=await page.evaluate((R/'src/browser-read.js').read_text());check(len(limited['text'])==8000,'read text bounded at 8000 characters')
  check(not errors,'no JavaScript errors');await b.close()
 O.joinpath('browser-read.json').write_text(json.dumps(dict(passed=len(checks),checks=checks),ensure_ascii=False,indent=2));print('BROWSER READ PASS',len(checks))
asyncio.run(main())
