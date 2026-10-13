mergeInto(LibraryManager.library,{
ChillReadBackup:function(){var text="";try{text=window.ChillSaveStore.backup()}catch(_){}var pointer=_malloc(lengthBytesUTF8(text)+1);stringToUTF8(text,pointer,lengthBytesUTF8(text)+1);return pointer},
ChillReadSave:function(){var text="";try{text=window.ChillSaveStore.load()}catch(_){}var pointer=_malloc(lengthBytesUTF8(text)+1);stringToUTF8(text,pointer,lengthBytesUTF8(text)+1);return pointer},
ChillWriteSave:function(value){return window.ChillSaveStore?window.ChillSaveStore.write(UTF8ToString(value)):0},
ChillCanWriteSave:function(){return window.ChillSaveStore&&window.ChillSaveStore.canWrite?1:0},
ChillExportSave:function(value){var url=URL.createObjectURL(new Blob([UTF8ToString(value)],{type:"application/json"}));var anchor=document.createElement("a");anchor.href=url;anchor.download="chill-with-you-save.json";document.body.appendChild(anchor);anchor.click();anchor.remove();setTimeout(function(){URL.revokeObjectURL(url)},1e4)},
ChillImportSave:function(receiver){var target=UTF8ToString(receiver),input=document.createElement("input");input.type="file";input.accept=".json,application/json";input.onchange=function(){var file=input.files&&input.files[0];if(!file)return;if(file.size>32*1024*1024){window.chillUnityInstance.SendMessage(target,"ImportSaveError","Save file is too large (maximum 32 MB).");return}file.text().then(function(text){window.chillUnityInstance.SendMessage(target,"ImportSave",text)}).catch(function(){window.chillUnityInstance.SendMessage(target,"ImportSaveError","Could not read the save file.")})};input.click()},
ChillImportPCSave:function(receiver){var target=UTF8ToString(receiver);import(new URL("ui/pc-save.mjs",document.baseURI).href).then(function(module){module.choosePCSave(target,function(r,m,v){if(window.chillUnityInstance)window.chillUnityInstance.SendMessage(r,m,v)});}).catch(function(error){if(window.chillUnityInstance)window.chillUnityInstance.SendMessage(target,"ImportSaveError",error.message)});},
ChillBindUI:function(receiver){if(window.chillUI)window.chillUI.bind(UTF8ToString(receiver))},
ChillPublishUI:function(state){if(window.chillUI)window.chillUI.update(JSON.parse(UTF8ToString(state)))},
ChillIsMobileBrowser:function(){return/Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent)||navigator.platform==="MacIntel"&&navigator.maxTouchPoints>1?1:0}
});
