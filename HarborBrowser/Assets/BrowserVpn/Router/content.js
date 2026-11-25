const port=Number(new URL(location.href).searchParams.get('port'));
const nonce=new URL(location.href).searchParams.get('nonce');
const type=new URL(location.href).searchParams.get('action')==='inspect'?'inspect':'configure';
chrome.runtime.sendMessage({type,port,nonce},result=>{
  const error=chrome.runtime.lastError;
  window.postMessage({harborTor:result||{Ok:false,Port:port,Nonce:nonce,Error:error?.message||'Routing worker unavailable.'}},location.origin);
});
