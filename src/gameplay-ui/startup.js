// Draw the original Boot canvas and clip curves before WebAssembly is available.
(() => {
  const layer=document.getElementById('unity-loading-bar'),canvas=document.getElementById('startup-canvas');
  const ctx=canvas.getContext('2d'),status=document.getElementById('startup-status');
  const begun=performance.now(),images=new Map();let data,phase='loading',phaseTime=begun,frame,failed=false,finishAt=null,finishTask,directionQueue=null,directionResolve,directionHold=0;
  const game=document.getElementById('unity-canvas');game.inert=true;
  function sample(keys,time){
    if(time<=keys[0].time)return keys[0].value;
    for(let i=1;i<keys.length;i++)if(time<keys[i].time){
      const a=keys[i-1],b=keys[i],span=b.time-a.time,t=(time-a.time)/span;
      if(a.step)return a.value;
      return(2*t*t*t-3*t*t+1)*a.value+(t*t*t-2*t*t+t)*span*a.outSlope+(-2*t*t*t+3*t*t)*b.value+(t*t*t-t*t)*span*b.inSlope;
    }
    return keys[keys.length-1].value;
  }
  function draw(now){
    if(phase==='done'||failed&&!directionQueue)return;
    if(data){
      const w=innerWidth,h=innerHeight,dpr=Math.min(devicePixelRatio||1,2);
      if(canvas.width!==Math.round(w*dpr)||canvas.height!==Math.round(h*dpr)){canvas.width=Math.round(w*dpr);canvas.height=Math.round(h*dpr);}
      ctx.setTransform(dpr,0,0,dpr,0,0);ctx.clearRect(0,0,w,h);
      const layout=data.layouts[w<h?'portrait':'landscape'],scale=w/layout.width,height=h/scale;
      const clip=data.clips[phase==='loading'?'loop':phase],elapsed=(now-phaseTime)/1000;
      const time=phase==='loading'?elapsed%clip.duration:Math.min(elapsed,clip.duration);
      const values=new Map();for(const t of clip.tracks){if(!values.has(t.path))values.set(t.path,{});values.get(t.path)[t.property]=sample(t.keys,time);}
      ctx.translate(w/2,h/2);ctx.scale(scale,-scale);
      function paint(n,pw,ph){
        const v=values.get(n.path)||{};if(!(v.active===undefined?n.active:v.active>=.5))return;
        const rw=pw*(n.anchorMax[0]-n.anchorMin[0])+n.size[0],rh=ph*(n.anchorMax[1]-n.anchorMin[1])+n.size[1];
        const x=pw*(n.anchorMin[0]+(n.anchorMax[0]-n.anchorMin[0])*n.pivot[0]-.5)+(v.x??n.position[0]);
        const y=ph*(n.anchorMin[1]+(n.anchorMax[1]-n.anchorMin[1])*n.pivot[1]-.5)+(v.y??n.position[1]);
        ctx.save();ctx.translate(x,y);ctx.rotate((v.rotation||0)*Math.PI/180);ctx.scale(v.scaleX??n.scale[0],v.scaleY??n.scale[1]);
        ctx.globalAlpha*=Math.max(0,Math.min(1,v.groupAlpha??n.groupAlpha??1));
        const left=-rw*n.pivot[0],bottom=-rh*n.pivot[1];
        if(n.image&&n.image.enabled){
          const color=n.image.color;ctx.save();ctx.globalAlpha*=Math.max(0,Math.min(1,v.alpha??color[3]));
          if(n.image.sprite){ctx.translate(left,bottom+rh);ctx.scale(1,-1);ctx.drawImage(images.get(n.image.sprite),0,0,rw,rh);}
          else{ctx.fillStyle=`rgb(${color.slice(0,3).map(x=>Math.round(x*255)).join(',')})`;ctx.fillRect(left,bottom,rw,rh);}
          ctx.restore();
        }
        // Children use the parent's rectangle center, independent of its pivot.
        ctx.translate(rw*(.5-n.pivot[0]),rh*(.5-n.pivot[1]));for(const c of n.children)paint(c,rw,rh);ctx.restore();
      }
      paint(layout.root,layout.width,height);
      if(directionQueue){
        if(elapsed>=Math.max(clip.duration,directionHold)){
          const next=directionQueue.shift();
          if(next){phase=next.clip;phaseTime=now;directionHold=next.hold||0;}
          else{phase=directionReveal===null?'error':'done';const reveal=directionReveal;directionQueue=null;if(reveal){layer.hidden=true;game.inert=false;}directionResolve();return;}
        }
      }else if(phase==='loading'&&finishAt!==null&&now>=finishAt){phase='finish';phaseTime=now;}
      else if(phase==='finish'&&elapsed>=clip.duration){phase='fade';phaseTime=now;}
      else if(phase==='fade'&&elapsed>=clip.duration){phase='done';layer.hidden=true;game.inert=false;resolveDone();return;}
    }
    frame=requestAnimationFrame(draw);
  }
  let directionReveal=true;
  function cancelDirection(){
    if(phase==='loading'||failed)return;
    cancelAnimationFrame(frame);directionQueue=null;phase='done';layer.hidden=true;game.inert=false;
    for(const button of layer.querySelectorAll('button'))button.remove();
    const resolve=directionResolve;directionResolve=null;if(resolve)resolve();
  }
  function play(kind){return assetsReady.then(()=>new Promise((resolve,reject)=>{
    if(directionQueue){reject(new Error("A loading direction is already playing."));return;}
    const plans={"credits-finish":[{clip:"creditsIn",hold:4},{clip:"finish"}],fade:[{clip:"fade"}],gameEnd:[{clip:"gameEnd"}],error:[{clip:"errorFinish"},{clip:"errorFade"}]};
    const plan=plans[kind];if(!plan){reject(new Error("Unknown loading direction."));return;}
    const first=plan[0];directionQueue=plan.slice(1);directionResolve=resolve;directionReveal=kind==="error"?null:kind==="fade";directionHold=first.hold||0;phase=first.clip;phaseTime=performance.now();layer.hidden=false;game.inert=true;frame=requestAnimationFrame(draw);
    if(kind==="gameEnd"){const back=document.createElement("button");back.textContent="Return to room";back.style.cssText="position:absolute;bottom:3%;left:50%;transform:translateX(-50%);padding:12px 24px";back.onclick=()=>{if(directionQueue)return;back.remove();play("fade");};layer.appendChild(back);}
  }));}
  let resolveDone;const done=new Promise(resolve=>resolveDone=resolve);
  function fail(message){
    failed=true;phase='error';cancelAnimationFrame(frame);
    const warning=document.getElementById('unity-warning');warning.hidden=false;warning.textContent=message;
    const retry=document.createElement('button');retry.textContent='Retry';retry.onclick=()=>location.reload();warning.appendChild(retry);
    status.textContent='Không tải được game.';if(data){directionQueue=null;play('error').catch(()=>{});}
  }
  const assetsReady=fetch('ui/loading/screen.json').then(r=>{if(!r.ok)throw new Error('Loading screen: HTTP '+r.status);return r.json();}).then(async manifest=>{
    await Promise.all(Object.entries(manifest.assets).map(async([name,url])=>{const img=new Image();img.src=url;await img.decode();images.set(name,img);}));
    data=manifest;layer.style.background='transparent';
  }).catch(error=>{fail(error.message);throw error;});

  // --- AssetBundle CacheStorage & Preload System ---
  const CACHE_NAME='chill-assetpacks-v1',MODE_KEY='chill-preload-mode',REMEMBER_KEY='chill-preload-remember';
  const CORE_PREFIXES=['texture2d/','mesh/','sound/','animationclip/','sprite/','material/','gameobject/','animatorcontroller/','cubemap/','avatar/','rendertexture/','flare/','terrainlayer/','audiomixercontroller/'];
  const MODE_LABELS={core:'Mức 1 (~142 MB)',voice:'Mức 2 (~217 MB)',all:'Mức 3 (~454 MB)',live:'Tải trực tiếp'};
  const nativeFetch=window.fetch.bind(window);
  const bundleHashes=new Map(),inflightPacks=new Map();
  let catalogPromise=null,cachePromise=window.caches?caches.open(CACHE_NAME).catch(()=>null):Promise.resolve(null);

  function extractPackName(url){
    const str=typeof url==='string'?url:(url&&url.url)||'';
    const m=/(?:^|\/)assetpacks\/([^?#]+)/.exec(str);
    return m?decodeURIComponent(m[1]):null;
  }
  function makeCacheKey(packName){
    const h=bundleHashes.get(packName)||'';
    return new URL('assetpacks/'+packName+(h?'?h='+h:''),document.baseURI).href;
  }
  function ensureCatalog(){
    if(!catalogPromise){
      catalogPromise=nativeFetch('StreamingAssets/chill-library.json').then(r=>r.ok?r.json():null).then(cat=>{
        if(cat&&Array.isArray(cat.bundles)){for(const b of cat.bundles)bundleHashes.set(b.name,b.hash||'');}
        return cat;
      }).catch(()=>null);
    }
    return catalogPromise;
  }
  async function fetchPackBuffer(packName,rawUrl){
    if(inflightPacks.has(packName))return inflightPacks.get(packName);
    const task=(async()=>{
      const cache=await cachePromise;
      const key=makeCacheKey(packName);
      if(cache){
        try{
          const hit=await cache.match(key);
          if(hit){const buf=await hit.arrayBuffer();if(buf&&buf.byteLength>0)return{buf,cached:true};}
        }catch(_){}
      }
      const res=await nativeFetch(rawUrl||('assetpacks/'+packName));
      if(!res.ok)throw new Error('HTTP '+res.status+' khi tải '+packName);
      const buf=await res.arrayBuffer();
      if(cache&&buf.byteLength>0){
        try{
          await cache.put(key,new Response(buf.slice(0),{status:200,headers:{'Content-Type':'application/octet-stream','Content-Length':String(buf.byteLength)}}));
        }catch(_){}
      }
      return{buf,cached:false};
    })();
    inflightPacks.set(packName,task);
    try{return await task;}finally{inflightPacks.delete(packName);}
  }
  window.fetch=function(input,init){
    const method=(init&&init.method)||(typeof input==='object'&&input&&input.method)||'GET';
    if(method.toUpperCase()==='GET'){
      const packName=extractPackName(input);
      if(packName){
        const rawUrl=typeof input==='string'?input:input.url;
        return ensureCatalog().then(()=>fetchPackBuffer(packName,rawUrl)).then(({buf})=>new Response(buf,{
          status:200,
          statusText:'OK',
          headers:{'Content-Type':'application/octet-stream','Content-Length':String(buf.byteLength)}
        })).catch(()=>nativeFetch(input,init));
      }
    }
    return nativeFetch(input,init);
  };
  ensureCatalog();

  // --- Startup HUD & 3-Tier Chooser ---
  const hud=document.createElement('div');hud.id='startup-hud';
  hud.innerHTML=`
    <div id="startup-chooser" hidden>
      <h2>Chọn chế độ tải tài nguyên</h2>
      <p>Tài nguyên tải trước được lưu vào bộ nhớ đệm trình duyệt — những lần chơi sau sẽ vào ngay không cần tải lại.</p>
      <div class="preload-grid">
        <button type="button" class="preload-card" data-mode="core">
          <div class="preload-card-head"><span>Mức 1 · Đồ đạc, Trang phục &amp; Hiệu ứng (~142 MB)</span><span class="preload-badge">Khuyên dùng</span></div>
          <div class="preload-desc">Tải sẵn 100% đồ đạc, quần áo, cử động nhân vật, icon và âm thanh SFX/Ambient. Đổi đồ mượt tức thì, nhạc tải theo bài.</div>
        </button>
        <button type="button" class="preload-card" data-mode="voice">
          <div class="preload-card-head"><span>Mức 2 · Đồ đạc + Toàn bộ Lồng tiếng (~217 MB)</span><span class="preload-badge">Đầy đủ Voice</span></div>
          <div class="preload-desc">Bao gồm toàn bộ Mức 1 cộng thêm 1,381 câu thoại lồng tiếng của Satone. Không trễ âm thoại khi đọc cốt truyện.</div>
        </button>
        <button type="button" class="preload-card" data-mode="all">
          <div class="preload-card-head"><span>Mức 3 · Tải sạch 100% Toàn bộ Game (~454 MB)</span><span class="preload-badge">Trọn bộ Offline</span></div>
          <div class="preload-desc">Tải trước toàn bộ đồ đạc, lồng tiếng và cả 152 bài nhạc Lo-Fi (233 gói). Vào phòng chơi mượt 100% không cần tải thêm bất cứ gì.</div>
        </button>
      </div>
      <div class="preload-footer">
        <label><input type="checkbox" id="preload-remember" checked> Ghi nhớ lựa chọn cho lần sau</label>
        <button type="button" class="preload-live-btn" data-mode="live">Vào nhanh (Tải trực tiếp khi chơi)</button>
      </div>
    </div>
    <div id="startup-bar-box">
      <div class="startup-bar-top">
        <span id="startup-progress-label">Đang tải nhân game… 0%</span>
        <div class="startup-bar-actions">
          <button type="button" class="startup-mini-btn" id="startup-change-mode">Đổi mức tải</button>
          <button type="button" class="startup-mini-btn" id="startup-skip-preload" hidden>Bỏ qua → Vào ngay</button>
        </div>
      </div>
      <div class="startup-track"><div id="startup-progress-fill"></div></div>
    </div>`;
  layer.appendChild(hud);

  const chooserEl=hud.querySelector('#startup-chooser'),rememberEl=hud.querySelector('#preload-remember');
  const labelEl=hud.querySelector('#startup-progress-label'),fillEl=hud.querySelector('#startup-progress-fill');
  const changeBtn=hud.querySelector('#startup-change-mode'),skipBtn=hud.querySelector('#startup-skip-preload');

  let engineProgress=0,preloadRatio=0,mountRatio=0,preloadActive=false,preloadDone=false,mountingActive=false;
  let currentMode=localStorage.getItem(MODE_KEY)||'core';
  if(!MODE_LABELS[currentMode])currentMode='core';
  let remembered=localStorage.getItem(REMEMBER_KEY)==='1';
  rememberEl.checked=true;
  let preloadRunId=0,resolveChoice,choicePromise=new Promise(r=>{resolveChoice=r;});
  let preloadTask=null,skipRequested=false;

  function highlightActiveCard(){
    for(const card of hud.querySelectorAll('[data-mode]')){
      card.classList.toggle('active',card.getAttribute('data-mode')===currentMode);
    }
  }
  function updateProgressUI(customText){
    status.textContent=customText||labelEl.textContent;
    if(customText){labelEl.textContent=customText;}
    let pct=0;
    if(mountingActive){
      pct=85+mountRatio*15;
    }else if(preloadActive||preloadDone){
      if(currentMode==='live')pct=engineProgress*100;
      else pct=engineProgress*35+preloadRatio*50+(preloadDone?15:0);
    }else{
      pct=engineProgress*35;
    }
    fillEl.style.width=Math.max(2,Math.min(100,Math.floor(pct)))+'%';
  }
  function bundlesForMode(cat,mode){
    if(!cat||!Array.isArray(cat.bundles)||mode==='live')return[];
    if(mode==='all')return cat.bundles.map(b=>b.name);
    if(mode==='voice')return cat.bundles.filter(b=>CORE_PREFIXES.some(p=>b.name.startsWith(p))||b.name.startsWith('voice/')).map(b=>b.name);
    return cat.bundles.filter(b=>CORE_PREFIXES.some(p=>b.name.startsWith(p))).map(b=>b.name);
  }
  async function runPreload(mode){
    const runId=++preloadRunId;
    skipRequested=false;preloadActive=true;preloadDone=false;preloadRatio=0;
    const cat=await ensureCatalog();
    const list=bundlesForMode(cat,mode);
    if(list.length===0){
      preloadRatio=1;preloadActive=false;preloadDone=true;skipBtn.hidden=true;
      updateProgressUI(engineProgress<1?`Đang tải nhân game… ${Math.floor(engineProgress*100)}% (${MODE_LABELS[mode]})`:`Chế độ: ${MODE_LABELS[mode]} · Đang vào phòng…`);
      return mode;
    }
    skipBtn.hidden=false;
    let completed=0,cachedCount=0;
    const total=list.length,concurrency=6;
    let cursor=0;
    updateProgressUI(`Đang tải tài nguyên (${MODE_LABELS[mode]}): 0 / ${total} gói (0%)`);
    async function worker(){
      while(cursor<total&&runId===preloadRunId&&!skipRequested){
        const idx=cursor++;
        const packName=list[idx];
        try{
          const res=await fetchPackBuffer(packName);
          if(res&&res.cached)cachedCount++;
        }catch(err){console.warn('Preload pack warning:',packName,err);}
        if(runId!==preloadRunId||skipRequested)return;
        completed++;
        preloadRatio=completed/total;
        const pct=Math.floor(preloadRatio*100);
        const cacheNote=cachedCount===completed&&completed>4?' · Đã có trong bộ nhớ đệm':'';
        updateProgressUI(`Đang tải tài nguyên (${MODE_LABELS[mode]}): ${completed} / ${total} gói (${pct}%)${cacheNote}`);
      }
    }
    await Promise.all(Array.from({length:Math.min(concurrency,total)},()=>worker()));
    if(runId!==preloadRunId)return preloadTask;
    preloadRatio=1;preloadActive=false;preloadDone=true;skipBtn.hidden=true;
    updateProgressUI(skipRequested?`Đã bỏ qua tải trước · Đang vào phòng…`:`Đã sẵn sàng ${completed}/${total} gói (${MODE_LABELS[mode]}) · Đang khởi tạo phòng…`);
    return mode;
  }
  function selectMode(mode,savePref){
    if(!MODE_LABELS[mode])mode='core';
    currentMode=mode;
    try{
      localStorage.setItem(MODE_KEY,mode);
      if(savePref!==undefined)localStorage.setItem(REMEMBER_KEY,savePref?'1':'0');
    }catch(_){}
    highlightActiveCard();
    chooserEl.hidden=true;
    preloadTask=runPreload(mode);
    if(resolveChoice){const r=resolveChoice;resolveChoice=null;r(preloadTask);}
  }
  for(const btn of hud.querySelectorAll('[data-mode]')){
    btn.addEventListener('click',()=>{selectMode(btn.getAttribute('data-mode'),rememberEl.checked);});
  }
  changeBtn.addEventListener('click',()=>{
    highlightActiveCard();
    chooserEl.hidden=!chooserEl.hidden;
  });
  skipBtn.addEventListener('click',()=>{
    skipRequested=true;skipBtn.hidden=true;
  });
  if(remembered){
    selectMode(currentMode,true);
  }else{
    highlightActiveCard();
    chooserEl.hidden=false;
    updateProgressUI('Đang tải nhân game… 0% · Hãy chọn mức tải tài nguyên phía trên');
  }

  // The native runtime calls finish only after data and the saved room are ready.
  window.ChillStartup={
    get phase(){return phase;},
    play:function(kind){hud.hidden=true;return play(kind);},
    cancelDirection,
    progress(value){
      engineProgress=Math.max(0,Math.min(1,value||0));
      if(!preloadActive&&!preloadDone){
        const suffix=chooserEl.hidden?` (${MODE_LABELS[currentMode]})`:' · Hãy chọn mức tải phía trên';
        updateProgressUI('Đang tải nhân game… '+Math.floor(engineProgress*100)+'%'+suffix);
      }else if(currentMode==='live'&&!mountingActive){
        updateProgressUI('Đang tải nhân game… '+Math.floor(engineProgress*100)+'% (Tải trực tiếp)');
      }else{
        updateProgressUI();
      }
    },
    waitPreload(){
      if(preloadTask)return preloadTask;
      return choicePromise.then(()=>preloadTask);
    },
    mountProgress(done,total){
      mountingActive=true;
      mountRatio=total>0?done/total:1;
      skipBtn.hidden=true;chooserEl.hidden=true;
      updateProgressUI(`Đang nạp đồ đạc & hiệu ứng vào RAM… ${done} / ${total} gói`);
    },
    getPreloadMode(){return currentMode;},
    setPreloadMode(mode,remember=true){
      if(!MODE_LABELS[mode])return currentMode;
      currentMode=mode;
      try{localStorage.setItem(MODE_KEY,mode);localStorage.setItem(REMEMBER_KEY,remember?'1':'0');}catch(_){}
      if(mode!=='live')ensureCatalog().then(cat=>{for(const p of bundlesForMode(cat,mode))fetchPackBuffer(p).catch(()=>{});});
      return currentMode;
    },
    fail,
    finish(){return finishTask??=assetsReady.then(()=>{
      if(failed)throw new Error('Startup failed');
      hud.hidden=true;
      const ready=Math.max(performance.now(),begun+2000),cycle=data.clips.loop.duration*1000;
      finishAt=data.transitions.SplashFinish.waitLoopEnd?begun+Math.ceil((ready-begun)/cycle)*cycle:ready;
      return done;
    });}
  };
  assetsReady.catch(()=>{});frame=requestAnimationFrame(draw);
})();
