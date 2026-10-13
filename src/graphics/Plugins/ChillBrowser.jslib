mergeInto(LibraryManager.library,{
ChillReadSave:function(){var text="";try{text=localStorage.getItem("chill-with-you-save-v1")||""}catch(_){}var pointer=_malloc(lengthBytesUTF8(text)+1);stringToUTF8(text,pointer,lengthBytesUTF8(text)+1);return pointer},
ChillWriteSave:function(value){try{var text=UTF8ToString(value),key="chill-with-you-save-v1";var old=localStorage.getItem(key);localStorage.setItem(key,text);if(old){try{localStorage.setItem(key+".backup",old)}catch(_){}}return 1}catch(_){return 0}},
ChillExportSave:function(value){var url=URL.createObjectURL(new Blob([UTF8ToString(value)],{type:"application/json"}));var anchor=document.createElement("a");anchor.href=url;anchor.download="chill-with-you-save.json";document.body.appendChild(anchor);anchor.click();anchor.remove();setTimeout(function(){URL.revokeObjectURL(url)},1e4)},
ChillImportSave:function(receiver){var target=UTF8ToString(receiver),input=document.createElement("input");input.type="file";input.accept=".json,application/json";input.onchange=function(){var file=input.files&&input.files[0];if(!file)return;if(file.size>1024*1024){window.chillUnityInstance.SendMessage(target,"ImportSaveError","File save quá lớn.");return}file.text().then(function(text){window.chillUnityInstance.SendMessage(target,"ImportSave",text)}).catch(function(){window.chillUnityInstance.SendMessage(target,"ImportSaveError","Không đọc được file save.")})};input.click()},
ChillBindUI:function(receiver){if(window.chillUI)window.chillUI.bind(UTF8ToString(receiver))},
ChillPublishUI:function(state){if(window.chillUI)window.chillUI.update(JSON.parse(UTF8ToString(state)))},
ChillIsMobileBrowser:function(){return/Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent)||navigator.platform==="MacIntel"&&navigator.maxTouchPoints>1?1:0}
});
