'use strict';
(() => {
  const bridge = window.chrome && window.chrome.webview;
  let privateTab = !!bridge;
  let searchTemplate = 'https://duckduckgo.com/?q={0}';
  let selectedTheme;
  let appAppearance = null;
  let homeScene='Auto', customBackground=null;
  let suggestionId=0, suggestionIndex=-1, suggestionItems=[];
  const queryInput=document.getElementById('query');
  const suggestionList=document.getElementById('home-suggestions');
  try { selectedTheme = localStorage.getItem('harbor.home.theme'); } catch (_) {}
  if (!['light','dark'].includes(selectedTheme))
    selectedTheme = matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';

  function theme(value, persist) {
    document.body.dataset.theme = value;
    if (appAppearance && appAppearance[value]) {
      const palette = appAppearance[value];
      for (const key of ['ink','muted','surface','soft','accent','accentText','background','line','selection']) {
        const css = key === 'accentText' ? 'accent-text' : key;
        if (/^#[0-9a-f]{6}(?:[0-9a-f]{2})?$/i.test(palette[key] || '')) document.body.style.setProperty('--' + css, palette[key]);
      }
    }
    document.querySelectorAll('[data-theme]').forEach(button => {
      if (button.tagName === 'BUTTON') button.setAttribute('aria-pressed', String(button.dataset.theme === value));
    });
    updateBackground();
    if (persist && !privateTab) { try { localStorage.setItem('harbor.home.theme', value); } catch (_) {} if (bridge) bridge.postMessage({type:'home-theme', value:value === 'dark' ? 'Dark' : 'Light'}); }
  }
  function updateBackground() {
    const dark=document.body.dataset.theme==='dark';
    if(homeScene==='Custom'&&customBackground) {
      document.body.style.backgroundImage='url("'+customBackground+'")';document.body.dataset.customBackground='true';document.getElementById('scene-name').textContent='Your background';
    } else {
      delete document.body.dataset.customBackground;
      const blue=homeScene==='BlueHour'||(homeScene!=='Morning'&&dark);
      document.body.style.backgroundImage='url("'+(blue?'blue-hour.png':'morning.png')+'")';document.getElementById('scene-name').textContent=blue?'Harbor at blue hour':'Morning harbor';
    }
  }
  theme(selectedTheme, false);
  document.querySelectorAll('button[data-theme]').forEach(button =>
    button.addEventListener('click', () => theme(button.dataset.theme, true)));
  document.getElementById('date').textContent = new Intl.DateTimeFormat(undefined, {weekday:'long', month:'long', day:'numeric'}).format(new Date());

  function applyState(state) {
    if (!state || state.type !== 'home-state') return;
    if (typeof state.version === 'string' && /^\d+\.\d+\.\d+$/.test(state.version)) document.getElementById('app-version').textContent = 'Harbor ' + state.version;
    privateTab = state.isPrivate === true;
    homeScene=['Auto','Morning','BlueHour','Custom'].includes(state.scene)?state.scene:'Auto';
    customBackground=typeof state.backgroundData==='string'&&/^data:image\/png;base64,[a-z0-9+/=]+$/i.test(state.backgroundData)?state.backgroundData:null;
    document.getElementById('customize-home').hidden=privateTab||!bridge;
    if (state.appearance && ['light','dark'].includes(state.appearance.mode)) { appAppearance = state.appearance; theme(appAppearance.mode, false); }
    document.querySelector('.private-note').hidden = !privateTab;
    updateBackground();
    document.getElementById('engine').textContent = state.engineName || 'DuckDuckGo';
    if (typeof state.searchTemplate === 'string' && /^https:\/\//i.test(state.searchTemplate))
      searchTemplate = state.searchTemplate;
    const favorites = document.getElementById('favorites');
    favorites.replaceChildren();
    (Array.isArray(state.bookmarks) ? state.bookmarks : []).slice(0,12).forEach(bookmark => {
      try {
        const url = new URL(bookmark.url);
        if (!['https:', 'http:'].includes(url.protocol)||url.username||url.password) return;
        const link = document.createElement('a');
        link.className = 'favorite'; link.href = url.href;
        const label = bookmark.title || url.hostname;
        link.title = label + ' — ' + url.hostname;
        const initial = document.createElement('span'); initial.className = 'initial';
        initial.textContent = label.slice(0,1).toUpperCase();
        const text = document.createElement('span'); text.textContent = label;
        link.append(initial, text); favorites.append(link);
      } catch (_) {}
    });
    document.getElementById('empty-state').hidden = favorites.children.length > 0;
    document.querySelector('#empty-state p').textContent=state.customShortcuts?'Choose Customize to add shortcuts or use your bookmarks.':'Bookmark a site with the star in the address bar to keep it here.';
  }
  if (bridge) {
    bridge.addEventListener('message', event => {if(event.data&&event.data.type==='home-suggestions')applySuggestions(event.data);else applyState(event.data);});
    bridge.postMessage({type:'home-ready'});
  }
  document.getElementById('customize-home').addEventListener('click',()=>{if(bridge&&!privateTab)bridge.postMessage({type:'home-customize'});});
  function dismissSuggestions() {
    suggestionId++;suggestionIndex=-1;suggestionItems=[];suggestionList.replaceChildren();suggestionList.hidden=true;queryInput.setAttribute('aria-expanded','false');queryInput.removeAttribute('aria-activedescendant');
    if(bridge)bridge.postMessage({type:'home-suggest',id:suggestionId,value:''});
  }
  function applySuggestions(state) {
    if(state.id!==suggestionId||state.value!==queryInput.value.trim()||document.activeElement!==queryInput)return;
    suggestionItems=[];suggestionIndex=-1;suggestionList.replaceChildren();
    (Array.isArray(state.items)?state.items:[]).slice(0,8).forEach(item=>{
      if(!item||typeof item.title!=='string')return;
      if(item.url){try{const url=new URL(item.url);if(!['http:','https:'].includes(url.protocol)||url.username||url.password)return;}catch(_){return;}}
      else if(typeof item.query!=='string'||item.query.length>240)return;
      const index=suggestionItems.length;suggestionItems.push(item);
      const row=document.createElement('li'),button=document.createElement('button');button.type='button';button.id='home-option-'+index;button.setAttribute('role','option');button.setAttribute('aria-selected','false');
      const label=document.createElement('span');label.textContent=item.title;const source=document.createElement('small');source.textContent=item.source||'Search suggestion';button.append(label,source);button.addEventListener('click',()=>chooseSuggestion(item));row.append(button);suggestionList.append(row);
    });
    suggestionList.hidden=suggestionItems.length===0;queryInput.setAttribute('aria-expanded',String(suggestionItems.length>0));
    const bounds=document.getElementById('search').getBoundingClientRect(),below=innerHeight-bounds.bottom;
    const above=below<160&&bounds.top>130;suggestionList.classList.toggle('opens-up',above);suggestionList.style.maxHeight=Math.max(80,Math.min(270,(above?bounds.top:below)-14))+'px';
  }
  function chooseSuggestion(item) {
    const value=item.url||item.query;queryInput.value=item.query||item.title;dismissSuggestions();
    if(bridge)bridge.postMessage({type:'home-search',value,searchOnly:!item.url});else window.location.assign(item.url||searchTemplate.replace('{0}',encodeURIComponent(item.query)));
  }
  queryInput.addEventListener('input',()=>{
    suggestionId++;suggestionIndex=-1;suggestionItems=[];suggestionList.hidden=true;queryInput.setAttribute('aria-expanded','false');
    const value=queryInput.value.trim();if(bridge)bridge.postMessage({type:'home-suggest',id:suggestionId,value:value.length<=160?value:''});
  });
  queryInput.addEventListener('keydown',event=>{
    if(event.key==='Escape'){dismissSuggestions();event.preventDefault();return;}
    if(!suggestionItems.length||suggestionList.hidden)return;
    if(event.key==='ArrowDown'||event.key==='ArrowUp') {
      event.preventDefault();suggestionIndex=event.key==='ArrowDown'?(suggestionIndex+1)%suggestionItems.length:(suggestionIndex<=0?suggestionItems.length-1:suggestionIndex-1);
      suggestionList.querySelectorAll('button').forEach((button,index)=>button.setAttribute('aria-selected',String(index===suggestionIndex)));queryInput.setAttribute('aria-activedescendant','home-option-'+suggestionIndex);
      const active=document.getElementById('home-option-'+suggestionIndex);if(active.offsetTop<suggestionList.scrollTop)suggestionList.scrollTop=active.offsetTop;else if(active.offsetTop+active.offsetHeight>suggestionList.scrollTop+suggestionList.clientHeight)suggestionList.scrollTop=active.offsetTop+active.offsetHeight-suggestionList.clientHeight;
    } else if(event.key==='Enter'&&suggestionIndex>=0){event.preventDefault();chooseSuggestion(suggestionItems[suggestionIndex]);}
  });
  document.addEventListener('pointerdown',event=>{if(!document.getElementById('search').contains(event.target))dismissSuggestions();});
  document.getElementById('search').addEventListener('submit', event => {
    event.preventDefault();
    const value = document.getElementById('query').value.trim();
    if (!value) return;
    dismissSuggestions();
    if (bridge) { bridge.postMessage({type:'home-search', value}); return; }
    // Standalone preview also supports search; the installed app resolves addresses itself.
    window.location.assign(searchTemplate.replace('{0}', encodeURIComponent(value)));
  });
})();
