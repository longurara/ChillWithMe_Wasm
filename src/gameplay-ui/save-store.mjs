const key='chill-with-you-save-v1';

export function createSaveStore(storage,canWrite=()=>true){
 let expected,preserveBackup=false;
 return {
  get canWrite(){return canWrite();},
  load(){expected=storage.getItem(key)||'';return expected;},
  backup(){preserveBackup=true;return storage.getItem(key+'.backup')||'';},
  write(text){
   try{
    if(!canWrite())return 0;
    const next=JSON.parse(text);if(next.format!=='chill-with-you-web'||![1,2].includes(next.version))return 0;
    const old=storage.getItem(key)||'';if(expected!==undefined&&old!==expected)return 0;
    let previous;try{previous=JSON.parse(old);}catch{}
    if(!preserveBackup&&previous?.format==='chill-with-you-web'&&[1,2].includes(previous.version)){
     storage.setItem(key+'.backup',old);
     if(previous.version===1&&next.version===2&&!storage.getItem(key+'.before-v2'))storage.setItem(key+'.before-v2',old);
    }
    storage.setItem(key,text);expected=text;preserveBackup=false;return 1;
   }catch{return 0;}
  }
 };
}

export async function beginStorageSession(flush=()=>{}){
 let storage;
 try{storage=localStorage;storage.getItem(key);}catch{
  // Storage denial must not prevent the room from starting. Keep this session
  // read-only; the player's current progress can still be exported from Unity.
  const store=createSaveStore({getItem:()=>null,setItem(){throw Error('Storage is blocked');}},()=>false);
  store.readOnlyReason='storage-blocked';return store;
 }
 let writable=true,release;
 // Keep a browser-managed lock for the session. A second tab may read/export but cannot overwrite.
 async function acquire(){
  if(!navigator.locks){writable=true;return;}
  writable=false;release=undefined;
  await new Promise(resolve=>{
   navigator.locks.request(key,{ifAvailable:true},async lock=>{
    writable=!!lock;resolve();if(lock)await new Promise(done=>release=done);
   }).catch(()=>resolve());
  });
 }
 await acquire();
 const store=createSaveStore(storage,()=>writable);
 addEventListener('pagehide',()=>{try{flush();}finally{writable=false;if(release)release();}});
 addEventListener('pageshow',async event=>{if(event.persisted)await acquire();});
 return store;
}
