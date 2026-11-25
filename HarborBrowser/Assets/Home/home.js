'use strict';
(() => {
  const bridge = window.chrome && window.chrome.webview;
  let privateTab = !!bridge;
  let searchTemplate = 'https://duckduckgo.com/?q={0}';
  let selectedTheme;
  let appAppearance = null;
  try { selectedTheme = localStorage.getItem('harbor.home.theme'); } catch (_) {}
  if (!['light','dark'].includes(selectedTheme))
    selectedTheme = matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';

  function theme(value, persist) {
    document.body.dataset.theme = value;
    if (appAppearance && appAppearance[value]) {
      const palette = appAppearance[value];
      for (const key of ['ink','muted','surface','soft','accent','accentText','background','line']) {
        const css = key === 'accentText' ? 'accent-text' : key;
        if (/^#[0-9a-f]{6}$/i.test(palette[key] || '')) document.body.style.setProperty('--' + css, palette[key]);
      }
    }
    document.querySelectorAll('[data-theme]').forEach(button => {
      if (button.tagName === 'BUTTON') button.setAttribute('aria-pressed', String(button.dataset.theme === value));
    });
    document.getElementById('scene-name').textContent = value === 'dark' ? 'Harbor at blue hour' : 'Morning harbor';
    if (persist && !privateTab) { try { localStorage.setItem('harbor.home.theme', value); } catch (_) {} if (bridge) bridge.postMessage({type:'home-theme', value:value === 'dark' ? 'Dark' : 'Light'}); }
  }
  theme(selectedTheme, false);
  document.querySelectorAll('button[data-theme]').forEach(button =>
    button.addEventListener('click', () => theme(button.dataset.theme, true)));
  document.getElementById('date').textContent = new Intl.DateTimeFormat(undefined, {weekday:'long', month:'long', day:'numeric'}).format(new Date());

  function applyState(state) {
    if (!state || state.type !== 'home-state') return;
    privateTab = state.isPrivate === true;
    if (state.appearance && ['light','dark'].includes(state.appearance.mode)) { appAppearance = state.appearance; theme(appAppearance.mode, false); }
    document.querySelector('.private-note').hidden = !privateTab;
    document.getElementById('engine').textContent = state.engineName || 'DuckDuckGo';
    if (typeof state.searchTemplate === 'string' && /^https:\/\//i.test(state.searchTemplate))
      searchTemplate = state.searchTemplate;
    const favorites = document.getElementById('favorites');
    favorites.replaceChildren();
    (Array.isArray(state.bookmarks) ? state.bookmarks : []).slice(0,8).forEach(bookmark => {
      try {
        const url = new URL(bookmark.url);
        if (!['https:', 'http:'].includes(url.protocol)) return;
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
  }
  if (bridge) {
    bridge.addEventListener('message', event => applyState(event.data));
    bridge.postMessage({type:'home-ready'});
  }
  document.getElementById('search').addEventListener('submit', event => {
    event.preventDefault();
    const value = document.getElementById('query').value.trim();
    if (!value) return;
    if (bridge) { bridge.postMessage({type:'home-search', value}); return; }
    // Standalone preview also supports search; the installed app resolves addresses itself.
    window.location.assign(searchTemplate.replace('{0}', encodeURIComponent(value)));
  });
})();
