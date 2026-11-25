const set=(setting,value)=>new Promise((resolve,reject)=>setting.set({value,scope:'regular'},()=>{const error=chrome.runtime.lastError;error?reject(new Error(error.message)):resolve();}));
const get=setting=>new Promise((resolve,reject)=>setting.get({},value=>{const error=chrome.runtime.lastError;error?reject(new Error(error.message)):resolve(value);}));
chrome.runtime.onMessage.addListener((message,sender,reply)=>{
  if(!sender.url?.startsWith('https://harbor-tor-control.invalid/')||!['configure','inspect'].includes(message.type)||!Number.isInteger(message.port)||message.port<1024||message.port>65535||!/^[a-f0-9]{32}$/.test(message.nonce||''))return;
  (async()=>{
    if(message.type==='configure'){
      await set(chrome.proxy.settings,{mode:'fixed_servers',rules:{singleProxy:{scheme:'socks5',host:'127.0.0.1',port:message.port},bypassList:['<-loopback>']}});
      await set(chrome.privacy.network.webRTCIPHandlingPolicy,'disable_non_proxied_udp');
      await set(chrome.privacy.network.networkPredictionEnabled,false);
    }
    const [state,rtc,prediction]=await Promise.all([get(chrome.proxy.settings),get(chrome.privacy.network.webRTCIPHandlingPolicy),get(chrome.privacy.network.networkPredictionEnabled)]);
    const proxy=state.value?.rules?.singleProxy;
    if(state.levelOfControl!=='controlled_by_this_extension'||state.value?.mode!=='fixed_servers'||proxy?.scheme!=='socks5'||proxy?.host!=='127.0.0.1'||proxy?.port!==message.port||JSON.stringify(state.value?.rules?.bypassList)!==JSON.stringify(['<-loopback>']))throw new Error('Harbor could not verify its proxy settings.');
    if(rtc.value!=='disable_non_proxied_udp'||prediction.value!==false)throw new Error('Browser privacy settings are not applied.');
    reply({Ok:true,Port:message.port,Nonce:message.nonce,WebRtcPolicy:rtc.value,NetworkPrediction:prediction.value});
  })().catch(error=>reply({Ok:false,Port:message.port,Nonce:message.nonce,Error:error.message}));
  return true;
});
