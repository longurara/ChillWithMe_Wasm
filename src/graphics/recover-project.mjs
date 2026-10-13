import {readFile,writeFile,mkdir,access} from 'node:fs/promises';
import {createReadStream,createWriteStream} from 'node:fs';
import {Readable} from 'node:stream';
import {pipeline} from 'node:stream/promises';
import {createHash} from 'node:crypto';
import {spawn,execFileSync} from 'node:child_process';
import {createServer} from 'node:net';
import path from 'node:path';

const root=path.resolve(import.meta.dirname,'../..');
const source=process.argv[2];
if(!source)throw Error('Usage: node src/graphics/recover-project.mjs <Windows game folder>');
await access(path.join(source,'Chill With You_Data/Managed/Assembly-CSharp.dll'));
const work=path.join(root,'logs'),tools=path.join(root,'.tools');
const output=path.join(root,'Recovered');
try{await access(path.join(output,'ExportedProject/Assets/App/Scenes/RoomScene.unity'));console.log('Recovered project already exists: '+output);process.exit(0);}catch{}
await mkdir(work,{recursive:true});await mkdir(tools,{recursive:true});await mkdir(output,{recursive:true});
const archive=path.join(tools,'AssetRipper_win_x64.zip');
try{await access(archive);}catch{
  const response=await fetch('https://github.com/AssetRipper/AssetRipper/releases/download/2.0.0/AssetRipper_win_x64.zip');
  if(!response.ok)throw Error('AssetRipper download failed: '+response.status);
  await pipeline(Readable.fromWeb(response.body),createWriteStream(archive));
}
const hash=createHash('sha256');for await(const chunk of createReadStream(archive))hash.update(chunk);
if(hash.digest('hex')!=='9a7ef0e7c5c3ea5b90b4e6d855e2d98d5f7ec8c3f9e26fccbc194c6a7b01baf7')throw Error('AssetRipper archive hash mismatch');
const exe=path.join(tools,'assetripper/AssetRipper.GUI.Free.exe');
try{await access(exe);}catch{
  const quote=value=>"'"+value.replaceAll("'","''")+"'";
  execFileSync('powershell.exe',['-NoProfile','-Command',`Expand-Archive -LiteralPath ${quote(archive)} -DestinationPath ${quote(path.dirname(exe))} -Force`],{stdio:'inherit',windowsHide:true});
}
const server=createServer();await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const port=server.address().port;await new Promise(resolve=>server.close(resolve));
const logFile=path.join(work,'assetripper.log');
const child=spawn(exe,['--headless','--port',String(port),'--log-path',logFile],{cwd:path.dirname(exe),windowsHide:true,stdio:'ignore'});
const base='http://127.0.0.1:'+port;
try{
  let ready=false;
  for(let attempt=0;attempt<60;attempt++){
    try{const response=await fetch(base+'/openapi.json');if(response.ok){ready=true;break;}}catch{}
    if(child.exitCode!==null)throw Error('AssetRipper exited before startup');
    await new Promise(resolve=>setTimeout(resolve,250));
  }
  if(!ready)throw Error('AssetRipper server did not start');
  const settings=new URLSearchParams({BundledAssetsExportMode:'GroupByAssetType',ScriptContentLevel:'Level2',AudioExportFormat:'Default',ImageExportFormat:'Png',LightmapTextureExportFormat:'Exr',SpriteExportMode:'Yaml',ShaderExportMode:'Dummy',TextExportMode:'Parse',ScriptLanguageVersion:'CSharp9_0',ScriptExportMode:'Hybrid',PreferOriginalTextureExtension:'true',DefaultVersion:'2022.3.62f2',TargetVersion:'0.0.0a0'});
  const configured=await fetch(base+'/Settings/Update',{method:'POST',body:settings});
  if(!configured.ok)throw Error('AssetRipper configuration failed');
  console.log('Loading Windows data. Progress log: '+logFile);
  const loaded=await fetch(base+'/LoadFolder',{method:'POST',body:new URLSearchParams({path:path.resolve(source)})});
  if(!loaded.ok)throw Error('AssetRipper load failed');
  console.log('Exporting scenes, rigs, animations, textures, audio and scripts.');
  const exported=await fetch(base+'/Export/UnityProject',{method:'POST',body:new URLSearchParams({path:output})});
  if(!exported.ok)throw Error('AssetRipper export failed');
  await access(path.join(output,'ExportedProject/Assets/App/Scenes/RoomScene.unity'));
  await writeFile(path.join(output,'recovery-source.json'),JSON.stringify({source:path.resolve(source),assetripper:'2.0.0',sha256:'9a7ef0e7c5c3ea5b90b4e6d855e2d98d5f7ec8c3f9e26fccbc194c6a7b01baf7',unity:'2022.3.62f2'},null,2));
  console.log('Recovered project: '+output);
}finally{child.kill();}
