import asyncio,json,re,pathlib,shutil
from playwright.async_api import async_playwright
R=pathlib.Path(__file__).resolve().parent.parent
html=re.search(r'internal const string StartPage="""\n(.*?)\n""";', (R/'src/BrowserWindow.cs').read_text(),re.S).group(1)
async def main():
 checks=[]
 async with async_playwright() as p:
  b=await p.chromium.launch(executable_path=shutil.which('chromium'),headless=True,args=['--no-sandbox']);page=await b.new_page(viewport={'width':640,'height':720});requests=[]
  page.on('request',lambda r:requests.append(r.url));await page.set_content(html);await page.wait_for_timeout(300)
  assert len(requests)==0;checks.append('local start page makes zero network requests')
  assert await page.locator('a[href="https://web.telegram.org/k/"]').count()==1;checks.append('Telegram link points directly to Telegram Web')
  assert await page.locator('h1').inner_text()=='Браузер NORA';checks.append('offline browser welcome page renders')
  assert await page.evaluate('document.documentElement.scrollWidth===innerWidth');checks.append('start page fits a narrow dock')
  await page.screenshot(path=str(R/'verification-3.7.1/browser-start.png'));await b.close()
 (R/'verification-3.7.1/start-page-ui.json').write_text(json.dumps({'passed':len(checks),'checks':checks},indent=2));print(str(len(checks))+' start page UI checks passed')
asyncio.run(main())
