import asyncio,ast,json,pathlib,shutil
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parent.parent
tr=ast.parse((R/'tests/ui_test.py').read_text());fixture=eval(compile(ast.Expression(next(n.value for n in tr.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='fixture' for t in n.targets))),'fixture','eval'),{'__builtins__':{}})
fixture['settings'].update(mode=2,fastQwenMode=True,unifiedAgent=True,maxTokens=512,contextTokens=4096,responseMode=0,reasoning=False)
fixture['activeModelName']='Llama 3.2 1B Instruct';fixture['unifiedProfileCount']=433;fixture['modelCatalog']=json.loads((R/'verification-2.6/catalog-specs.json').read_text())
fixture['current']['title']='Быстрый код и браузер';fixture['sessions'][0]['title']='Быстрый код и браузер'
fixture['current']['messages'][0]['text']='Открой браузер, я войду в аккаунт сам.'
fixture['current']['messages'][1]['text']='Браузер открыт. Нажми Watch справа, чтобы смотреть страницу. Клик в браузере передаст управление тебе; через 10 секунд без действий NORA продолжит задачу.'
checks=[]
async def run():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox']);page=await b.new_page(viewport={'width':1440,'height':940});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome=window.chrome||{};window.chrome.webview={postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}')
  await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(d)=>receive({type:"state",data:d})',fixture)
  async def check(ok,n):
   assert ok,n
   checks.append(n)
  await check(await page.locator('#browser-btn').count()==0,'obsolete browser button removed')
  await check(await page.locator('#agent-select option').count()==1,'single unified agent selector')
  await check('НОРА Агент' in await page.locator('#agent-select').inner_text(),'single merged name replaces role count')
  data={'title':'Открываю Telegram Web','url':'https://web.telegram.org/k/','preview':'','manual':False,'remaining':0,'split':False}
  await page.evaluate('(d)=>receive({type:"browserState",data:d})',data)
  await check(await page.locator('#browser-rail').count()==1,'right-hand Browser card')
  await check(await page.locator('#browser-rail').evaluate('(e)=>e.getBoundingClientRect().x')>1000,'card lies in right rail')
  await page.locator('.browser-summary').hover();await page.wait_for_timeout(350)
  await check(await page.locator('.browser-watch').evaluate('(e)=>e.getBoundingClientRect().height')>150,'hover animates Watch preview')
  await page.screenshot(path='/data/nora-fast/verification/browser-hover.png')
  await page.locator('.browser-watch').click();await check(await page.evaluate('__sent.at(-1).action==="browserExpand"'),'Watch opens native browser pane')
  data.update(manual=True,remaining=9);await page.evaluate('(d)=>receive({type:"browserState",data:d})',data)
  await check('9 с' in await page.locator('.browser-lease').inner_text(),'manual countdown visible')
  await page.evaluate('receive({type:"browserSplit",data:true})');await page.set_viewport_size({'width':720,'height':940})
  await check(await page.locator('.sidebar').is_hidden() and await page.locator('.right').is_hidden(),'native split leaves full chat half')
  await check(await page.evaluate('document.documentElement.scrollWidth===innerWidth'),'split no overflow')
  await page.evaluate('receive({type:"browserSplit",data:false})');await page.set_viewport_size({'width':390,'height':844});await page.wait_for_timeout(100)
  await check(await page.evaluate('document.documentElement.scrollWidth===innerWidth'),'mobile no horizontal overflow')
  await page.screenshot(path='/data/nora-fast/verification/browser-mobile.png')
  await page.set_viewport_size({'width':1440,'height':940});await page.evaluate('browserState=null;renderBrowser()')
  await page.locator('#models-btn').click();await page.wait_for_timeout(100)
  await check(await page.locator('.model-item').count()==19,'all nineteen models render');await check(await page.locator('#tab-gguf').get_attribute('aria-pressed')=='true','GGUF catalog is default')
  await page.screenshot(path='/data/nora-fast/verification/catalog.png');await page.locator('#fast-start-model').click();await check(await page.evaluate('__sent.at(-1).action==="chooseModel"&&__sent.at(-1).data.id==="llama-1b"'),'one-click Llama setup payload')
  await check(not errors,'no JavaScript errors')
  print(json.dumps({'passed':len(checks),'checks':checks,'errors':errors},ensure_ascii=False,indent=2));await b.close()
asyncio.run(run())
