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
paths+=['/assetpacks/'+b['name'] for b in catalog['bundles'][:3]]
http=[]
for path in paths:
 with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:8093'+path,method='HEAD'),timeout=15) as response:
  assert response.status==200
  http.append({'path':path,'status':response.status,'bytes':response.headers.get('Content-Length'),'type':response.headers.get('Content-Type')})
(root/'research/completion-http-check.json').write_text(json.dumps({'passed':True,'checks':http},indent=2)+'\n')
p=root/'build-status.json';status=json.loads(p.read_text());status.update(stage='standalone-completion-browser-preview-ready',completedAt=datetime.now().astimezone().isoformat(),productVersion=version,playerBuildLog=logname,featureReleaseBuildBytes=int(match.group(1)),playerBytes=sum(f.stat().st_size for f in (root/'WebPlayer/Build').iterdir() if f.is_file()),automatedGameplayTests=True,browserGameplayCheck='research/browser-gameplay-check.json',browserStorageEndToEndTested=True,completionCheck='research/completion-check.json',completionHttpCheck='research/completion-http-check.json',cloudDeferredByUser=True,multiPageNotes=True,multipleTodoLists=True,calendarDiary=True,habits=True,dailyWeeklyMonthlyYearlyStatistics=True,roomPresets=True,sourceCostumes=True,localProfileAvatar=True,independentSpecialProgress=True,decorationRestoreDoesNotMutateUI=True,completeOriginalGameParity=False)
p.write_text(json.dumps(status,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
for name in ['bridge-check.js','profile-bridge-check.js']:
 helper=root/'WebPlayer/ui'/name
 if helper.is_file() and helper.resolve().is_relative_to(root.resolve()):helper.unlink()
print('Release ready:',status['productVersion'],status['url'],'HTTP checks:',len(http))
