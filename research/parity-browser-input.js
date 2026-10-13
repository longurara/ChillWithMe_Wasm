// Run in the isolated profile after gameplay qualification; all writes are fixture data.
async(page)=>{
 const errors=[];page.on('pageerror',error=>errors.push(error.message));
 await page.setViewportSize({width:1280,height:720});
 await page.evaluate(()=>{const unity=window.chillUnityInstance;unity.SendMessage('ChillPrototype','StopStory');unity.SendMessage('ChillPrototype','Close');unity.SendMessage('ChillPrototype','UICommand',JSON.stringify({action:'language',text:'En'}));unity.SendMessage('ChillPrototype','UICommand',JSON.stringify({action:'timer-reset'}));});
 await page.waitForTimeout(600);
 await page.mouse.move(1232,300);await page.waitForTimeout(800);
 await page.screenshot({path:'output/playwright/24-input-hover.png'});
 await page.mouse.click(1232,300);await page.waitForTimeout(500);
 await page.mouse.click(1000,360);await page.keyboard.press('Control+A');
 await page.keyboard.type('Browser keyboard: Vietnamese, Japanese, Korean and Russian.');
 await page.waitForTimeout(6000);
 const note=await page.evaluate(()=>JSON.parse(localStorage.getItem('chill-with-you-save-v1')).workspace.notes.find(n=>n.id==='fixture-note')?.text);
 if(note!=='Browser keyboard: Vietnamese, Japanese, Korean and Russian.')throw new Error('Native note keyboard input did not save: '+note);
 await page.screenshot({path:'output/playwright/24-input-note.png'});
 await page.keyboard.press('Escape');await page.waitForTimeout(400);
 await page.mouse.click(1158,165);await page.waitForTimeout(800);
 const running=await page.evaluate(()=>JSON.parse(localStorage.getItem('chill-with-you-save-v1')).timerRunning);
 if(!running)throw new Error('Native timer Play button did not start the timer');
 await page.screenshot({path:'output/playwright/24-input-timer.png'});
 await page.evaluate(()=>window.chillUnityInstance.SendMessage('ChillPrototype','UICommand',JSON.stringify({action:'timer-reset'})));
 if(errors.length)throw new Error(JSON.stringify(errors));
 return{passed:true,browser:await page.evaluate(()=>navigator.userAgent),nativePointerOpen:true,nativeKeyboardEditSaved:true,nativeTimerPointerStart:true,errors,userSaveWritten:false};
}
