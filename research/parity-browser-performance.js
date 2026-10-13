// Capture Unity frame intervals, not only independent browser RAF callbacks.
async(page)=>{
 await page.setViewportSize({width:1920,height:1080});
 await page.goto('http://127.0.0.1:8093/',{waitUntil:'domcontentloaded',timeout:90000});
 await page.waitForFunction(()=>window.ChillStartup&&ChillStartup.phase==='done',null,{timeout:90000});
 await page.evaluate(async()=>{const unity=window.chillUnityInstance,story=await(await fetch('/StreamingAssets/chill-story.json')).json();unity.SendMessage('ChillPrototype','ImportSave',JSON.stringify({format:'chill-with-you-web',version:2,needsTutorial:false,previewUnlock:true,level:50,mainEpisode:36,completed:story.groups.filter(g=>g.Scenario===1).map(g=>g.ID),language:'En',workMinutes:25,breakMinutes:5,loopCount:2,settings:{autonomy:true,selfTalk:false,autoGreeting:false,mobileFit:true,quality:2,activeFps:60,inactiveFps:15,master:1,music:.35,voice:1,sfx:1,ambient:1,timer:1,renderScale:1,postProcessing:true,dayStart:6,sunsetStart:17,nightStart:19,ambience:[]}}));unity.SendMessage('ChillPrototype','BrowserVisibility','visible');});
 await page.waitForTimeout(4000);
 await page.evaluate(()=>{window.ChillPerformanceReport=null;window.chillUnityInstance.SendMessage('ChillPrototype','CapturePerformance','15');});
 await page.waitForFunction(()=>window.ChillPerformanceReport,null,{timeout:30000});
 const result=await page.evaluate(()=>{const gl=document.getElementById('unity-canvas').getContext('webgl2'),info=gl.getExtension('WEBGL_debug_renderer_info');return{unity:window.ChillPerformanceReport,browser:navigator.userAgent,gpu:info?gl.getParameter(info.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER),wasmHeapBytes:window.chillUnityInstance.Module?.HEAPU8?.byteLength??null};});
 const frameCapRespected=result.unity.framesPerSecond<=result.unity.targetFps*1.1;
 const frameBudgetMet=result.unity.framesPerSecond>=result.unity.targetFps*.8&&result.unity.p95FrameIntervalMs<=2500/result.unity.targetFps;
 return{passed:result.unity.frames>0&&frameCapRespected&&frameBudgetMet,...result,frameCapRespected,frameBudgetMet,scope:'Actual Unity frame intervals and reported hardware; GPU render duration is not measured'};
}
