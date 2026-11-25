const port=Number(new URL(location.href).searchParams.get('port'));
chrome.runtime.sendMessage({type:'configure',port},result=>{
  const error=chrome.runtime.lastError;
  window.postMessage({harborTor:result||{Ok:false,Port:port,Error:error?.message||'Routing worker unavailable.'}},location.origin);
});
