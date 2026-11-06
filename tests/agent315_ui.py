import asyncio,json,pathlib,shutil,copy
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parents[1];O=R/'verification-3.15';O.mkdir(exist_ok=True)
f=json.loads((R/'verification-3.6/ui-fixture.json').read_text());f['settings'].update(toolMode=True,unifiedAgent=True,interfaceTheme='dark',mode=2,responseMode=2,accessMode=2)
f['activeModelName']='Qwen2.5 Coder · 7B';f['current'].update(title='Публикация сайта',draft='',attachments=[])
f['current']['messages']=[dict(id='user1',role='user',state='complete',text='Создай сайт и опубликуй на Cloudflare'),dict(id='pause1',role='assistant',state='complete',text='Задача приостановлена: повтор схем без нового результата. Цель и результаты сохранены.',taskId='task315',taskPending=True,startedAt='2026-10-31T16:46:33Z',elapsedSeconds=8,work=[dict(at='2026-10-31T16:46:33Z',text='Доступна схема MCP: cloudflare / execute'),dict(at='2026-10-31T16:46:41Z',text='Повтор пропущен без вызова: mcp_describe_tool · cloudflare / execute')],artifacts=[])]
checks=[]
def check(v,n):
 assert v,n
 checks.append(n)
async def main():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox']);page=await b.new_page(viewport=dict(width=1440,height=960));errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome={webview:{postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}}');await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(f)=>receive({type:"state",data:f})',f)
  check('Приостановлено' in await page.locator('#msg-pause1 .work summary').inner_text(),'paused work not labeled completed')
  check(await page.locator('.resume-task').count()==1,'one resume button for pending response')
  check(await page.locator('.resume-task').is_visible(),'resume button visible in chat')
  await page.locator('.resume-task').click();check(await page.evaluate('__sent.at(-1).action==="resumeTask"&&__sent.at(-1).data.taskId==="task315"'),'resume carries exact task ID to host')
  await page.locator('#prompt').fill('Мой черновик');await page.locator('.resume-task').click();check(await page.locator('#prompt').input_value()=='Мой черновик','resume does not replace typed draft in DOM')
  await page.screenshot(path=str(O/'paused-task.png'),full_page=True)
  await page.evaluate('setBusy(true)');check(await page.locator('.resume-task').is_disabled(),'cannot submit resume twice during execution');await page.evaluate('setBusy(false)')
  for width in [1440,1100]:
   await page.set_viewport_size(dict(width=width,height=960));check(await page.evaluate('document.documentElement.scrollWidth<=innerWidth'),'desktop no horizontal overflow '+str(width));check(await page.locator('.resume-task').is_visible(),'resume visible '+str(width))
  done=copy.deepcopy(f);done['current']['messages'][-1].update(taskPending=False,text='Cloudflare вернул ссылку публикации: https://test.example.workers.dev')
  await page.evaluate('(f)=>receive({type:"state",data:f})',done);check(await page.locator('.resume-task').count()==0,'completed task has no resume control');check('Выполнено' in await page.locator('.work summary').inner_text(),'completed receipt labeled completed')
  running=copy.deepcopy(f);running['current']['messages'][-1]['state']='streaming';await page.evaluate('(f)=>receive({type:"state",data:f})',running);check(await page.locator('.resume-task').count()==0,'streaming response has no resume control');check('Работает' in await page.locator('.work summary').inner_text(),'streaming retains working status')
  unsafe=copy.deepcopy(f);unsafe['current']['messages'][-1]['text']='<img src=x onerror=window.bad=1>';await page.evaluate('(f)=>receive({type:"state",data:f})',unsafe);check(await page.locator('#msg-pause1 .content img').count()==0,'checkpoint text cannot inject HTML');check(not errors,'no JS errors')
  await b.close()
 (O/'ui-tests.json').write_text(json.dumps(dict(passed=len(checks),checks=checks),ensure_ascii=False,indent=2));print('AGENT 3.15 UI PASS',len(checks))
asyncio.run(main())
