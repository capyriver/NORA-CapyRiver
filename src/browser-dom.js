(() => {
  const key = __NORA_NONCE__;
  const excluded = '[hidden],[inert],[aria-hidden="true"]';
  const redactedControls = 'script,style,noscript,template,input,textarea,select,[contenteditable],[role="textbox"]';
  const redact = s => s.replace(/\b\d{6,12}:[A-Za-z0-9_-]{30,}\b/g,'[скрыт токен]').replace(/\b(?:ghp_|github_pat_)[A-Za-z0-9_]+\b/g,'[скрыт токен]');
  function styled(e) {
    if (!e || e.closest(excluded)) return false;
    for (let p=e; p; p=p.parentElement) {
      const s=getComputedStyle(p);
      if (s.visibility==='hidden'||s.visibility==='collapse'||s.display==='none'||s.contentVisibility==='hidden'||Number(s.opacity)===0) return false;
    }
    return true;
  }
  const inViewport = r => r.width>0&&r.height>0&&r.right>0&&r.left<innerWidth&&r.bottom>0&&r.top<innerHeight;
  const visible = e => styled(e)&&Array.from(e.getClientRects()).some(inViewport);
  const nodes=Array.from(document.querySelectorAll('a[href],button,input,textarea,select,[role="button"],[role="textbox"],[role="searchbox"],[contenteditable]:not([contenteditable="false"])')).filter(visible).slice(0,100);
  const elements=nodes.map((e,i)=>{
    e.setAttribute('data-nora-target',key+'-'+(i+1));
    const editable=e.isContentEditable;
    const label=redact((e.getAttribute('aria-label')||e.labels?.[0]?.innerText||e.getAttribute('placeholder')||e.getAttribute('data-placeholder')||(!e.matches('input,textarea,select,[contenteditable],[role="textbox"]')?e.innerText:'')||e.getAttribute('title')||e.getAttribute('name')||'').replace(/\s+/g,' ').slice(0,120));
    return {index:i+1,tag:editable?'contenteditable':e.tagName.toLowerCase(),type:e.getAttribute('type')||'',label,name:e.getAttribute('name')||'',autocomplete:e.getAttribute('autocomplete')||'',disabled:!!e.disabled||e.getAttribute('aria-disabled')==='true'||!!e.readOnly};
  });
  const texts=[];let total=0,visited=0;
  if(document.body){
    const walker=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT),range=document.createRange();
    while(walker.nextNode()&&total<3000&&visited++<20000){
      const n=walker.currentNode,p=n.parentElement;
      if(!p||p.closest(redactedControls)||!styled(p))continue;
      range.selectNodeContents(n);if(!Array.from(range.getClientRects()).some(inViewport))continue;
      const t=n.textContent.replace(/\s+/g,' ').trim();if(t){texts.push(t);total+=t.length;}
    }
  }
  return {url:location.href,title:document.title,text:redact(texts.join(' ').slice(0,3000)),elements,iframeCount:document.querySelectorAll('iframe').length};
})()
