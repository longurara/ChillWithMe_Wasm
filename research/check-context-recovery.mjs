import assert from 'node:assert/strict';
import {installContextRecovery} from '../src/gameplay-ui/context-recovery.mjs';
const canvas=new EventTarget(),calls=[],messages=[],values=new Map();let restarts=0,time=100000;
const controller=installContextRecovery({canvas,getInstance:()=>({SendMessage:(...args)=>calls.push(args)}),notify:(...args)=>messages.push(args),reload:()=>restarts++,now:()=>time,storage:{getItem:k=>values.get(k),setItem:(k,v)=>values.set(k,v)}});
const lost=new Event('webglcontextlost',{cancelable:true});canvas.dispatchEvent(lost);assert.equal(lost.defaultPrevented,true);assert.equal(calls[0][1],'SaveFromBrowser');assert.equal(calls[1][1],'BrowserVisibility');canvas.dispatchEvent(new Event('webglcontextrestored'));assert.equal(restarts,1);
time+=1000;canvas.dispatchEvent(new Event('webglcontextlost',{cancelable:true}));canvas.dispatchEvent(new Event('webglcontextrestored'));assert.equal(restarts,1);messages.at(-1)[1]();assert.equal(restarts,2);
controller.dispose();canvas.dispatchEvent(new Event('webglcontextrestored'));assert.equal(restarts,2);
const denied=new EventTarget();const deniedController=installContextRecovery({canvas:denied,getInstance:()=>null,notify:(...args)=>messages.push(args),reload:()=>restarts++,storage:{getItem(){throw new Error('denied');}}});denied.dispatchEvent(new Event('webglcontextlost',{cancelable:true}));denied.dispatchEvent(new Event('webglcontextrestored'));assert.equal(restarts,2);messages.at(-1)[1]();assert.equal(restarts,3);deniedController.dispose();
console.log(JSON.stringify({passed:true,saveBeforeRestart:true,preventRestoreLoop:true,manualRetry:true,removedListeners:true}));
