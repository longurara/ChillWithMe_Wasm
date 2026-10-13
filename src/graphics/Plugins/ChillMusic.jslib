mergeInto(LibraryManager.library, {
  $ChillMusicStore: {
    db: null, receiver: '',
    open: function () {
      if (this.db) return this.db;
      this.db = new Promise(function (resolve, reject) {
        var request = indexedDB.open('chill-personal-music', 1);
        request.onupgradeneeded = function () { request.result.createObjectStore('tracks', {keyPath:'id'}); };
        request.onsuccess = function () { resolve(request.result); };
        request.onerror = function () { reject(new Error('Browser storage is unavailable.')); };
      });
      return this.db;
    },
    send: function (target, method, value) {
      if (window.chillUnityInstance) window.chillUnityInstance.SendMessage(target, method, JSON.stringify(value));
    },
    write: async function (row) {
      var db = await this.open();
      return new Promise(function (resolve,reject) {
        var tx=db.transaction('tracks','readwrite'),store=tx.objectStore('tracks'),existing=store.get(row.id);
        existing.onsuccess=function(){var count=store.count();count.onsuccess=function(){if(!existing.result&&count.result>=200){tx.abort();return;}store.put(row);};};
        tx.oncomplete=resolve;tx.onerror=tx.onabort=function () {reject(new Error('No space available to save this music file.'));};
      });
    },
    get: async function (id) {
      var db=await this.open();
      return new Promise(function (resolve,reject) {
        var req=db.transaction('tracks').objectStore('tracks').get(id);
        req.onsuccess=function () {resolve(req.result);};req.onerror=function () {reject(req.error);};
      });
    },
    tags: function(bytes,fallback){
      var data=new Uint8Array(bytes),view=new DataView(bytes),result={title:fallback,artist:'Personal music'};
      function sync(offset){return ((data[offset]&127)<<21)|((data[offset+1]&127)<<14)|((data[offset+2]&127)<<7)|(data[offset+3]&127);}
      function decode(start,end){var mode=data[start++],encoding=mode===3?'utf-8':mode===2?'utf-16be':mode===1?'utf-16':'iso-8859-1';return new TextDecoder(encoding).decode(data.subarray(start,end)).replace(/\0/g,'').trim().slice(0,240);}
      try{
        if(data.length>=10&&data[0]===73&&data[1]===68&&data[2]===51&&data[3]>=3&&data[3]<=4){
          var end=Math.min(data.length,10+sync(6),1024*1024),offset=10;
          if(data[5]&64){if(offset+4>end)return result;offset+=data[3]===4?sync(offset):4+view.getUint32(offset);}
          for(var count=0;offset+10<=end&&count<1000;count++){
            var id=String.fromCharCode.apply(null,data.subarray(offset,offset+4)),size=data[3]===4?sync(offset+4):view.getUint32(offset+4),start=offset+10;
            if(size<1||start+size>end)break;
            if((id==='TIT2'||id==='TPE1')&&size<=65536&&!(data[offset+9]&0x0c)){var value=decode(start,start+size);if(value)result[id==='TIT2'?'title':'artist']=value;}
            offset=start+size;
          }
        }else if(data.length>=128&&String.fromCharCode.apply(null,data.subarray(data.length-128,data.length-125))==='TAG'){
          var at=data.length-125;var title=new TextDecoder('iso-8859-1').decode(data.subarray(at,at+30)).replace(/\0/g,'').trim(),artist=new TextDecoder('iso-8859-1').decode(data.subarray(at+30,at+60)).replace(/\0/g,'').trim();if(title)result.title=title;if(artist)result.artist=artist;
        }
      }catch(error){}return result;
    },
    wave: function (audio) {
      var channels=Math.min(2,audio.numberOfChannels),frames=audio.length,rate=audio.sampleRate;
      if (audio.duration>600 || rate>96000 || frames*channels*2>128*1024*1024) throw new Error('Music must be at most 10 minutes and 128 MB when decoded.');
      var buffer=new ArrayBuffer(44+frames*channels*2),view=new DataView(buffer);
      function text(offset,value){for(var i=0;i<value.length;i++)view.setUint8(offset+i,value.charCodeAt(i));}
      text(0,'RIFF');view.setUint32(4,buffer.byteLength-8,true);text(8,'WAVE');text(12,'fmt ');view.setUint32(16,16,true);view.setUint16(20,1,true);view.setUint16(22,channels,true);view.setUint32(24,rate,true);view.setUint32(28,rate*channels*2,true);view.setUint16(32,channels*2,true);view.setUint16(34,16,true);text(36,'data');view.setUint32(40,frames*channels*2,true);
      var data=[];for(var c=0;c<channels;c++)data.push(audio.getChannelData(c));
      for(var f=0,o=44;f<frames;f++)for(var c=0;c<channels;c++,o+=2){var sample=Math.max(-1,Math.min(1,data[c][f]));view.setInt16(o,sample<0?sample*32768:sample*32767,true);}
      return new Blob([buffer],{type:'audio/wav'});
    }
  },
  ChillMusicAttach__deps: ['$ChillMusicStore'],
  ChillMusicAttach: function (receiver) {
    var target=UTF8ToString(receiver);ChillMusicStore.receiver=target;
    if(!window.chillMusicLifecycleInstalled){
      window.chillMusicLifecycleInstalled=true;
      var save=function () {if(window.chillUnityInstance)window.chillUnityInstance.SendMessage(ChillMusicStore.receiver,'SaveFromBrowser');};
      window.addEventListener('pagehide',save);document.addEventListener('visibilitychange',function(){if(document.hidden)save();});
    }
    // Reconcile IndexedDB after a save import or clearing only localStorage.
    ChillMusicStore.open().then(function(db){var req=db.transaction('tracks').objectStore('tracks').getAll();req.onsuccess=function(){req.result.forEach(function(row){ChillMusicStore.send(target,'PersonalTrackImported',{guid:row.id,title:row.title,artist:row.artist});});};}).catch(function(error){ChillMusicStore.send(target,'PersonalTrackImported',{error:error.message});});
  },
  ChillMusicImport__deps: ['$ChillMusicStore'],
  ChillMusicImport: function (receiver, folder) {
    var target=UTF8ToString(receiver),input=document.createElement('input');
    input.type='file';input.accept='audio/*,.mp3,.ogg,.wav,.m4a,.flac';input.multiple=true;
    if(folder)input.webkitdirectory=true;
    input.onchange=async function () {
      var files=Array.from(input.files||[]).filter(function(file){return /\.(mp3|ogg|wav|m4a|aac|flac|opus)$/i.test(file.name);});
      for(var file of files.slice(0,200)){
        try{
          if(file.size>32*1024*1024)throw new Error(file.name+': maximum 32 MB per music file.');
          var bytes=await file.arrayBuffer(),hash=await crypto.subtle.digest('SHA-256',bytes);
          var id='local-'+Array.from(new Uint8Array(hash)).map(function(n){return n.toString(16).padStart(2,'0');}).join('');
          var tags=ChillMusicStore.tags(bytes,file.name.replace(/\.[^.]+$/,'').slice(0,240));
          await ChillMusicStore.write({id:id,title:tags.title,artist:tags.artist,file:new Blob([bytes],{type:file.type})});
          ChillMusicStore.send(target,'PersonalTrackImported',{guid:id,title:tags.title,artist:tags.artist});
        }catch(error){ChillMusicStore.send(target,'PersonalTrackImported',{error:error.message});}
      }
    };
    input.click();
  },
  ChillMusicLoad__deps: ['$ChillMusicStore'],
  ChillMusicLoad: function (receiver, id, token) {
    var target=UTF8ToString(receiver),key=UTF8ToString(id);
    (async function () {
      var context;
      try {
        var row=await ChillMusicStore.get(key);if(!row)throw new Error('This music file is missing on this device. Import it again.');
        context=new (window.AudioContext||window.webkitAudioContext)();
        var decoded=await context.decodeAudioData(await row.file.arrayBuffer());
        var url=URL.createObjectURL(ChillMusicStore.wave(decoded));
        ChillMusicStore.send(target,'PersonalTrackReady',{guid:key,token:token,url:url});
      }catch(error){ChillMusicStore.send(target,'PersonalTrackReady',{guid:key,token:token,error:error.message||'Cannot decode this audio format.'});}
      finally{if(context)context.close().catch(function(){});}
    })();
  },
  ChillMusicRemove__deps: ['$ChillMusicStore'],
  ChillMusicRemove: function (receiver,id) {
    var target=UTF8ToString(receiver),key=UTF8ToString(id);
    ChillMusicStore.open().then(function(db){return new Promise(function(resolve,reject){var tx=db.transaction('tracks','readwrite');tx.objectStore('tracks').delete(key);tx.oncomplete=resolve;tx.onerror=tx.onabort=function(){reject(new Error('Cannot remove this music file from browser storage.'));};});})
      .then(function(){ChillMusicStore.send(target,'PersonalTrackRemoved',{guid:key});})
      .catch(function(error){ChillMusicStore.send(target,'PersonalTrackRemoved',{guid:key,error:error.message});});
  },
  ChillMusicRevoke: function (url) {var value=UTF8ToString(url);if(value)URL.revokeObjectURL(value);},
  ChillEnterFullscreen: function () {
    if(document.fullscreenElement){document.exitFullscreen().catch(function(){});return;}
    var target=document.querySelector('#unity-container')||document.querySelector('canvas');
    if(target&&target.requestFullscreen)target.requestFullscreen().catch(function(){});
  }
});
