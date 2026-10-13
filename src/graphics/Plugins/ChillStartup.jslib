mergeInto(LibraryManager.library,{
ChillStartupReveal:function(receiver){
  var target=UTF8ToString(receiver);
  if(!window.ChillStartup){console.error('Native startup screen is missing');return;}
  window.ChillStartup.finish().then(function(){window.chillUnityInstance.SendMessage(target,'StartupComplete');}).catch(function(error){window.ChillStartup.fail(error.message);});
},
ChillStartupFailure:function(message){if(window.ChillStartup)window.ChillStartup.fail(UTF8ToString(message));}
});
