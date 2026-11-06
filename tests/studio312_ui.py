import asyncio,json,pathlib,shutil,re,copy
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parents[1];O=R/'verification-3.12';O.mkdir(exist_ok=True)
f=json.loads((R/'verification-3.6/ui-fixture.json').read_text());f['settings'].update(toolMode=True,unifiedAgent=True,interfaceTheme='light',experimentalProfileId='');f['current'].update(title='Новый чат',messages=[],attachments=[]);f['sessions']=[dict(id=f['current']['id'],title='Новый чат')]
f['mcp'].update(connected=0,totalTools=0)
for item in f['mcp']['presets']:item.update(connected=False,enabled=False,tools=0,toolNames=[],phase='idle',error='')
manifest=json.loads((R/'src/peft-adapters.json').read_text());f['peftCatalog']=[dict(id=m['id'],url=m['url'],likes=m['likes'],sourceLicense=m['sourceLicense'],weightBytes=m['weightBytes'],present=False,active=False) for m in manifest['models']];f['peftRuntimePresent']=False
package=json.loads((R/'src/nora-agent.json').read_text());f['agentProfiles']=package.get('specializations',package.get('Specializations',[]));f['unifiedProfileCount']=len(f['agentProfiles'])
checks=[]
def check(v,n):
 assert v,n
 checks.append(n)
async def main():
 global f
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox']);page=await b.new_page(viewport=dict(width=1440,height=960));errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome={webview:{postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}}');await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(f)=>receive({type:"state",data:f})',f)
  async def bake(name,script=''):
   content=await page.content();content=content.replace('</body>','<script>receive('+json.dumps({'type':'state','data':f},ensure_ascii=False)+');'+script+'</script></body>');(O/(name+'.html')).write_text(content)
  check(not errors,'no startup errors');check(not await page.locator('#right').is_visible(),'context drawer hidden by default');check(await page.locator('.main').evaluate('e=>e.clientWidth>1100'),'wide primary workspace')
  ids=await page.locator('[id]').evaluate_all('els=>els.map(e=>e.id)');check(len(ids)==len(set(ids)),'all HTML identifiers unique')
  await page.locator('[data-start=code]').click();check(await page.locator('#prompt').input_value()=='Помоги написать код: ','new code tile fills composer');await page.locator('#prompt').fill('Черновик не должен потеряться')
  await page.locator('#theme-btn').click();check(await page.evaluate('document.documentElement.dataset.theme==="dark"'),'dark theme switches');check(await page.evaluate('__sent.at(-1).action==="uiTheme"&&__sent.at(-1).data.theme==="dark"'),'theme sent to native persistence');check(await page.locator('#prompt').input_value()=='Черновик не должен потеряться','theme change preserves draft');await bake('studio-dark',"applyTheme('dark')")
  await page.locator('#theme-btn').click();check(await page.evaluate('interfaceTheme==="system"'),'system theme supported');await page.emulate_media(color_scheme='dark');await page.wait_for_timeout(150);check(await page.evaluate('document.documentElement.dataset.theme==="dark"'),'system dark preference followed');await page.emulate_media(color_scheme='light');await page.wait_for_timeout(80);check(await page.evaluate('document.documentElement.dataset.theme==="light"'),'system theme updates live');await page.locator('#theme-btn').click();check(await page.evaluate('interfaceTheme==="light"'),'light theme explicit');await page.locator('#prompt').fill('');f['settings']['interfaceTheme']='light';await bake('studio-light')
  await page.locator('#panel-btn').click();check(await page.locator('#right').is_visible(),'context drawer opens');check(await page.locator('.main').evaluate('e=>e.inert'),'background inactive behind drawer');check(await page.locator('#shell-scrim').is_visible(),'drawer scrim visible');await bake('studio-drawer','closeShell();toggleDrawer()');await page.locator('#drawer-close').click();check(not await page.locator('#right').is_visible(),'drawer closes');check(not await page.locator('.main').evaluate('e=>e.inert'),'main interactive again')
  await page.locator('[data-start=repo]').click();check(await page.locator('#repo-modal').is_visible(),'repository tile opens real import');await page.evaluate('closeModal()')
  # Every preserved dialog remains scrollable and contained in both themes / widths.
  modals=await page.locator('.modal-backdrop').evaluate_all('els=>els.map(e=>e.id)')
  for width in [1440,960,620,390]:
   await page.set_viewport_size(dict(width=width,height=960))
   for theme in ['light','dark']:
    await page.evaluate('(t)=>applyTheme(t)',theme)
    for id in modals:
     await page.evaluate('(id)=>modal(id)',id)
     check(await page.locator('#'+id+' .modal').evaluate('e=>e.scrollWidth<=e.clientWidth+1'),f'{id} no horizontal overflow {width} {theme}')
     check(await page.locator('#'+id+' .modal').evaluate('e=>{const r=e.getBoundingClientRect();return r.x>=0&&r.right<=innerWidth+1&&r.y>=0&&r.bottom<=innerHeight+1}'),f'{id} within viewport {width} {theme}')
     if width==1440 and theme=='light':await bake('dialog-'+id, dict(**{'settings-modal':'settingsModal()','skills-modal':'skillsModal()','agent-library-modal':'agentLibrary()','catalog-modal':'openCatalog()','peft-modal':'openPeft()','access-modal':'accessModal()'}).get(id,f"modal('{id}')"))
     await page.evaluate('closeModal()')
   check(await page.evaluate('document.documentElement.scrollWidth<=innerWidth'),'shell contained '+str(width))
  await page.set_viewport_size(dict(width=390,height=844));await page.evaluate("applyTheme('light');renderThread()");check(await page.locator('#scroll').evaluate('e=>e.scrollTop===0'),'empty state starts at top on mobile');await bake('studio-mobile')
  await page.locator('#menu-btn').click();check(await page.locator('#sidebar').is_visible(),'mobile navigation opens');await bake('studio-mobile-menu',"closeShell();$('menu-btn').click()");await page.locator('#sidebar-close').click();check(not await page.locator('#sidebar').is_visible(),'mobile navigation closes')
  await page.locator('#panel-btn').click();check(await page.locator('#right').is_visible(),'mobile tools accessible');await page.locator('#drawer-close').click()
  await page.evaluate('openConnections()');first=page.locator('#mcp-modal button').first;last=page.locator('#mcp-modal button').last
  # Use actual focusable list because some controls live in hidden tab panels.
  await page.evaluate('''()=>{const e=[...document.querySelectorAll('#mcp-modal button,#mcp-modal input,#mcp-modal select,#mcp-modal textarea,#mcp-modal summary')].filter(e=>!e.disabled&&e.getClientRects().length);e.at(-1).focus()}''');await page.keyboard.press('Tab');check(await page.evaluate('document.activeElement.closest("#mcp-modal")!==null'),'dialog keyboard focus trapped');await page.keyboard.press('Escape');check(not await page.locator('#mcp-modal').is_visible(),'Escape closes dialog')
  await page.set_viewport_size(dict(width=1440,height=960));await page.evaluate('settingsModal()');await bake('studio-settings','settingsModal()');await page.evaluate('closeModal();openCatalog()');await bake('studio-models','openCatalog()');await page.evaluate('closeModal();openPeft()');await bake('studio-peft','openPeft()');await page.evaluate('closeModal()')
  # Render actual message fixture, including code/work cards.
  populated=json.loads((R/'verification-3.6/ui-fixture.json').read_text());populated['settings']['interfaceTheme']='dark';await page.evaluate('(f)=>receive({type:"state",data:f})',populated);prev=f;f=populated;await bake('studio-chat');f=prev
  check(not errors,'no runtime JavaScript errors: '+str(errors));await b.close()
 (O/'studio-ui-tests.json').write_text(json.dumps(dict(passed=len(checks),checks=checks),ensure_ascii=False,indent=2));print('STUDIO UI PASS',len(checks))
asyncio.run(main())
