// Run after physics-browser-check.js in its isolated Chrome session.
async(page)=>{
  const save=await page.evaluate(()=>{const s=JSON.parse(localStorage.getItem('chill-with-you-save-v1'));s.settings.lastVisit=0;return JSON.stringify(s);});
  const context=await page.context().browser().newContext({viewport:{width:844,height:390},deviceScaleFactor:2,isMobile:true,hasTouch:true});
  try{
    await context.addInitScript(save=>{if(location.origin==='http://127.0.0.1:8093')localStorage.setItem('chill-with-you-save-v1',save);},save);
    const mobile=await context.newPage();const errors=[];
    mobile.on('pageerror',e=>errors.push(e.message));mobile.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
    await mobile.goto('http://127.0.0.1:8093/');await mobile.waitForFunction(()=>!!window.chillUnityInstance,null,{timeout:90000});await mobile.waitForTimeout(6500);
    await mobile.waitForFunction(()=>window.ChillStartup?.phase==='done',null,{timeout:90000});
    await mobile.waitForFunction(()=>{window.chillUnityInstance.SendMessage('ChillPrototype','SaveFromBrowser');return JSON.parse(localStorage.getItem('chill-with-you-save-v1')).settings.lastVisit>Date.now()/1000-3;},null,{timeout:60000});
    const device=await mobile.evaluate(()=>({touchPoints:navigator.maxTouchPoints,coarsePointer:matchMedia('(pointer: coarse)').matches}));
    if(device.touchPoints<1||!device.coarsePointer)throw new Error('Mobile input emulation did not activate');
    await mobile.touchscreen.tap(410,230);
    for(const costume of [3,0]){await mobile.evaluate(i=>window.chillUnityInstance.SendMessage('ChillPrototype','ChooseCostume',i),costume);await mobile.waitForTimeout(1000);}
    await mobile.evaluate(()=>{window.chillUnityInstance.SendMessage('ChillPrototype','BrowserVisibility','hidden');});await mobile.waitForTimeout(600);
    await mobile.evaluate(()=>{window.chillUnityInstance.SendMessage('ChillPrototype','BrowserVisibility','visible');});await mobile.waitForTimeout(600);
    await mobile.screenshot({path:'output/playwright/physics-mobile-touch.png'});
    if(errors.length)throw new Error(JSON.stringify(errors));
    return {passed:true,browser:'Chrome mobile/touch emulation',viewport:[844,390],deviceScaleFactor:2,configuredFps:30,...device,errors:0,physicalDevice:false};
  }finally{await context.close();}
}
