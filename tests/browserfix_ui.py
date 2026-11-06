import asyncio,json,pathlib,shutil
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parent.parent
fixture=json.loads((R/'verification-3.6/ui-fixture.json').read_text())
for id,name in [('memory-bank','Memory Bank · память проектов'),('aim-memory','AIM · постоянный граф памяти')]:fixture['mcp']['presets'].append(dict(id=id,name=name,description='Постоянная локальная память',source='https://github.com/'+('alioshr/memory-bank-mcp' if id=='memory-bank' else 'shaneholloman/mcp-knowledge-graph'),enabled=False,connected=False,tools=0,toolNames=[],requirement='Node.js 24+'))
fixture['current']['messages'][-1]['limitReached']=True
checks=[]
def check(value,name):
 assert value,name
 checks.append(name)
async def run():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox'])
  page=await b.new_page(viewport={'width':1440,'height':940});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome={webview:{postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}}')
  await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(d)=>receive({type:"state",data:d})',fixture)
  check(await page.locator('.msg .warn').count()>0,'length-limit warning displayed separately from answer/code')
  await page.locator('#mcp-btn').click();modal=page.locator('#mcp-modal .modal');box=await modal.bounding_box()
  check(await modal.evaluate('e=>e.scrollHeight>e.clientHeight'),'MCP has a scrollable overflow area')
  check(await page.locator('#mcp-list .card').count()==15,'15 connector cards from fixture render')
  await page.mouse.move(box['x']+8,box['y']+150);await page.mouse.wheel(0,1000);await page.wait_for_timeout(400)
  first=await modal.evaluate('e=>e.scrollTop');check(first>100,'real mouse wheel scrolls MCP downward')
  await page.mouse.wheel(0,-700);await page.wait_for_timeout(400);check(await modal.evaluate('e=>e.scrollTop')<first,'real mouse wheel scrolls MCP upward')
  await modal.focus();before=await modal.evaluate('e=>e.scrollTop');await page.keyboard.press('PageDown');await page.wait_for_timeout(350);check(await modal.evaluate('e=>e.scrollTop')>before,'keyboard PageDown scrolls MCP')
  await page.keyboard.press('End');await page.wait_for_timeout(350);check(await modal.evaluate('e=>e.scrollHeight-e.clientHeight-e.scrollTop<5'),'keyboard End reaches final controls')
  check(await page.locator('#mcp-run').is_visible(),'manual run control visible at bottom');await page.screenshot(path=str(R/'verification-3.7.1/mcp-bottom.png'))
  await page.keyboard.press('Home');await page.wait_for_timeout(350);check(await modal.evaluate('e=>e.scrollTop')==0,'keyboard Home returns to top')
  await page.evaluate('closeModal()');await page.locator('#settings-btn').click();check(await page.locator('#s-context option[value="24576"]').count()==1,'24K choice exists')
  check(await page.locator('#s-tokens').get_attribute('max')=='8192','8K output limit for GGUF')
  await page.locator('#s-context').select_option('24576');await page.locator('#s-tokens').fill('4096');await page.locator('#settings-form button[type=submit]').click()
  check(await page.evaluate('__sent.at(-1).data.contextTokens===24576&&__sent.at(-1).data.maxTokens===4096'),'24K context and separate 4K output dispatched')
  await page.locator('#s-mode').select_option('0');check(await page.locator('#s-context option[value="24576"]').is_disabled(),'24K disabled for legacy AirLLM')
  check(await page.locator('#s-tokens').get_attribute('max')=='1024','AirLLM output restriction explained/enforced')
  await page.evaluate('closeModal()');await page.locator('#mcp-btn').click();await page.screenshot(path=str(R/'verification-3.7.1/mcp-desktop.png'))
  check(await page.evaluate('document.documentElement.scrollWidth===innerWidth'),'desktop no horizontal overflow')
  # Trusted Chromium touch input, not JavaScript scrollTop assignment.
  await page.set_viewport_size({'width':390,'height':844});cdp=await page.context.new_cdp_session(page);await cdp.send('Emulation.setTouchEmulationEnabled',{'enabled':True,'maxTouchPoints':1})
  box=await modal.bounding_box();x=box['x']+12;y=box['y']+box['height']-110;before=await modal.evaluate('e=>e.scrollTop')
  await cdp.send('Input.dispatchTouchEvent',{'type':'touchStart','touchPoints':[{'x':x,'y':y}]})
  for step in range(1,9):
   await cdp.send('Input.dispatchTouchEvent',{'type':'touchMove','touchPoints':[{'x':x,'y':y-step*45}]});await page.wait_for_timeout(30)
  await cdp.send('Input.dispatchTouchEvent',{'type':'touchEnd','touchPoints':[]});await page.wait_for_timeout(350)
  check(await modal.evaluate('e=>e.scrollTop')>before,'trusted touch swipe scrolls mobile MCP')
  check(await page.evaluate('document.documentElement.scrollWidth===innerWidth'),'mobile no horizontal overflow');await page.screenshot(path=str(R/'verification-3.7.1/mcp-mobile.png'))
  check(not errors,'no JavaScript exceptions');await b.close()
 (R/'verification-3.7.1/ui-results.json').write_text(json.dumps({'passed':len(checks),'checks':checks},ensure_ascii=False,indent=2));print(str(len(checks))+' UI checks passed')
asyncio.run(run())
