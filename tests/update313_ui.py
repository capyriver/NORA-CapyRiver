import asyncio,json,pathlib,shutil,copy
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parents[1];O=R/'verification-3.13';O.mkdir(exist_ok=True)
f=json.loads((R/'verification-3.6/ui-fixture.json').read_text());f['settings'].update(toolMode=True,unifiedAgent=True,interfaceTheme='light',experimentalProfileId='',mode=2,responseMode=0,accessMode=0)
f['current'].update(title='Новый чат',messages=[],attachments=[]);f['sessions']=[dict(id=f['current']['id'],title='Новый чат'),dict(id='secondchat',title='Второй чат')]
f['activeModelName']='Qwen2.5 Coder · 1.5B';f['modelCatalog']=[dict(id='coder',name='Qwen2.5 Coder · 1.5B',active=True,thinking=False)]
f['mcp']['connected']=1;f['mcp']['totalTools']=4
f['mcp']['presets'].append(dict(id='cloudflare',name='Cloudflare · API аккаунта',description='Workers, DNS, R2',url='https://mcp.cloudflare.com/mcp',source='Cloudflare',requirement='OAuth',auth='oauth',package='',version='',toolNames=['search','execute']))
for item in f['mcp']['presets']:item.update(connected=item['id']=='cloudflare',enabled=item['id']=='cloudflare',tools=4 if item['id']=='cloudflare' else 0,phase='connected' if item['id']=='cloudflare' else 'idle',error='')
checks=[]
def check(v,n):
 assert v,n
 checks.append(n)
async def main():
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox']);page=await b.new_page(viewport=dict(width=1440,height=960));errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.add_init_script('window.__sent=[];window.chrome={webview:{postMessage(x){__sent.push(JSON.parse(x))},addEventListener(){}}}');await page.goto((R/'src/ui.html').as_uri());await page.evaluate('(f)=>receive({type:"state",data:f})',f)
  async def bake(name,script=''):
   content=await page.content();content=content.replace('</body>','<script>receive('+json.dumps({'type':'state','data':f},ensure_ascii=False)+');'+script+'</script></body>');(O/(name+'.html')).write_text(content)
  check(not errors,'no startup JS errors');ids=await page.locator('[id]').evaluate_all('els=>els.map(e=>e.id)');check(len(ids)==len(set(ids)),'unique DOM identifiers')
  check(await page.evaluate('document.documentElement.dataset.theme==="dark"'),'fixed dark even legacy light setting');await page.emulate_media(color_scheme='light');await page.evaluate("applyTheme('light')");check(await page.evaluate('document.documentElement.dataset.theme==="dark"'),'system/light cannot switch the fixed theme')
  check(await page.locator('#response-mode option[value="1"]').evaluate('e=>e.disabled'),'nonthinking model explains unsupported thinking');check('не поддерживается' in await page.locator('#response-mode option[value="1"]').inner_text(),'reason displayed instead of silent lock');check(await page.locator('#response-mode option[value="2"]').evaluate('e=>!e.disabled'),'autoactions available for Coder')
  await page.locator('#response-mode').select_option('2');check(await page.evaluate('__sent.at(-1).action==="responseMode"&&__sent.at(-1).data.mode===2'),'autoaction selection reaches host');await page.locator('#response-mode').select_option('0')
  await page.locator('#theme-btn').click();check(await page.evaluate('__sent.at(-1).action==="window"&&__sent.at(-1).data.command==="fullscreen"'),'fullscreen button reaches host');await page.keyboard.press('F11');check(await page.evaluate('__sent.at(-1).data.command==="fullscreen"'),'F11 reaches native host from WebView');await page.evaluate('receive({type:"windowState",data:{fullscreen:false}})')
  await page.locator('#prompt').fill('Черновик');await page.locator('#panel-btn').click();check(not await page.locator('#video-btn').is_visible(),'legacy video action absent from primary menu');check(not await page.locator('#capture-output-btn').is_visible(),'whole-screen capture removed from primary menu');check(await page.locator('#zip-btn').is_visible(),'ZIP export retained');check(await page.locator('#files-btn').is_visible(),'file attachment retained');await bake('workspace-tools','closeShell();toggleDrawer()');await page.locator('#drawer-close').click();check(await page.locator('#prompt').input_value()=='Черновик','drawer preserves draft')
  await page.locator('.chat-more').nth(1).click();check(await page.locator('#chat-actions-modal').is_visible(),'per-chat actions open');check(await page.locator('#chat-name').input_value()=='Второй чат','actions target selected chat not current');await page.locator('#chat-name').fill('Переименован');await page.locator('#chat-rename-form button[type=submit]').click();check(await page.evaluate('__sent.at(-1).action==="renameChat"&&__sent.at(-1).data.id==="secondchat"&&__sent.at(-1).data.title==="Переименован"'),'rename sends exact selected ID')
  await page.locator('.chat-more').nth(1).click();await page.locator('#chat-action-delete').click();check(await page.locator('#confirm-modal').is_visible(),'deletion gets explicit confirmation');await page.locator('#confirm-yes').click();check(await page.evaluate('__sent.at(-1).action==="deleteChat"&&__sent.at(-1).data.id==="secondchat"'),'delete targets selected chat');await page.evaluate('receive({type:"chatDeleted",data:{id:"secondchat"}})');check(await page.locator('#undo-bar').is_visible(),'undo shown');await page.locator('#undo-delete').click();check(await page.evaluate('__sent.at(-1).action==="restoreChat"'),'undo reaches host')
  await page.locator('#prompt').fill('');await bake('workspace-desktop')
  # in-chat permission card is live even while generation is busy, no modal/inert application
  await page.evaluate('setBusy(true);receive({type:"approval",data:{id:"test",title:"Разрешить действие Cloudflare?",description:"Cloudflare / execute\\nПубликация сайта в вашем аккаунте. Результат станет доступен в интернете."}})')
  check(await page.locator('.approval-card').count()==1,'approval inserted once in chat');check(await page.locator('#approval-test').is_visible(),'approval visible while busy');check(await page.locator('#approval-test button').last.evaluate('e=>{const r=e.getBoundingClientRect(),v=document.querySelector("#scroll").getBoundingClientRect();return r.top>=v.top&&r.bottom<=v.bottom}'),'approval buttons inside scroll viewport');check(not await page.locator('.app').evaluate('e=>e.inert'),'approval does not block application with modal');check(await page.locator('#approval-test button').first.evaluate('e=>!e.disabled'),'approval actionable while busy');await bake('workspace-approval','setBusy(true);receive({type:"approval",data:{id:"test",title:"Разрешить действие Cloudflare?",description:"Cloudflare / execute\\nПубликация сайта в вашем аккаунте. Результат станет доступен в интернете."}})')
  await page.locator('#approval-test button').first.click();check(await page.evaluate('__sent.at(-1).action==="approvalDecision"&&__sent.at(-1).data.id==="test"&&__sent.at(-1).data.allow'),'allow carries request ID');check(await page.locator('#approval-test button').first.evaluate('e=>e.disabled'),'duplicate decision prevented');await page.evaluate('receive({type:"approvalDone",data:{id:"test"}});setBusy(false)');check(await page.locator('.approval-card').count()==0,'resolved approval removed')
  await page.evaluate('receive({type:"approval",data:{id:"deny",title:"<script>bad()</script>",description:"<img src=x onerror=bad()>"}})');check(await page.locator('#approval-deny script,#approval-deny img').count()==0,'tool descriptions escaped not HTML');await page.locator('#approval-deny button').last.click();check(await page.evaluate('__sent.at(-1).data.allow===false'),'deny propagated');await page.evaluate('receive({type:"approvalDone",data:{id:"deny"}})')
  # all preserved dialogs remain usable and within bounds
  modals=await page.locator('.modal-backdrop').evaluate_all('els=>els.map(e=>e.id)')
  for width in [1440,1280,1100]:
   await page.set_viewport_size(dict(width=width,height=960 if width>390 else 844))
   check(await page.evaluate('document.documentElement.scrollWidth<=innerWidth'),'shell no horizontal overflow '+str(width))
   for id in modals:
    await page.evaluate('(id)=>modal(id)',id)
    check(await page.locator('#'+id+' .modal').evaluate('e=>e.scrollWidth<=e.clientWidth+1'),f'{id} no horizontal overflow {width}')
    check(await page.locator('#'+id+' .modal').evaluate('e=>{const r=e.getBoundingClientRect();return r.x>=0&&r.right<=innerWidth+1&&r.y>=0&&r.bottom<=innerHeight+1}'),f'{id} contained {width}')
    await page.evaluate('closeModal()')
   if width==1100:await bake('workspace-compact-desktop')
  await page.set_viewport_size(dict(width=700,height=960));await page.evaluate('document.body.classList.add("browser-split")');check(await page.evaluate('document.documentElement.scrollWidth<=innerWidth'),'desktop split browser uses compact rail');await bake('workspace-browser-split','document.body.classList.add("browser-split")');await page.evaluate('document.body.classList.remove("browser-split")')
  # stale connected connection offers are not repeated in messages
  await page.set_viewport_size(dict(width=1440,height=960));chat=copy.deepcopy(f);chat['current']['messages']=[dict(id='reply',role='assistant',state='complete',text='Cloudflare подключён.',connectionOffer='cloudflare',work=[],artifacts=[])]
  await page.evaluate('(f)=>receive({type:"state",data:f})',chat);check(await page.locator('.connection-offer').count()==0,'connected Cloudflare does not show reconnect card');check(await page.locator('#msg-reply').is_visible(),'actual assistant answer retained')
  await page.evaluate('receive({type:"catalog",data:{models:[]}})');check(not await page.locator('#catalog-modal').is_visible(),'background catalogue update does not pop modal')
  # stream updates preserve reading position and batch full response DOM updates
  chat['current']['messages']=[dict(id='reply',role='assistant',state='streaming',text='Начало\\n'*300,work=[],artifacts=[])]
  await page.evaluate('(f)=>receive({type:"state",data:f})',chat);await page.evaluate('$("scroll").scrollTop=0;receive({type:"token",data:{id:"reply",text:"Обновлённый ответ\\n".repeat(350)}})');await page.wait_for_timeout(70);check(await page.locator('#scroll').evaluate('e=>e.scrollTop===0'),'stream does not yank reader to bottom');check('Обновлённый ответ' in await page.locator('#msg-reply .content').inner_text(),'stream text actually updated')
  await page.evaluate('(f)=>receive({type:"state",data:f})',f);await bake('workspace-final')
  check(not errors,'no runtime JS errors: '+str(errors));await b.close()
 (O/'ui-tests.json').write_text(json.dumps(dict(passed=len(checks),checks=checks),ensure_ascii=False,indent=2));print('UPDATE 3.13 UI PASS',len(checks))
asyncio.run(main())
