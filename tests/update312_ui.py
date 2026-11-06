import asyncio,json,pathlib,shutil,copy
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parents[1];O=R/'verification-3.12';O.mkdir(exist_ok=True)
f=json.loads((R/'verification-3.6/ui-fixture.json').read_text())
f['settings'].update(toolMode=True,unifiedAgent=True,experimentalProfileId='',visionModel='')
f['current'].update(title='Новый чат',messages=[],attachments=[])
f['sessions']=[{'id':f['current']['id'],'title':'Новый чат'}]
f['mcp'].update(connected=0,totalTools=0)
for p in f['mcp']['presets']:p.update(connected=False,enabled=False,tools=0,toolNames=[],phase='idle',error='')
cloud=dict(id='cloudflare',name='Cloudflare · API аккаунта',description='Workers, DNS, R2 и другие операции твоего аккаунта. Каждый вызов требует подтверждения.',source='https://developers.cloudflare.com/agents/model-context-protocol/mcp-servers-for-cloudflare/',url='https://mcp.cloudflare.com/mcp',requirement='Вход через OAuth в системном браузере. Выбирай минимально необходимые права.',auth='oauth',enabled=False,connected=False,tools=0,toolNames=[],phase='idle',error='')
f['mcp']['presets']=[cloud]+[p for p in f['mcp']['presets'] if p['id']!='cloudflare']
checks=[]
def check(v,n):
 assert v,n
 checks.append(n)
async def main():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox'])
  page=await b.new_page(viewport={'width':1440,'height':960});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome={webview:{postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}}')
  await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(f)=>receive({type:"state",data:f})',f)
  check(not errors,'no startup JS errors');check(await page.locator('#nav-connections').is_visible(),'connections always accessible from sidebar');check(await page.locator('#nav-browser').is_visible(),'browser always accessible from sidebar')
  await page.locator('#nav-browser').click();check(await page.evaluate('__sent.at(-1).action==="browser"'),'browser shortcut invokes host')
  async def bake(name,script=''):
   content=await page.content();content=content.replace('</body>','<script>receive('+json.dumps({'type':'state','data':f},ensure_ascii=False)+');'+script+'</script></body>');(O/name).write_text(content)
  await bake('home.html')
  await page.locator('#nav-connections').click();check(await page.locator('#mcp-modal').is_visible(),'connections modal opens');card=page.locator('[data-server="cloudflare"]');check(await card.locator('.primary').evaluate('e=>!e.disabled'),'connect works without enabling first');await card.locator('.primary').click();check(await page.evaluate('__sent.at(-1).action==="mcpConnect" && __sent.at(-1).data.id==="cloudflare"'),'single click requests real connection');check('Не подключён' in await card.inner_text(),'click alone does not mark connected')
  await bake('connections.html','openConnections()')
  f['mcp']['presets'][0]['phase']='connecting';await page.evaluate('(f)=>{receive({type:"state",data:f});receive({type:"busy",data:true})}',f)
  check(await card.locator('.primary').evaluate('e=>!e.disabled'),'cancel enabled during connection');check(await card.locator('.primary').inner_text()=='Отменить','connecting phase visible');await card.locator('.primary').click();check(await page.evaluate('__sent.at(-1).action==="mcpCancel"'),'cancel invokes host');check(await page.locator('[data-server="github"] .primary').evaluate('e=>e.disabled'),'parallel connection disabled');await bake('connecting.html','openConnections();receive({type:"busy",data:true})')
  f['mcp']['presets'][0].update(phase='error',error='Сервер недоступен. Повтори подключение.');await page.evaluate('(f)=>{receive({type:"state",data:f});receive({type:"busy",data:false})}',f)
  check(await card.locator('[role="alert"]').is_visible(),'failure state visible');check(await card.locator('.primary').evaluate('e=>!e.disabled'),'retry enabled after failure');await bake('connection-error.html','openConnections()')
  f['mcp']['presets'][0].update(phase='connected',connected=True,enabled=True,tools=3,toolNames=['search','execute','docs'],error='');f['mcp'].update(connected=1,totalTools=3);await page.evaluate('(f)=>receive({type:"state",data:f})',f)
  check('3 функций' in await card.inner_text(),'connected tool count supplied by host');check(await page.locator('#nav-connection-count').inner_text()=='1','sidebar count follows actual host');await card.locator('.secondary').click();check(await page.evaluate('__sent.at(-1).action==="mcpDisconnect"'),'disconnect invokes host');await card.locator('summary').click();await card.locator('details button').click();check(await page.evaluate('__sent.at(-1).action==="mcpForget"'),'forget is explicit separate action');await card.locator('summary').click()
  await page.locator('[data-connection-tab="connected"]').click();check(await page.locator('.connector-card').count()==1,'connected tab actual only')
  await page.locator('[data-connection-tab="browse"]').click();await page.locator('#mcp-catalog-filter').fill('nothing-zxq');check(await page.locator('.connection-empty').is_visible(),'empty search state');await page.locator('#mcp-catalog-filter').fill('')
  for width in [1440,960,620,390]:
   await page.set_viewport_size({'width':width,'height':960});await page.evaluate('openConnections()');check(await page.evaluate('document.documentElement.scrollWidth<=innerWidth'),'document contained '+str(width));check(await page.locator('.mcp-modal').evaluate('e=>e.scrollWidth<=e.clientWidth+1'),'modal contained '+str(width));await card.locator('.primary').scroll_into_view_if_needed();check(await card.locator('.primary').is_visible(),'connect visible '+str(width))
  await page.set_viewport_size({'width':390,'height':960});await page.evaluate('closeModal()');await bake('home-mobile.html')
  await page.evaluate('openConnections()');await bake('connections-mobile.html','openConnections()')
  await page.evaluate('closeModal();receive({type:"browserState",data:{title:"Документация проекта",url:"https://example.com",toolsReady:true,manual:false,tabs:1}})');check(await page.locator('#browser-access-check').is_visible(),'DOM inspection accessible');await page.locator('#browser-access-check').click();check(await page.evaluate('__sent.at(-1).action==="browserDiagnose"'),'DOM button invokes native host');await page.locator('#browser-access-vision').click();check(await page.locator('#settings-modal').is_visible(),'missing vision routes to settings, not fake result');await page.evaluate('closeModal()');f['settings']['visionModel']='local-vision';await page.evaluate('(f)=>receive({type:"state",data:f})',f);await page.locator('#browser-access-vision').click();check(await page.evaluate('__sent.at(-1).action==="browserVision"'),'configured vision invokes real screenshot tool')
  await bake('browser-mobile.html','receive({type:"browserState",data:{title:"Документация проекта",url:"https://example.com",toolsReady:true,manual:false,tabs:1}})')
  # Reject UI injection and validate no dead image symbols in newly added navigation.
  f['mcp']['presets'][0]['name']='<img src=x onerror="window.PWN=1">';await page.evaluate('(f)=>receive({type:"state",data:f})',f);check(not await page.evaluate('Boolean(window.PWN)'),'server strings rendered literally')
  check(not errors,'no JavaScript runtime errors: '+str(errors))
  await b.close()
 (O/'ui-tests.json').write_text(json.dumps(dict(passed=len(checks),checks=checks),ensure_ascii=False,indent=2));print('UI PASS',len(checks))
asyncio.run(main())
