// Exercises an actual failed catalog request, then the visible Retry button.
async(page)=>{
 await page.goto('http://127.0.0.1:8093/ui/native.css');
 let release,seen;const held=new Promise(resolve=>release=resolve),requestSeen=new Promise(resolve=>seen=resolve);
 await page.route('**/StreamingAssets/chill-library.json',async route=>{seen();await held;await route.fulfill({status:503,body:'fixture: unavailable'});});
 await page.goto('http://127.0.0.1:8093/');await page.waitForFunction(()=>!!window.chillUnityInstance,null,{timeout:90000});
 await Promise.race([requestSeen,page.waitForTimeout(30000).then(()=>{throw new Error('Catalog request not seen');})]);await page.waitForTimeout(2300);
 if(await page.evaluate(()=>ChillStartup.phase)!=='loading')throw new Error('Catalog delay reveals an incomplete room');
 await page.screenshot({path:'output/playwright/23-real-waiting-catalog.png'});release();
 await page.waitForFunction(()=>ChillStartup.phase==='error',null,{timeout:30000});
 await page.screenshot({path:'output/playwright/23-real-catalog-error.png'});
 if(!await page.evaluate(()=>document.getElementById('unity-canvas').inert))throw new Error('Failed room accepts input');
 await page.unroute('**/StreamingAssets/chill-library.json');await page.getByRole('button',{name:'Retry',exact:true}).click();
 await page.waitForFunction(()=>window.ChillStartup?.phase==='done',null,{timeout:90000});await page.waitForTimeout(700);
 await page.screenshot({path:'output/playwright/23-real-retry-success.png'});
 return{passed:true,catalogDelayKeepsLoading:true,failedCatalogShowsRetry:true,errorBlocksRoomInput:true,retryStartsActualRoom:true,expectedHttpFailure:503};
}
