const set=(setting,value)=>new Promise((resolve,reject)=>setting.set({value,scope:'regular'},()=>{const error=chrome.runtime.lastError;error?reject(new Error(error.message)):resolve();}));
chrome.runtime.onMessage.addListener((message,sender,reply)=>{
  if(!sender.url?.startsWith('https://harbor-tor-control.invalid/')||message.type!=='configure'||!Number.isInteger(message.port)||message.port<1024||message.port>65535)return;
  (async()=>{
    await set(chrome.proxy.settings,{mode:'fixed_servers',rules:{singleProxy:{scheme:'socks5',host:'127.0.0.1',port:message.port},bypassList:['<-loopback>']}});
    await set(chrome.privacy.network.webRTCIPHandlingPolicy,'disable_non_proxied_udp');
    await set(chrome.privacy.network.networkPredictionEnabled,false);
    const state=await new Promise(resolve=>chrome.proxy.settings.get({},resolve));
    if(state.levelOfControl!=='controlled_by_this_extension'||state.value?.rules?.singleProxy?.port!==message.port)throw new Error('Harbor could not verify its proxy settings.');
    reply({Ok:true,Port:message.port});
  })().catch(error=>reply({Ok:false,Port:message.port,Error:error.message}));
  return true;
});
