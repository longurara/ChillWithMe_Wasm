mergeInto(LibraryManager.library,{
ChillCancelLoadDirection:function(){if(window.ChillStartup&&window.ChillStartup.cancelDirection)window.ChillStartup.cancelDirection();},
ChillStartupReveal:function(receiver){
  var target=UTF8ToString(receiver);
  if(!window.ChillStartup){console.error('Native startup screen is missing');return;}
  window.ChillStartup.finish().then(function(){window.chillUnityInstance.SendMessage(target,'StartupComplete');}).catch(function(error){window.ChillStartup.fail(error.message);});
},
ChillPlayLoadDirection:function(kind,receiver,token){var target=UTF8ToString(receiver),name=UTF8ToString(kind);var complete=function(){if(window.chillUnityInstance)window.chillUnityInstance.SendMessage(target,"DirectionComplete",String(token));};if(window.ChillStartup&&window.ChillStartup.play)window.ChillStartup.play(name).then(complete).catch(function(error){console.error(error);complete();});else complete();},
ChillStartupFailure:function(message){if(window.ChillStartup)window.ChillStartup.fail(UTF8ToString(message));},
ChillStartupWaitPreload:function(receiver){
  var target=UTF8ToString(receiver);
  if(!window.ChillStartup||!window.ChillStartup.waitPreload){
    if(window.chillUnityInstance)window.chillUnityInstance.SendMessage(target,'OnPreloadReady','live');
    return;
  }
  window.ChillStartup.waitPreload().then(function(mode){
    if(window.chillUnityInstance)window.chillUnityInstance.SendMessage(target,'OnPreloadReady',String(mode||'core'));
  }).catch(function(error){
    console.warn('Preload warning:',error);
    if(window.chillUnityInstance)window.chillUnityInstance.SendMessage(target,'OnPreloadReady','core');
  });
},
ChillStartupMountProgress:function(done,total){
  if(window.ChillStartup&&window.ChillStartup.mountProgress)window.ChillStartup.mountProgress(done,total);
},
ChillStartupSetPreloadMode:function(mode){
  if(window.ChillStartup&&window.ChillStartup.setPreloadMode)window.ChillStartup.setPreloadMode(UTF8ToString(mode),true);
}
});
