import asyncio,ast,json,pathlib,shutil
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parent.parent
tr=ast.parse((R/'tests/ui_test.py').read_text());fixture=eval(compile(ast.Expression(next(n.value for n in tr.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='fixture' for t in n.targets))),'fixture','eval'),{'__builtins__':{}})
fixture['settings'].update(mode=2,fastQwenMode=True,unifiedAgent=True,maxTokens=512,contextTokens=4096,responseMode=0,reasoning=False)
fixture['activeModelName']='Qwen2.5 Coder 3B';fixture['unifiedProfileCount']=433;fixture['agents']=[{'id':'nora-agent','name':'НОРА Агент','description':'Все 429 специализаций объединены внутри'}]
fixture['current']['title']='НОРА Агент';fixture['sessions'][0]['title']='НОРА Агент';fixture['current']['messages'][0]['text']='Включи НОРА Агент и проверь мой код.';fixture['current']['messages'][1]['text']='Один агент: реализация и самопроверка в одном ответе. Переключатель находится под сообщением и в правой панели.'
checks=[]
async def run():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox']);page=await b.new_page(viewport={'width':1440,'height':960});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome=window.chrome||{};window.chrome.webview={postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}')
  await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(d)=>receive({type:"state",data:d})',fixture)
  async def check(ok,n):
   assert ok,n
   checks.append(n)
  await check(await page.locator('#agent-select').is_hidden(),'individual role selector removed from interface')
  await check(await page.locator('#agent-library-btn').is_hidden(),'429-role catalogue removed from interface')
  await check(await page.locator('#nora-agent-toggle').get_attribute('aria-checked')=='true','NORA agent starts enabled')
  await page.locator('#nora-agent-toggle').click();await check(await page.evaluate('__sent.at(-1).action==="noraAgent"&&__sent.at(-1).data.enabled===false'),'off switch posts explicit disable')
  fixture['settings']['unifiedAgent']=False;fixture['current']['agentId']='team';await page.evaluate('(d)=>receive({type:"state",data:d})',fixture)
  await check(await page.locator('#nora-agent-toggle').get_attribute('aria-checked')=='false','off state shown even for old team chat')
  await check('Выключен' in await page.locator('#nora-agent-card-toggle').inner_text(),'sidebar switch shows disabled')
  await page.locator('#nora-agent-toggle').click();await check(await page.evaluate('__sent.at(-1).data.enabled===true'),'on switch posts explicit enable')
  fixture['settings']['unifiedAgent']=True;await page.evaluate('(d)=>receive({type:"state",data:d})',fixture)
  await page.locator('#nora-agent-card').scroll_into_view_if_needed();await page.screenshot(path='/data/nora-fast/verification/nora-agent-on-3.4.png')
  await page.evaluate('receive({type:"busy",data:true})');await check(await page.locator('#nora-agent-toggle').is_disabled() and await page.locator('#nora-agent-card-toggle').is_disabled(),'switches cannot mutate an active response')
  await page.evaluate('receive({type:"busy",data:false})');await page.set_viewport_size({'width':390,'height':844});await page.screenshot(path='/data/nora-fast/verification/nora-agent-mobile-3.4.png')
  await check(await page.evaluate('document.documentElement.scrollWidth===innerWidth'),'mobile no horizontal overflow')
  await check(await page.locator('#nora-agent-toggle').is_visible(),'agent switch visible on mobile')
  await check(not errors,'no JavaScript errors')
  print(json.dumps({'passed':len(checks),'checks':checks,'errors':errors},ensure_ascii=False,indent=2));await b.close()
asyncio.run(run())
