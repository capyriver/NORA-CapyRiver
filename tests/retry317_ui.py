import asyncio,json,pathlib,shutil,copy
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parents[1];O=R/'verification-3.17';O.mkdir(exist_ok=True)
f=json.loads((R/'verification-3.6/ui-fixture.json').read_text());f['settings'].update(toolMode=True,unifiedAgent=True,interfaceTheme='dark',mode=2,responseMode=2,accessMode=2)
f['current'].update(title='Ошибка MCP',draft='',attachments=[])
f['current']['messages']=[dict(id='u317',role='user',state='complete',text='Опубликуй сайт'),dict(id='e317',role='assistant',state='complete',text='Задача приостановлена: Authentication error',taskId='t317',taskStatus='paused',taskPending=False,elapsedSeconds=9,work=[dict(text='MCP TOOL FAILED Authorization: Bearer secret317')],toolResults=['mcp_call · cloudflare / execute: MCP TOOL FAILED\nAuthentication error Authorization: Bearer secret317\n<img src=x onerror=window.injected=1>'],artifacts=[])]
checks=[]
def check(v,n):
 assert v,n
 checks.append(n)
async def main():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox']);page=await b.new_page(viewport=dict(width=1440,height=960));errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome={webview:{postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}}');await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(f)=>receive({type:"state",data:f})',f)
  check(await page.locator('.tool-diagnostics').count()==1,'tool results rendered for stored message')
  check(await page.locator('.tool-diagnostics').evaluate('(d)=>d.open'),'provider error expanded by default')
  check('Authentication error' in await page.locator('.tool-diagnostics').inner_text(),'actual cause in chat')
  check('secret317' not in await page.locator('.tool-diagnostics').inner_text(),'historical tool result bearer redacted')
  check('secret317' not in await page.locator('.work').inner_text(),'historical journal bearer redacted')
  check(await page.locator('.tool-diagnostics img').count()==0,'provider output displayed as text, not HTML')
  check(not await page.evaluate('window.injected||false'),'no HTML execution')
  check(await page.locator('.resume-task').count()==0,'unrepaired failure has no misleading resume button')
  check('Приостановлено' in await page.locator('.work summary').inner_text(),'failure not labeled completed')
  await page.screenshot(path=str(O/'mcp-error-chat.png'),full_page=True)
  await page.evaluate('setBusy(true);setBusy(false)');check(await page.locator('#send-btn').get_attribute('aria-label')=='Отправить сообщение','send available after stopping')
  safe=copy.deepcopy(f);safe['current']['messages'][-1].update(toolResults=['mcp_call: MCP TOOL RESULT: deployed https://test.workers.dev'],taskStatus='complete',text='Публикация подтверждена сервисом')
  await page.evaluate('(f)=>receive({type:"state",data:f})',safe);check(not await page.locator('.tool-diagnostics').evaluate('(d)=>d.open'),'successful result collapsed by default')
  await page.locator('.tool-diagnostics summary').click();check('deployed' in await page.locator('.tool-diagnostics pre').inner_text(),'successful provider output can be inspected')
  for width in [1440,1100]:
   await page.set_viewport_size(dict(width=width,height=960));check(await page.evaluate('document.documentElement.scrollWidth<=innerWidth'),'desktop no horizontal overflow '+str(width))
  check(not errors,'no JavaScript errors');await b.close()
 (O/'ui317-tests.json').write_text(json.dumps(dict(passed=len(checks),checks=checks),ensure_ascii=False,indent=2));print('RETRY317 UI PASS',len(checks))
asyncio.run(main())
