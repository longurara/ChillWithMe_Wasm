// Run in an isolated playwright-cli browser after the checked build and finish_preview.py.
async(page)=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
 await page.setViewportSize({width:1280,height:720});
 await page.unroute('**/StreamingAssets/chill-story.json');
 let release,requested;const held=new Promise(resolve=>release=resolve),requestSeen=new Promise(resolve=>requested=resolve);
 await page.route('**/StreamingAssets/chill-story.json',async route=>{requested();await held;await route.continue();});
 await page.goto('http://127.0.0.1:8093/');
 await page.waitForFunction(()=>!!window.chillUnityInstance,null,{timeout:90000});
 await Promise.race([requestSeen,page.waitForTimeout(30000).then(()=>{throw new Error('Story request did not reach interception');})]);await page.waitForTimeout(2500);
 if(await page.evaluate(()=>ChillStartup.phase)!=='loading')throw new Error('Room was revealed before story data');
 await page.screenshot({path:'output/playwright/23-real-waiting-story.png'});release();
 await page.waitForFunction(()=>ChillStartup.phase==='done',null,{timeout:90000});await page.waitForTimeout(1400);
 await page.evaluate(()=>window.chillUnityInstance.SendMessage('ChillPrototype','StopStory'));
 await page.screenshot({path:'output/playwright/23-real-ready-room.png'});
 const state=await page.evaluate(()=>({phase:ChillStartup.phase,hidden:document.getElementById('unity-loading-bar').hidden,inert:document.getElementById('unity-canvas').inert,version:document.documentElement.innerHTML.match(/productVersion:'([^']+)'/)[1]}));
 if(!state.hidden||state.inert||state.version!=='1.23.0')throw new Error(JSON.stringify(state));
 await page.mouse.click(1236,615,{delay:180});await page.waitForTimeout(600);await page.screenshot({path:'output/playwright/23-real-settings-after-startup.png'});
 await page.unroute('**/StreamingAssets/chill-story.json');
 if(errors.length)throw new Error(JSON.stringify(errors));
 return{passed:true,heldStoryUntilEngineReady:true,sourceFinishFadeReachesRoom:true,roomInputEnabled:true,state,errors};
}
