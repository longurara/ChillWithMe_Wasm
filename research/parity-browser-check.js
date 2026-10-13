// Run via playwright-cli run-code in an isolated browser after finish_preview.py.
async(page)=>{
 const errors=[],engineMessages=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error'){if(m.text().startsWith('Applying workaround to Firefox bug https://bugzilla.mozilla.org/show_bug.cgi?id=1397977'))engineMessages.push(m.text());else errors.push(m.text());}});
 await page.setViewportSize({width:1280,height:720});
 await page.goto('http://127.0.0.1:8093/');
 await page.waitForFunction(()=>window.ChillStartup&&ChillStartup.phase==='done',null,{timeout:90000});
 await page.screenshot({path:'output/playwright/24-initial-room.png'});
 const state=await page.evaluate(()=>({phase:ChillStartup.phase,inert:document.getElementById('unity-canvas').inert,version:document.documentElement.innerHTML.match(/productVersion:'([^']+)'/)[1],browser:navigator.userAgent}));
 if(state.inert||state.version!=='1.24.0')throw new Error(JSON.stringify(state));
 const pc=await page.evaluate(async()=>{const m=await import('/ui/pc-save.mjs');return m.parseES3('{42:639270970027740616}')['42'];});
 if(pc!=='639270970027740616')throw new Error('PC save precision lost');
 await page.evaluate(()=>window.chillUnityInstance.SendMessage('ChillPrototype','StopStory'));
 await page.evaluate(()=>ChillStartup.play('credits-finish'));
 if(!await page.evaluate(()=>document.getElementById('unity-canvas').inert))throw new Error('Credits cover did not block room input');
 await page.evaluate(()=>ChillStartup.play('fade'));
 if(await page.evaluate(()=>document.getElementById('unity-canvas').inert))throw new Error('Credits fade did not release input');
 await page.evaluate(()=>{ChillStartup.play('credits-finish');});
 await page.waitForTimeout(350);await page.evaluate(()=>ChillStartup.cancelDirection());
 if(await page.evaluate(()=>document.getElementById('unity-canvas').inert))throw new Error('Cancelled direction blocks room');
 await page.evaluate(()=>ChillStartup.play('gameEnd'));
 await page.getByRole('button',{name:'Return to room'}).click();
 await page.waitForFunction(()=>ChillStartup.phase==='done');
 await page.screenshot({path:'output/playwright/24-directions-returned.png'});
 if(errors.length)throw new Error(JSON.stringify(errors));
 return{passed:true,scope:'Browser startup/directions/parser smoke; not all gameplay or PC pixel parity',state,exact64BitIds:true,creditsAndFade:true,cancelDirection:true,farewellReturn:true,errors,engineMessages};
}
