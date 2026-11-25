(() => {
  if(window.__harborMediaController)return;
  const channel='__HARBOR_CHANNEL__';
  let saved=[],style=null,active=null,originalControls=false;
  function remember(el){if(saved.some(x=>x[0]===el))return;saved.push([el,el.getAttribute('style')]);}
  function focus(el){
    if(!style){style=document.createElement('style');style.textContent='html,body{margin:0!important;padding:0!important;width:100%!important;height:100%!important;overflow:hidden!important;background:#000!important}body *{visibility:hidden!important}.harbor-pip-focus{visibility:visible!important;position:fixed!important;inset:0!important;width:100vw!important;height:100vh!important;max-width:none!important;max-height:none!important;z-index:2147483647!important;object-fit:contain!important;background:#000!important;border:0!important}';(document.head||document.documentElement).append(style);}
    remember(el);el.classList.add('harbor-pip-focus');
    let parent=el.parentElement;
    while(parent){remember(parent);for(const prop of ['transform','filter','perspective','contain','clip-path'])parent.style.setProperty(prop,prop==='contain'?'none':'none','important');parent.style.setProperty('overflow',parent===document.body||parent===document.documentElement?'hidden':'visible','important');parent=parent.parentElement;}
    if(window.parent!==window)window.parent.postMessage({channel,type:'focus-frame'},'*');
  }
  function candidates(){return Array.from(document.querySelectorAll('video')).filter(v=>{const r=v.getBoundingClientRect();return v.readyState>=1&&r.width>20&&r.height>20;}).sort((a,b)=>score(b)-score(a));}
  function score(v){const r=v.getBoundingClientRect();return Math.round(r.width*r.height)+(!v.paused?100000000:0);}
  function probe(){const v=candidates()[0];return v?{found:true,score:score(v),width:v.videoWidth,height:v.videoHeight}:{found:false,score:0};}
  function enter(){const v=candidates()[0];if(!v)return {ok:false};active=v;originalControls=v.controls;v.controls=true;focus(v);return {ok:true};}
  function exit(){if(active)active.controls=originalControls;for(const [el,css] of saved){el.classList.remove('harbor-pip-focus');if(css===null)el.removeAttribute('style');else el.setAttribute('style',css);}saved=[];active=null;if(style){style.remove();style=null;}return true;}
  window.addEventListener('message',e=>{if(!e.data||e.data.channel!==channel||e.data.type!=='focus-frame')return;const frame=Array.from(document.querySelectorAll('iframe,frame')).find(f=>f.contentWindow===e.source);if(frame)focus(frame);});
  window.__harborMediaController={probe,enter,exit,togglePlayback:()=>{if(!active)return false;if(active.paused)active.play().catch(()=>{});else active.pause();return true;},toggleMute:()=>{if(!active)return false;active.muted=!active.muted;return true;}};
})();
