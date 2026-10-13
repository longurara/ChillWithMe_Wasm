mergeInto(LibraryManager.library,{
ChillCancelLoadDirection:function(){if(window.ChillStartup&&window.ChillStartup.cancelDirection)window.ChillStartup.cancelDirection();},
ChillStartupReveal:function(receiver){
  var target=UTF8ToString(receiver);
  if(!window.ChillStartup){console.error('Native startup screen is missing');return;}
  window.ChillStartup.finish().then(function(){window.chillUnityInstance.SendMessage(target,'StartupComplete');}).catch(function(error){window.ChillStartup.fail(error.message);});
},
ChillPlayLoadDirection:function(kind,receiver,token){var target=UTF8ToString(receiver),name=UTF8ToString(kind);var complete=function(){if(window.chillUnityInstance)window.chillUnityInstance.SendMessage(target,"DirectionComplete",String(token));};if(window.ChillStartup&&window.ChillStartup.play)window.ChillStartup.play(name).then(complete).catch(function(error){console.error(error);complete();});else complete();},
ChillStartupFailure:function(message){if(window.ChillStartup)window.ChillStartup.fail(UTF8ToString(message));}
});
