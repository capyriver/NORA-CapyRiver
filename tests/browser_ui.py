import asyncio,ast,json,pathlib,shutil
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parent.parent
tr=ast.parse((R/'tests/ui_test.py').read_text());fixture=eval(compile(ast.Expression(next(n.value for n in tr.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='fixture' for t in n.targets))),'fixture','eval'),{'__builtins__':{}})
fixture['current']['messages'][0]['text']='Открой репозиторий и покажи браузер рядом с чатом.'
fixture['current']['title']='Работа с браузером';fixture['sessions'][0]['title']='Работа с браузером'
fixture['settings'].update(mode=2,fastQwenMode=True,responseMode=1,reasoning=True)
fixture['current']['messages'][1]['text']='Открываю репозиторий во встроенном браузере. Нажми карточку ниже: переписка останется слева, а страница откроется справа. Чтобы самому перейти по странице или ввести адрес, нажми «Перехватить управление». Вернуть браузер помощнику можно отдельной кнопкой после остановки текущего запроса.'
checks=[]
async def run():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox']);page=await b.new_page(viewport={'width':1440,'height':940});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome=window.chrome||{};window.chrome.webview={postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}')
  await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(d)=>receive({type:"state",data:d})',fixture)
  async def check(ok,n):
   assert ok,n
   checks.append(n)
  await check(await page.locator('#response-mode option').count()==3,'three response modes in composer')
  for i in range(3):
   await page.locator('#response-mode').select_option(str(i));await check(await page.evaluate(f'__sent.at(-1).action==="responseMode"&&__sent.at(-1).data.mode==={i}'),f'mode {i} host payload')
  data={'title':'Документация проекта','url':'https://github.com/msitarzewski/agency-agents','preview':'','manual':False}
  await page.evaluate('(d)=>receive({type:"browserState",data:d})',data);await check(await page.locator('.browser-card').count()==1,'compact browser card in chat');await check(await page.locator('.browser-fold').count()==1,'long last response folded')
  await page.locator('.browser-unfold').click();await check(await page.locator('.browser-fold').count()==0,'last response can expand');await page.locator('.browser-card>button').click();await check(await page.evaluate('__sent.at(-1).action==="browserExpand"'),'card opens native split')
  await page.evaluate('receive({type:"busy",data:true})');await page.locator('.browser-controls button').click();await check(await page.evaluate('__sent.at(-1).action==="browserTakeover"'),'takeover remains available while AI busy')
  data['manual']=True;await page.evaluate('(d)=>receive({type:"browserState",data:d})',data);await check(await page.locator('.browser-controls button').is_disabled(),'cannot resume before cancelled operation settles');await page.evaluate('receive({type:"busy",data:false})');await page.evaluate('(d)=>receive({type:"browserState",data:d})',data);await page.locator('.browser-controls button').click();await check(await page.evaluate('__sent.at(-1).action==="browserResume"'),'resume explicit')
  await page.set_viewport_size({'width':720,'height':940});await page.evaluate('receive({type:"browserSplit",data:true})');await check(await page.locator('.sidebar').is_hidden() and await page.locator('.right').is_hidden(),'sidebars hidden in half-width chat');await check(await page.evaluate('document.documentElement.scrollWidth===innerWidth'),'split chat no horizontal overflow')
  await page.set_viewport_size({'width':390,'height':844});await page.evaluate('receive({type:"browserSplit",data:false})');await check(await page.evaluate('document.documentElement.scrollWidth===innerWidth'),'mobile browser card no overflow')
  bad={**data,'title':'<img src=x onerror=window.pwned=1>','url':'<script>window.pwned=1</script>','preview':'javascript:alert(1)'};await page.evaluate('(d)=>receive({type:"browserState",data:d})',bad);await check(await page.evaluate('window.pwned===undefined') and await page.locator('.browser-card img').count()==0,'browser card treats page metadata as text');await check(not errors,'no JS errors')
  source=(R/'src/ui.html').read_text();data['manual']=False
  payload=json.dumps(fixture,ensure_ascii=False).replace('</','<\\/');state=json.dumps(data,ensure_ascii=False).replace('</','<\\/')
  (R/'out/browser-card-2.5.html').write_text(source.replace('</body>',f'<script>setTimeout(()=>{{receive({{type:"state",data:{payload}}});receive({{type:"browserState",data:{state}}});scrollBottom();}},150)</script></body>'))
  (R/'verification-2.5/browser-ui.json').write_text(json.dumps({'passed':len(checks),'checks':checks,'nativeScope':'HTML bridge only; not native WebView2 Windows integration'},ensure_ascii=False,indent=2));print(len(checks),'browser UI checks passed');await b.close()
asyncio.run(run())
