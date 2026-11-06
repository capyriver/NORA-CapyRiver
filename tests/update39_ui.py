import asyncio,json,pathlib,shutil
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parent.parent
fixture=json.loads((R/'verification-3.6/ui-fixture.json').read_text())
package=json.loads((R/'src/nora-agent.json').read_text())
# Disk.Json is camelCase on wire; bundled file's properties follow the same policy.
profiles=package.get('specializations',package.get('Specializations',[]))
fixture.update(agentProfiles=profiles,unifiedProfileCount=len(profiles),browserToolsReady=True)
fixture['settings'].update(toolMode=True,unifiedAgent=True,experimentalProfileId='')
fixture['current']['messages'][0]['text']='В открытом Telegram найди BotFather. Сначала прочитай страницу.'
fixture['current']['messages'][-1].update(text='Браузер подключён. На странице найдено поле поиска. Сначала проверю элементы, затем запрошу подтверждение действия.\n\n```json\n{"tool":"browser_snapshot"}\n```',state='complete')
checks=[]
def check(v,n):
 assert v,n
 checks.append(n)
async def main():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox'])
  page=await b.new_page(viewport={'width':1440,'height':940});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome={webview:{postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}}')
  await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(x)=>receive({type:"state",data:x})',fixture)
  check(not errors,'no UI JavaScript startup errors')
  await page.locator('#agent-library-btn').evaluate('e=>e.click()');check(await page.locator('#agent-library-modal').is_visible(),'previously dead profile library opens')
  await page.locator('#agent-search').fill('python');check(await page.locator('.profile-item').count()>0,'real profile search returns actual imported records')
  check('из '+str(len(profiles)) in await page.locator('#agent-library-count').inner_text(),'library count is derived from source records')
  await page.locator('#agent-search').fill('not-an-existing-skill-abc');check(await page.locator('.profile-item').count()==0,'search empty state')
  await page.evaluate('closeModal()')
  await page.evaluate('receive({type:"browserState",data:{title:"Telegram Web",url:"https://web.telegram.org/k/",toolsReady:true,manual:false,tabs:1,split:false}})')
  check(await page.locator('#browser-access').is_visible(),'browser access status always visible in composer')
  check('подключён' in await page.locator('#browser-access-text').inner_text(),'actual ready state shown')
  await page.locator('#browser-access-check').click();check(await page.evaluate('__sent.at(-1).action==="browserDiagnose"'),'diagnostic button invokes native host, not dummy UI')
  await page.evaluate('receive({type:"browserState",data:{title:"Telegram Web",toolsReady:false}})');check('выключены' in await page.locator('#browser-access-text').inner_text(),'disabled tool state is not falsely ready')
  await page.evaluate('receive({type:"browserState",data:{title:"Telegram Web",toolsReady:true}})')
  for width in [1440,960,620,390]:
   await page.set_viewport_size({'width':width,'height':940})
   await page.evaluate('receive({type:"browserSplit",data:innerWidth<1000})')
   check(await page.evaluate('document.documentElement.scrollWidth<=innerWidth'),'document contained '+str(width))
   check(await page.locator('.thread').evaluate('e=>e.scrollWidth<=e.clientWidth+1'),'chat stays in pane '+str(width))
   check(await page.locator('#send-btn').is_visible(),'send remains usable '+str(width))
  await page.set_viewport_size({'width':620,'height':940})
  await page.evaluate('receive({type:"token",data:{id:state.current.messages.at(-1).id,text:"ОченьДлинныйТекст".repeat(300)+"\\n```python\\n"+"x".repeat(3000)+"\\n```"}})')
  check(await page.locator('.thread').evaluate('e=>e.scrollWidth<=e.clientWidth+1'),'long model response cannot escape chat')
  await page.evaluate('setBusy(true)');check(await page.locator('#browser-access-check').is_disabled(),'diagnostics not started during generation')
  await page.evaluate('setBusy(false)');check(not await page.locator('#browser-access-check').is_disabled(),'diagnostics available after stop')
  # Real extracted browser scripts run in Chromium against a Telegram-like editable DOM.
  target=await b.new_page(viewport={'width':700,'height':600})
  await target.set_content('''<body style="background:white"><div contenteditable="true" role="textbox" aria-label="Message" style="width:350px;height:70px;border:1px solid black"></div><button onclick="window.sent=true">Send</button><input type="password" value="NEVER_READ_THIS_VALUE"><input type="text" readonly value="READONLY_CANARY"><p>bot token 123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdef12345</p><p>Visible Telegram text</p></body>''')
  dom=(R/'src/browser-dom.js').read_text().replace('__NORA_NONCE__',json.dumps('123abc'));snapshot=await target.evaluate(dom)
  check(snapshot['elements'][0]['tag']=='contenteditable','Telegram editor discovered as writable contenteditable')
  check('NEVER_READ_THIS_VALUE' not in json.dumps(snapshot),'password value never extracted')
  check('READONLY_CANARY' not in json.dumps(snapshot),'normal input values never extracted')
  check('[скрыт токен]' in snapshot['text'],'Telegram bot token redacted from visible page text')
  scripts=json.loads((R/'verification-3.9/action-scripts.json').read_text())
  await target.evaluate(scripts['type']);check(await target.locator('[contenteditable]').inner_text()=='Привет, тест!','actual typing works in Telegram-like editor')
  await target.evaluate(scripts['click']);check(await target.evaluate('sent===true'),'actual button click handled by site')
  await target.evaluate(scripts['injection']);check(not await target.evaluate('Boolean(window.HACKED)'),'typed text not executed as JS')
  for key in ['secret','readonlyEditor']:
   rejected=False
   try: await target.evaluate(scripts[key])
   except Exception: rejected=True
   check(rejected,key+' action blocked in real DOM')
  await target.evaluate('document.body.insertAdjacentHTML("beforeend",\'<div style="position:fixed;inset:0;z-index:999;background:white">Overlay</div>\')')
  rejected=False
  try:await target.evaluate(scripts['type'])
  except Exception:rejected=True
  check(rejected,'covered browser target is not clicked or edited')
  check(not errors,'no UI runtime errors across all states')
  await b.close()
 (R/'verification-3.9/ui-results.json').write_text(json.dumps({'passed':len(checks),'checks':checks},ensure_ascii=False,indent=2));print(len(checks),'UI/DOM checks passed')
asyncio.run(main())
