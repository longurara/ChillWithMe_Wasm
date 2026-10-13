"""Record a successfully checked standalone release and HTTP/browser evidence."""
import json,re,sys,urllib.request
from pathlib import Path
from datetime import datetime
root=Path(__file__).resolve().parents[1]
logname=sys.argv[1] if len(sys.argv)>1 else 'logs/unity-completion-release13.log'
log=(root/logname).read_text(encoding='utf8',errors='replace')
assert 'HLC_COMPLETION_CHECK_PASSED' in log and 'HLC_FEATURES_CHECK_PASSED' in log
match=re.search(r'HLC_CHILL_WEBGL_BUILT .+ bytes=(\d+)',log);assert match,'No successful player build'
version=re.search(r"productVersion:\s*'([^']+)'",(root/'WebPlayer/index.html').read_text(encoding='utf8')).group(1)
browser=json.loads((root/'research/browser-gameplay-check.json').read_text());assert browser['passed']
catalog=json.loads((root/'WebPlayer/StreamingAssets/chill-library.json').read_text())
paths=['/','/ui/native.css','/Build/WebPlayer.loader.js','/Build/WebPlayer.framework.js','/Build/WebPlayer.wasm','/Build/WebPlayer.data','/StreamingAssets/chill-story.json','/StreamingAssets/chill-library.json']
if 'HLC_STARTUP_CHECK_PASSED' in log:
 paths+=['/ui/startup.js','/ui/loading/screen.json','/ui/loading/t_ui_loading_icon.png','/ui/loading/t_ui_loading_phone_active.png','/ui/loading/t_ui_loading_halo.png','/ui/loading/t_ui_loading_logo.png']
paths+=['/assetpacks/'+b['name'] for b in catalog['bundles'][:3]]
http=[]
for path in paths:
 with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:8093'+path,method='HEAD'),timeout=15) as response:
  assert response.status==200
  http.append({'path':path,'status':response.status,'bytes':response.headers.get('Content-Length'),'type':response.headers.get('Content-Type')})
(root/'research/completion-http-check.json').write_text(json.dumps({'passed':True,'checks':http},indent=2)+'\n')
p=root/'build-status.json';status=json.loads(p.read_text());status.update(stage='standalone-completion-browser-preview-ready',completedAt=datetime.now().astimezone().isoformat(),productVersion=version,playerBuildLog=logname,featureReleaseBuildBytes=int(match.group(1)),playerBytes=sum(f.stat().st_size for f in (root/'WebPlayer/Build').iterdir() if f.is_file()),automatedGameplayTests=True,browserGameplayCheck='research/browser-gameplay-check.json',browserStorageEndToEndTested=True,completionCheck='research/completion-check.json',completionHttpCheck='research/completion-http-check.json',cloudDeferredByUser=True,multiPageNotes=True,multipleTodoLists=True,calendarDiary=True,habits=True,dailyWeeklyMonthlyYearlyStatistics=True,roomPresets=True,sourceCostumes=True,localProfileAvatar=True,independentSpecialProgress=True,decorationRestoreDoesNotMutateUI=True,completeOriginalGameParity=False)
status['browserGameplayCheckVersion']=browser.get('productVersion')
physics_browser=root/'research/browser-physics-check.json'
if 'HLC_PHYSICS_CHECK_PASSED' in log:
 assert json.loads((root/'research/physics-check.json').read_text())['passed']
 assert 'HLC_PHYSICS_BAKE_PASSED' in log and json.loads((root/'research/physics-bake-check.json').read_text())['passed']
 assert physics_browser.is_file(),'Record browser physics evidence before finalizing the release'
 evidence=json.loads(physics_browser.read_text());assert evidence['passed'] and evidence['productVersion']==version and evidence['finalBuildLog']==logname
 status.update(physicsFixedHz=60,physicsRigCheck='research/physics-check.json',physicsBakeCheck='research/physics-bake-check.json',latestBrowserCheck='research/browser-physics-check.json',physicsSourceProfile='research/physics-source-profile.json',physicsNativeParityVerified=False,physicsPhysicalMobileVerified=False)
if 'HLC_WINDOW_COAT_CHECK_PASSED' in log:
 for name,marker in [('window-coat-check','HLC_WINDOW_COAT_CHECK_PASSED'),('pane-layout-check','HLC_PANE_LAYOUT_CHECK_PASSED'),('hover-check','HLC_HOVER_CHECK_PASSED'),('story-flow-check','HLC_STORY_FLOW_CHECK_PASSED'),('break-props-check','HLC_BREAK_PROPS_CHECK_PASSED')]:
  assert marker in log and json.loads((root/'research'/f'{name}.json').read_text())['passed'],name
 if 'HLC_STARTUP_CHECK_PASSED' not in log:
  story_ui=json.loads((root/'research/browser-story-ui-check.json').read_text())
  assert story_ui['passed'] and story_ui['productVersion']==version and story_ui['finalBuildLog']==logname
  status['latestBrowserCheck']='research/browser-story-ui-check.json'
 status.update(storyFlowCheck='research/story-flow-check.json',paneLayoutCheck='research/pane-layout-check.json',hoverCheck='research/hover-check.json',windowCoatCheck='research/window-coat-check.json')
if 'HLC_STARTUP_CHECK_PASSED' in log:
 for name in ['startup-gate-check','loading-screen-source-check']:
  assert json.loads((root/'research'/f'{name}.json').read_text())['passed']
 startup=json.loads((root/'research/browser-startup-check.json').read_text())
 assert startup['passed'] and startup['productVersion']==version and startup['finalBuildLog']==logname
 status.update(originalStartupScreen=True,startupWaitsForRoomReady=True,startupGateCheck='research/startup-gate-check.json',startupSourceCheck='research/loading-screen-source-check.json',latestBrowserCheck='research/browser-startup-check.json')
p.write_text(json.dumps(status,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
for name in ['bridge-check.js','profile-bridge-check.js']:
 helper=root/'WebPlayer/ui'/name
 if helper.is_file() and helper.resolve().is_relative_to(root.resolve()):helper.unlink()
print('Release ready:',status['productVersion'],status['url'],'HTTP checks:',len(http))
