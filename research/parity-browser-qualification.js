// CLI verification in an isolated desktop browser; result includes the actual GPU and browser.
async(page)=>{
 const errors=[],engineMessages=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error'){if(m.text().startsWith('Applying workaround to Firefox bug https://bugzilla.mozilla.org/show_bug.cgi?id=1397977'))engineMessages.push(m.text());else errors.push(m.text());}});
 await page.setViewportSize({width:1280,height:720});await page.goto('http://127.0.0.1:8093/');
 await page.waitForFunction(()=>window.ChillStartup&&ChillStartup.phase==='done',null,{timeout:90000});
 const writer=await page.evaluate(()=>ChillSaveStore.canWrite);if(!writer)throw new Error('Primary session did not acquire its save lock');
 const second=await page.context().newPage();await second.goto('http://127.0.0.1:8093/ui/native.css');
 const locked=await second.evaluate(async()=>{const {beginStorageSession}=await import('/ui/save-store.mjs');const store=await beginStorageSession();store.load();return{canWrite:store.canWrite,result:store.write(JSON.stringify({format:'chill-with-you-web',version:2}))};});
 await second.close();if(locked.canWrite||locked.result!==0)throw new Error('A second tab can overwrite the primary save');
 await page.evaluate(()=>window.chillUnityInstance.SendMessage('ChillPrototype','StopStory'));
 const sizes=[];for(const size of [{width:1280,height:720},{width:1920,height:1080},{width:2560,height:1080}]){
  await page.setViewportSize(size);await page.waitForTimeout(700);await page.screenshot({path:'output/playwright/24-room-'+size.width+'x'+size.height+'-'+(await page.evaluate(()=>navigator.userAgent.includes('Firefox')?'firefox':navigator.userAgent.includes('Edg/')?'edge':'chrome'))+'.png'});sizes.push(size);
 }
 await page.setViewportSize({width:1920,height:1080});
 const metrics=await page.evaluate(async()=>{
  const canvas=document.getElementById('unity-canvas'),gl=canvas.getContext('webgl2'),debug=gl.getExtension('WEBGL_debug_renderer_info');
  const gpu=debug?gl.getParameter(debug.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER);
  const frames=[];let previous=performance.now(),end=previous+10000;
  await new Promise(resolve=>{function tick(now){frames.push(now-previous);previous=now;if(now<end)requestAnimationFrame(tick);else resolve();}requestAnimationFrame(tick);});
  const sorted=frames.slice(10).sort((a,b)=>a-b);
  return{browser:navigator.userAgent,gpu,webgl2:!!gl,dpr:devicePixelRatio,p95FrameMilliseconds:sorted[Math.floor(sorted.length*.95)],maximumFrameMilliseconds:sorted.at(-1),samples:sorted.length,heapBytes:performance.memory?.usedJSHeapSize||null,saveBytes:(localStorage.getItem('chill-with-you-save-v1')||'').length};
 });
 if(errors.length)throw new Error(JSON.stringify(errors));
 return{passed:true,scope:'Startup, multi-tab write exclusion, three viewport room renders and 10-second RAF sample; not full gameplay, GPU render timing, DPI or soak qualification',multiTabWriterLock:true,sizes,metrics,errors,engineMessages};
}
