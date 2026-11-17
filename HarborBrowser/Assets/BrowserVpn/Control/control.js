window.addEventListener('message',event=>{if(event.source===window&&event.origin===location.origin&&event.data?.harborTor)chrome.webview.postMessage(event.data.harborTor);});
