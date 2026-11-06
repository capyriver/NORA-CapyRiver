(() => {
  const page = {title: document.title, url: location.href, text: '', links: []};
  if (!document.body) {page.text = 'Страница ещё загружается'; return page;}
  const excluded = 'script,style,noscript,template,input,textarea,select,[contenteditable]:not([contenteditable="false"]),[hidden],[aria-hidden="true"]';
  function visible(element) {
    if (!element || element.closest(excluded)) return false;
    for (let el = element; el; el = el.parentElement) {
      const s = getComputedStyle(el);
      if (s.display === 'none' || s.visibility === 'hidden' || s.visibility === 'collapse' || s.contentVisibility === 'hidden' || Number(s.opacity) === 0) return false;
    }
    return true;
  }
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  const range = document.createRange(); const parts = []; let count = 0, length = 0;
  while (walker.nextNode() && count++ < 20000 && length < 8000) {
    const node = walker.currentNode, text = node.textContent.replace(/\s+/g, ' ').trim();
    if (!text || !visible(node.parentElement)) continue;
    range.selectNodeContents(node);
    if (!Array.from(range.getClientRects()).some(r => r.width > 0 && r.height > 0)) continue;
    parts.push(text); length += text.length + 1;
  }
  page.text = parts.join(' ').slice(0, 8000);
  for (const a of document.querySelectorAll('a[href]')) {
    if (page.links.length >= 40) break;
    if (!a.href.startsWith('https://') || !visible(a) || !Array.from(a.getClientRects()).some(r => r.width > 0 && r.height > 0)) continue;
    const text = a.innerText.trim(); if (text) page.links.push({text: text.slice(0,140), url: a.href});
  }
  return page;
})()
