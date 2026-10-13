mergeInto(LibraryManager.library,{
    ChillAvatarImport:function(receiver){
        var target=UTF8ToString(receiver), input=document.createElement('input');input.type='file';input.accept='image/png,image/jpeg,image/webp';
        input.onchange=async function(){var file=input.files[0];if(!file||file.size>16*1024*1024)return;try{
            var bitmap=await createImageBitmap(file),canvas=document.createElement('canvas');canvas.width=canvas.height=256;
            var context=canvas.getContext('2d'), side=Math.min(bitmap.width,bitmap.height);context.drawImage(bitmap,(bitmap.width-side)/2,(bitmap.height-side)/2,side,side,0,0,256,256);bitmap.close();
            var data=canvas.toDataURL('image/png');if(data.length>90000){canvas.width=canvas.height=128;context=canvas.getContext('2d');var smaller=await createImageBitmap(file);context.drawImage(smaller,(smaller.width-side)/2,(smaller.height-side)/2,side,side,0,0,128,128);smaller.close();data=canvas.toDataURL('image/png');}
            window.chillUnityInstance.SendMessage(target,'ProfileIconImported',data);
        }catch(error){console.warn('Avatar import failed:',error.message);}};input.click();
    },
    ChillNotify:function(title,body,request){
        if(!('Notification' in window))return;
        if(request){Notification.requestPermission().catch(function(){});return;}
        if(Notification.permission==='granted'&&document.hidden){try{new Notification(UTF8ToString(title),{body:UTF8ToString(body)});}catch(error){}}
    }
});
