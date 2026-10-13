// Draw the original Boot canvas and clip curves before WebAssembly is available.
(() => {
  const layer=document.getElementById('unity-loading-bar'),canvas=document.getElementById('startup-canvas');
  const ctx=canvas.getContext('2d'),status=document.getElementById('startup-status');
  const begun=performance.now(),images=new Map();let data,phase='loading',phaseTime=begun,frame,failed=false,finishAt=null,finishTask;
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
    if(phase==='done'||failed)return;
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
        const left=-rw*n.pivot[0],bottom=-rh*n.pivot[1];
        if(n.image&&n.image.enabled){
          const color=n.image.color;ctx.save();ctx.globalAlpha=Math.max(0,Math.min(1,v.alpha??color[3]));
          if(n.image.sprite){ctx.translate(left,bottom+rh);ctx.scale(1,-1);ctx.drawImage(images.get(n.image.sprite),0,0,rw,rh);}
          else{ctx.fillStyle=`rgb(${color.slice(0,3).map(x=>Math.round(x*255)).join(',')})`;ctx.fillRect(left,bottom,rw,rh);}
          ctx.restore();
        }
        // Children use the parent's rectangle center, independent of its pivot.
        ctx.translate(rw*(.5-n.pivot[0]),rh*(.5-n.pivot[1]));for(const c of n.children)paint(c,rw,rh);ctx.restore();
      }
      paint(layout.root,layout.width,height);
      if(phase==='loading'&&finishAt!==null&&now>=finishAt){phase='finish';phaseTime=now;}
      else if(phase==='finish'&&elapsed>=clip.duration){phase='fade';phaseTime=now;}
      else if(phase==='fade'&&elapsed>=clip.duration){phase='done';layer.hidden=true;game.inert=false;canvas.remove();resolveDone();return;}
    }
    frame=requestAnimationFrame(draw);
  }
  let resolveDone;const done=new Promise(resolve=>resolveDone=resolve);
  function fail(message){
    failed=true;phase='error';cancelAnimationFrame(frame);
    const warning=document.getElementById('unity-warning');warning.hidden=false;warning.textContent=message;
    const retry=document.createElement('button');retry.textContent='Retry';retry.onclick=()=>location.reload();warning.appendChild(retry);
    status.textContent='Không tải được game.';
  }
  const assetsReady=fetch('ui/loading/screen.json').then(r=>{if(!r.ok)throw new Error('Loading screen: HTTP '+r.status);return r.json();}).then(async manifest=>{
    await Promise.all(Object.entries(manifest.assets).map(async([name,url])=>{const img=new Image();img.src=url;await img.decode();images.set(name,img);}));
    data=manifest;layer.style.background='transparent';
  }).catch(error=>{fail(error.message);throw error;});
  // The native runtime calls finish only after data and the saved room are ready.
  window.ChillStartup={get phase(){return phase;},progress(value){status.textContent='Đang tải game… '+Math.floor(value*100)+'%';},fail,
    finish(){return finishTask??=assetsReady.then(()=>{
      if(failed)throw new Error('Startup failed');
      const ready=Math.max(performance.now(),begun+2000),cycle=data.clips.loop.duration*1000;
      finishAt=data.transitions.SplashFinish.waitLoopEnd?begun+Math.ceil((ready-begun)/cycle)*cycle:ready;
      return done;
    });}};
  assetsReady.catch(()=>{});frame=requestAnimationFrame(draw);
})();
