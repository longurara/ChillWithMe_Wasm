export function installContextRecovery({canvas,getInstance,notify,reload,storage,now=Date.now}) {
  let lost=false,disposed=false;
  const budgetKey='chill-webgl-recovery-at';
  const save=()=>{try{getInstance()?.SendMessage('ChillPrototype','SaveFromBrowser');}catch{}};
  const retry=()=>{if(disposed)return;save();reload();};
  const onLost=event=>{
    event.preventDefault();if(lost||disposed)return;lost=true;
    save();try{getInstance()?.SendMessage('ChillPrototype','BrowserVisibility','hidden');}catch{}
    notify('Graphics context lost. Your last saved progress is retained. Waiting for the GPU…',retry);
  };
  const onRestored=()=>{
    if(!lost||disposed)return;lost=false;
    let previous=0,canRecord=false;try{previous=Number(storage.getItem(budgetKey))||0;canRecord=true;}catch{}
    if(!canRecord){notify('Graphics recovered. Restart the game to continue.',retry);return;}
    if(previous&&now()-previous<60000){notify('Graphics recovered. Restart the game to continue.',retry);return;}
    try{storage.setItem(budgetKey,String(now()));}catch{notify('Graphics recovered. Restart the game to continue.',retry);return;}
    retry();
  };
  canvas.addEventListener('webglcontextlost',onLost);
  canvas.addEventListener('webglcontextrestored',onRestored);
  return {retry,dispose(){disposed=true;canvas.removeEventListener('webglcontextlost',onLost);canvas.removeEventListener('webglcontextrestored',onRestored);}};
}

export async function attachContextRecovery() {
  const catalog=await fetch(new URL('./i18n.json',import.meta.url)).then(r=>{if(!r.ok)throw new Error('Cannot load UI translations');return r.json();}).catch(()=>({entries:[]}));
  const translate=text=>{
    let language='En';try{language=JSON.parse(localStorage.getItem('chill-with-you-save-v1'))?.language||'En';}catch{}
    const row=catalog.entries.find(row=>row.En===text);return row?.[language]||text;
  };
  let storage;try{storage=sessionStorage;}catch{}
  return installContextRecovery({storage,
    canvas:document.getElementById('unity-canvas'),getInstance:()=>window.chillUnityInstance,reload:()=>location.reload(),
    notify(message,retry){const warning=document.getElementById('unity-warning');warning.hidden=false;warning.replaceChildren();const label=document.createElement('p');label.textContent=translate(message);const button=document.createElement('button');button.textContent=translate('Retry');button.onclick=retry;warning.append(label,button);}
  });
}
