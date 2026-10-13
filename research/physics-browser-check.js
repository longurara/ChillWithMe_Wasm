// Run with playwright-cli run-code --filename research/physics-browser-check.js
// Use an isolated browser session; this intentionally changes its test save.
async (page) => {
  const messages = [], failures = [];
  page.on('pageerror', e => failures.push(e.message));
  page.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') messages.push({type:m.type(),text:m.text()}); });
  await page.setViewportSize({width:1280,height:720});
  await page.goto('http://127.0.0.1:8093/ui/native.css');
  await page.evaluate(() => {
    const key='chill-with-you-save-v1';const s=JSON.parse(localStorage.getItem(key));
    if(!s)throw new Error('Prepare an isolated game save before running this check');
    s.chapter='';s.cursor=0;s.sceneHistory=[];s.previewUnlock=true;s.autoRead=false;
    Object.assign(s.settings,{autonomy:false,resting:false,autoGreeting:false,selfTalk:false,cameraView:0,activeFps:60});
    s.deskMode=0;s.timerRunning=false;s.pausedRemaining=0;s.countupRunning=false;s.countupActive=false;
    s.workspace.costume=0;s.workspace.hair=0;localStorage.setItem(key,JSON.stringify(s));
  });
  await page.goto('http://127.0.0.1:8093/');
  await page.waitForFunction(() => !!window.chillUnityInstance, null, {timeout:90000});
  await page.waitForTimeout(6500);
  if(!await page.evaluate(()=>document.documentElement.innerHTML.includes("productVersion:'1.21.0'")))throw new Error('Wrong player version');
  const send=async(method,value)=>page.evaluate(({method,value})=>window.chillUnityInstance.SendMessage('ChillPrototype',method,...(value===undefined?[]:[value])),{method,value});
  for(const costume of [1,2,3,4,0]){
    await send('ChooseCostume',costume);await page.waitForTimeout(1000);
    await send('SaveFromBrowser');
    const actual=await page.evaluate(()=>JSON.parse(localStorage.getItem('chill-with-you-save-v1')).workspace.costume);
    if(actual!==costume)throw new Error('Costume did not apply: '+costume);
    await page.screenshot({path:'output/playwright/physics-costume-'+costume+'.png'});
  }
  await send('BrowserVisibility','hidden');await page.waitForTimeout(1200);
  await send('BrowserVisibility','visible');await page.waitForTimeout(1200);
  await page.screenshot({path:'output/playwright/physics-resume.png'});
  await page.goto('http://127.0.0.1:8093/ui/native.css');
  await page.evaluate(()=>{const k='chill-with-you-save-v1',s=JSON.parse(localStorage.getItem(k));s.settings.activeFps=30;localStorage.setItem(k,JSON.stringify(s));});
  await page.setViewportSize({width:844,height:390});await page.goto('http://127.0.0.1:8093/');
  await page.waitForFunction(()=>!!window.chillUnityInstance,null,{timeout:90000});await page.waitForTimeout(6500);
  await send('ChooseCostume',3);await page.waitForTimeout(1200);await send('ChooseCostume',0);await page.waitForTimeout(1200);
  await page.screenshot({path:'output/playwright/physics-landscape-30.png'});
  await send('SaveFromBrowser');
  const save=await page.evaluate(()=>JSON.parse(localStorage.getItem('chill-with-you-save-v1')));
  if(save.settings.activeFps!==30||save.workspace.costume!==0)throw new Error('30 FPS fixture not restored');
  const errors=messages.filter(m=>m.type==='error');
  if(failures.length||errors.length)throw new Error(JSON.stringify({failures,errors}));
  return {passed:true,version:'1.21.0',outfits:5,desktopFps:60,landscapeFps:30,visibilityRoundTrip:true,pageErrors:failures.length,consoleErrors:errors.length,warnings:messages.filter(m=>m.type==='warning').length,mobileScope:'Chrome resized viewport; no physical device or Safari claim'};
}
