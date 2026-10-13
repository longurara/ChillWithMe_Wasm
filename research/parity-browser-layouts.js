// Isolated contexts cover native windows at three viewport sizes and three pixel densities.
async(page)=>{
 const browser=page.context().browser();
 const reference=await page.evaluate(async()=>({agent:navigator.userAgent,completed:(await(await fetch('/StreamingAssets/chill-story.json')).json()).groups.filter(g=>g.Scenario===1).map(g=>g.ID)}));
 const name=reference.agent.includes('Firefox')?'firefox':reference.agent.includes('Edg/')?'edge':'chrome';
 const panes=['notes','todos','habits','calendar','statistics','settings','profile','heroine-profile','special','decorations','environment','music','story','presets','timer-audio','achievements','history'];
 const cases=[],errors=[];
 for(const density of [1,1.25,1.5])for(const viewport of [{width:1280,height:720},{width:1920,height:1080},{width:2560,height:1080}]){
  const context=await browser.newContext({viewport,deviceScaleFactor:density});
  try{
   const target=await context.newPage();target.on('pageerror',e=>errors.push(e.message));target.on('console',m=>{if(m.text().includes('does not have receiver for function')||m.text().includes('The character with Unicode value')||(m.type()==='error'&&!m.text().startsWith('Applying workaround to Firefox bug https://bugzilla.mozilla.org/show_bug.cgi?id=1397977')))errors.push(m.text());});
   await target.addInitScript(completed=>localStorage.setItem('chill-with-you-save-v1',JSON.stringify({format:'chill-with-you-web',version:2,needsTutorial:false,previewUnlock:true,level:50,mainEpisode:36,completed,language:'En',workMinutes:25,breakMinutes:5,loopCount:2,settings:{autonomy:false,selfTalk:false,autoGreeting:false,mobileFit:true,quality:1,activeFps:30,inactiveFps:15,master:1,music:.35,voice:1,sfx:1,ambient:1,timer:1,renderScale:1,postProcessing:true,dayStart:6,sunsetStart:17,nightStart:19,ambience:[]}})),reference.completed);
   await target.goto('http://127.0.0.1:8093/',{waitUntil:'domcontentloaded',timeout:90000});
   await target.waitForFunction(()=>window.ChillStartup&&ChillStartup.phase==='done',null,{timeout:90000});
   for(const pane of panes){await target.evaluate(value=>window.chillUnityInstance.SendMessage('ChillPrototype','Open',value),pane);await target.waitForTimeout(300);await target.screenshot({path:'output/playwright/layout-'+name+'-'+viewport.width+'x'+viewport.height+'-'+density+'-'+pane+'.png'});await target.evaluate(()=>window.chillUnityInstance.SendMessage('ChillPrototype','Close'));}
   for(const tab of ['_graphicInteractableUI','_audioInteractableUI']){await target.evaluate(value=>{window.chillUnityInstance.SendMessage('ChillPrototype','Open','settings');window.chillUnityInstance.SendMessage('ChillPrototype','SettingsTab',value);},tab);await target.waitForTimeout(300);await target.screenshot({path:'output/playwright/layout-'+name+'-'+viewport.width+'x'+viewport.height+'-'+density+'-'+tab+'.png'});await target.evaluate(()=>window.chillUnityInstance.SendMessage('ChillPrototype','Close'));}
   for(const popup of ['birthday','ProfileIconSettingView','StatusChangeView']){await target.evaluate(value=>{const unity=window.chillUnityInstance;unity.SendMessage('ChillPrototype','Open','profile');if(value==='birthday')unity.SendMessage('ChillPrototype','OpenBirthdayDialog');else unity.SendMessage('ChillPrototype','ProfilePopup',value);},popup);await target.waitForTimeout(300);await target.screenshot({path:'output/playwright/layout-'+name+'-'+viewport.width+'x'+viewport.height+'-'+density+'-'+popup+'.png'});await target.evaluate(()=>window.chillUnityInstance.SendMessage('ChillPrototype','Close'));}
   cases.push({viewport,density,panes:panes.length+5});
  }finally{await context.close();}
 }
 if(errors.length)throw new Error(JSON.stringify(errors));
 return{passed:true,browser:reference.agent,cases,screenshots:cases.length*(panes.length+5),errors,scope:'Window handlers and rendered captures; visual review is required before accepting layout parity',userSaveWritten:false};
}
