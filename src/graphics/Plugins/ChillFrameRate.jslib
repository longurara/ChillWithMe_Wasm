mergeInto(LibraryManager.library,{
ChillSetBrowserFrameRate__deps:['emscripten_set_main_loop_timing'],
ChillSetBrowserFrameRate:function(fps){_emscripten_set_main_loop_timing(0,Math.max(1,Math.round(1000/fps)));}
});
