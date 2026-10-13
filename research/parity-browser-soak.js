// Install a two-hour, real-clock run in an isolated profile; poll ChillSoak separately.
async(page)=>{
 await page.setViewportSize({width:1920,height:1080});await page.goto('http://127.0.0.1:8093/',{waitUntil:'domcontentloaded',timeout:90000});
 await page.waitForFunction(()=>window.ChillStartup&&ChillStartup.phase==='done',null,{timeout:90000});
 const errors=[];page.on('pageerror',error=>{if(errors.length<20)errors.push(error.message);});page.on('console',message=>{if(message.type()==='error'&&!message.text().startsWith('Applying workaround to Firefox bug https://bugzilla.mozilla.org/show_bug.cgi?id=1397977')&&errors.length<20)errors.push(message.text());});
 await page.evaluate(async()=>{
  const story=await(await fetch('/StreamingAssets/chill-story.json')).json(),unity=window.chillUnityInstance;
  unity.SendMessage('ChillPrototype','ImportSave',JSON.stringify({format:'chill-with-you-web',version:2,needsTutorial:false,previewUnlock:true,level:50,mainEpisode:36,completed:story.groups.filter(g=>g.Scenario===1).map(g=>g.ID),language:'En',workMinutes:1,breakMinutes:1,loopCount:99,settings:{autonomy:true,selfTalk:true,autoGreeting:false,autoTime:true,mobileFit:true,quality:2,activeFps:60,inactiveFps:15,master:1,music:.35,voice:1,sfx:1,ambient:1,timer:1,renderScale:1,postProcessing:true,dayStart:6,sunsetStart:17,nightStart:19,ambience:[]}}));
  unity.SendMessage('ChillPrototype','BrowserVisibility','visible');unity.SendMessage('ChillPrototype','UICommand',JSON.stringify({action:'timer-toggle'}));
  const state=window.ChillSoak={started:Date.now(),durationMs:7200000,samples:[],errors:[],done:false,invalidSaves:0,browser:navigator.userAgent};
  const reportError=value=>{if(state.errors.length<20)state.errors.push(String(value).slice(0,2000));};
  addEventListener('error',event=>reportError(event.message));addEventListener('unhandledrejection',event=>reportError(event.reason));
  const originalError=console.error;console.error=(...values)=>{if(!String(values[0]).startsWith('Applying workaround to Firefox bug https://bugzilla.mozilla.org/show_bug.cgi?id=1397977'))reportError(values.join(' '));originalError.apply(console,values);};
  function sample(){
   let save;try{save=JSON.parse(localStorage.getItem('chill-with-you-save-v1'));if(save.format!=='chill-with-you-web'||save.version!==2)throw Error();}catch{state.invalidSaves++;}
   state.samples.push({at:Date.now(),rounds:save?.rounds,breakTime:save?.breakTime,running:save?.timerRunning,workedSeconds:save?.workedSeconds,saveBytes:(localStorage.getItem('chill-with-you-save-v1')||'').length,wasmHeapBytes:unity.Module?.HEAPU8?.byteLength??null,jsHeapBytes:performance.memory?.usedJSHeapSize??null,unity:window.ChillPerformanceReport??null});
   unity.SendMessage('ChillPrototype','CapturePerformance','3');
   if(Date.now()-state.started>=state.durationMs){state.done=true;clearInterval(state.interval);unity.SendMessage('ChillPrototype','UICommand',JSON.stringify({action:'timer-reset'}));}
  }
  sample();state.interval=setInterval(sample,60000);
 });
 return{installed:true,durationMs:7200000,browser:await page.evaluate(()=>navigator.userAgent),userSaveWritten:false,scope:'Poll ChillSoak and browser errors; installation does not establish a successful soak'};
}
