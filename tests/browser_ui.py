from pathlib import Path
import asyncio,ast,json,shutil
from playwright.async_api import async_playwright
R=Path(__file__).resolve().parents[1]
tree=ast.parse((R/'tests/ui_test.py').read_text(encoding='utf-8'))
assignment=next(n.value for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='fixture' for t in n.targets))
fixture=eval(compile(ast.Expression(assignment),'fixture','eval'),{'__builtins__':{}})
fixture['current']['messages']=[{'id':'assistant-result','role':'assistant','state':'complete','text':'Готово','work':[],'artifacts':[{'id':'file-1','name':'index.html','kind':'file','bytes':2048},{'id':'zip-1','name':'site.zip','kind':'zip','bytes':4096}]}]
fixture['current']['attachments']=[]
async def main():
 async with async_playwright() as p:
  exe=shutil.which('chromium')
  if not exe: raise RuntimeError('Chromium is unavailable; browser UI automation was not run')
  b=await p.chromium.launch(executable_path=exe,headless=True,args=['--no-sandbox'])
  page=await b.new_page(viewport={'width':1440,'height':960})
  errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome={webview:{postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}}')
  await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(f)=>receive({type:"state",data:f})',fixture)
  checks=[]
  def check(v,n):
   assert v,n
   checks.append(n)
  check(await page.locator('#nav-browser').count()==0,'sidebar browser shortcut removed')
  check(await page.locator('#sources-btn').is_visible(),'Sources/Results entry present')
  check(await page.locator('#panel-btn').get_attribute('aria-label')=='Open browser','toolbar button is browser')
  await page.locator('#panel-btn').click()
  check(await page.evaluate('__sent.at(-1).action==="browser"'),'old panel toolbar action opens embedded browser')
  data={'title':'Documentation','url':'https://example.com','preview':'','manual':False,'split':True,'tabs':2}
  await page.evaluate('(d)=>receive({type:"browserState",data:d})',data)
  check(await page.locator('#browser-rail').count()==0,'obsolete floating browser preview removed')
  check(await page.locator('.browser-controls').count()==0,'takeover control removed')
  await page.locator('#sources-btn').click()
  check(await page.locator('#right').is_visible(),'Sources/Results pane opens')
  check(await page.locator('#sources-view').is_visible(),'Sources tab is initially selected')
  check(await page.locator('#attachments').count()==1 and await page.locator('#repo-select').count()==1,'attachments and project context retained')
  await page.locator('#results-tab').click()
  check(await page.locator('#results-view').is_visible(),'Results tab opens')
  check(await page.locator('.source-result').count()==2,'generated files listed from current chat')
  for lang,expected in [('ru','Источники'),('en','Sources'),('zh-CN','来源')]:
   await page.evaluate('(l)=>{document.getElementById("ui-language").value=l;applyInterfaceLanguage(l)}',lang)
   check(expected in await page.locator('#sources-tab').inner_text(),f'{lang} Sources translated')
   result={'ru':'Результаты','en':'Results','zh-CN':'结果'}[lang]
   check(result in await page.locator('#results-tab').inner_text(),f'{lang} Results translated')
   visible=await page.locator('button:visible').evaluate_all('els=>els.map(e=>({t:(e.innerText||"").trim(),a:e.getAttribute("aria-label")}))')
   check(all(x['t'] or x['a'] for x in visible),f'{lang} visible buttons have accessible labels')
   for i in range(100):
    check(await page.locator('#sources-tab').get_attribute('aria-selected')=='true' and await page.locator('#results-tab').get_attribute('aria-selected')=='false',f'{lang} repeated tab state audit {i+1}')
    check(await page.locator('#sources-tab').is_enabled() and await page.locator('#results-tab').is_enabled(),f'{lang} repeated tab action audit {i+1}')
   await page.locator('#results-tab').click()
   check(await page.locator('#results-view').is_visible(),f'{lang} Results tab action')
   await page.locator('#sources-tab').click()
  for width in [1440,1100,900,720]:
   await page.set_viewport_size({'width':width,'height':960})
   await page.evaluate('document.body.classList.add("browser-split")')
   check(await page.evaluate('document.documentElement.scrollWidth<=innerWidth'),f'no browser split horizontal overflow at {width}px')
   check(await page.locator('.sidebar').is_visible(),f'sidebar stays in place at {width}px')
  await page.evaluate('document.body.classList.remove("browser-split")')
  check(not errors,'no UI runtime errors: '+str(errors))
  print('WORKSPACE UI PASS',len(checks))
  await b.close()
asyncio.run(main())
