import asyncio,json,pathlib,shutil
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parents[1]
fixture=json.loads((R/'verification-3.6/ui-fixture.json').read_text())
manifest=json.loads((R/'src/peft-adapters.json').read_text())
fixture['peftCatalog']=[dict(id=m['id'],url=m['url'],likes=m['likes'],sourceLicense=m['sourceLicense'],weightBytes=m['weightBytes'],present=False,active=False) for m in manifest['models']]
fixture['peftRuntimePresent']=False
checks=[]
def check(v,n):
 assert v,n
 checks.append(n)
async def main():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox'])
  page=await b.new_page(viewport={'width':1100,'height':940});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome={webview:{postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}}')
  await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(x)=>receive({type:"state",data:x})',fixture)
  await page.evaluate('openCatalog()');await page.locator('#catalog-peft').click()
  check(await page.locator('#peft-modal').is_visible(),'library accessible from Models')
  check(await page.locator('.peft-item').count()==16,'16 real adapter entries, separate from prompts')
  check(await page.locator('#download-all-peft').is_disabled(),'no download before runtime installation')
  await page.locator('#setup-peft').click();check(await page.evaluate('__sent.at(-1).action==="setupPeft"'),'setup button invokes host')
  await page.locator('#peft-search').fill('sql');check(await page.locator('.peft-item').count()==2,'actual candidate search works')
  await page.locator('#peft-search').fill('notpresent');check('Совпадений нет' in await page.locator('#peft-list').inner_text(),'empty search state')
  await page.locator('#peft-search').fill('')
  fixture['peftRuntimePresent']=True;fixture['settings'].update(mode=3,peftAdapterId=manifest['models'][0]['id']);fixture['peftCatalog'][0].update(present=True,active=True)
  await page.evaluate('(x)=>receive({type:"state",data:x})',fixture)
  check(await page.locator('.peft-item.is-active').count()==1,'only one active adapter')
  await page.locator('.peft-item').nth(1).locator('.primary').click();check(await page.evaluate('__sent.at(-1).action==="choosePeft"'),'choose routes to native PEFT, not prompts')
  await page.locator('#download-all-peft').click();check(await page.evaluate('__sent.at(-1).action==="downloadAllPeft"'),'all weights download button invokes host')
  check('LoRA' in await page.locator('#model-label').inner_text(),'selected engine labelled LoRA')
  check(await page.locator('#response-mode option').nth(1).is_disabled(),'unsupported thinking disabled')
  await page.evaluate('setBusy(true)');check(await page.locator('#setup-peft').is_disabled() and await page.locator('.peft-item .primary').first.is_disabled(),'no concurrent setup/model changes');await page.evaluate('setBusy(false)')
  for width in [1100,620,390]:
   await page.set_viewport_size({'width':width,'height':940});await page.evaluate('openPeft()')
   check(await page.evaluate('document.documentElement.scrollWidth<=innerWidth'),'document width '+str(width))
   check(await page.locator('.peft-modal').evaluate('e=>e.scrollWidth<=e.clientWidth+1'),'modal no horizontal overflow '+str(width))
   check(await page.locator('.peft-modal').evaluate('e=>e.scrollHeight>e.clientHeight'),'scroll enabled '+str(width))
   await page.locator('.peft-item').last.scroll_into_view_if_needed();check(await page.locator('.peft-item').last.locator('button').first.is_visible(),'last adapter accessible '+str(width))
  check(not errors,'no JavaScript errors: '+str(errors))
  # Snapshot with state baked in for mandatory capture-helper visual QA.
  for width in [1100,390]:
   await page.set_viewport_size({'width':width,'height':940});await page.evaluate('openPeft();document.querySelector(".peft-modal").scrollTop=0')
   content=await page.content();content=content.replace('</body>',f'<script>receive({json.dumps({"type":"state","data":fixture},ensure_ascii=False)});openPeft();</script></body>')
   (R/f'verification-3.10/peft-{width}.html').write_text(content)
  await b.close()
 (R/'verification-3.10/ui-test.json').write_text(json.dumps({'passed':len(checks),'checks':checks},ensure_ascii=False,indent=2));print('UI PASS',len(checks))
asyncio.run(main())
