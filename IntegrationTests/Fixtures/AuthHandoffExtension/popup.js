window.harborPopupProof={ready:true,returnedWindow:false,roundTrip:false};
window.harborNativePrepared=false;window.harborNativeRead=false;
window.prepareNativeCanary=canary=>chrome.storage.local.set({harborSyntheticPendingCanary:canary},()=>{window.harborNativePrepared=!chrome.runtime.lastError;});
window.readNativeCanary=()=>chrome.storage.local.get('harborSyntheticPendingCanary',value=>{window.harborNativeValue=value.harborSyntheticPendingCanary||null;window.harborNativeRead=!chrome.runtime.lastError;});
window.beginPopupProof=(target,name,canary)=>{sessionStorage.setItem('harbor-popup-canary',canary);window.harborPopupProof={ready:true,target,name,canary,returnedWindow:false,roundTrip:false};window.harborChildWindow=window.open(target,name);harborPopupProof.returnedWindow=!!harborChildWindow;};
addEventListener('message',e=>{const p=window.harborPopupProof;if(!e.data||e.data.kind!=='harbor-child-proof'||e.data.canary!==p.canary||e.source!==window.harborChildWindow)return;p.roundTrip=true;p.childName=e.data.name;e.source.postMessage({kind:'harbor-parent-confirm',canary:p.canary},'*');});
