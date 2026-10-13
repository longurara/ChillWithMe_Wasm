"""Finalize only a successful checked release, then verify local HTTP assets."""
import json,re,hashlib,urllib.request
from pathlib import Path
from datetime import datetime
root=Path(__file__).resolve().parent.parent
log=(root/'logs/unity-features-release.log').read_text('utf-8',errors='replace')
match=re.search(r'HLC_CHILL_WEBGL_BUILT .* bytes=(\d+)',log)
if not match or 'HLC_FEATURES_CHECK_PASSED' not in log or 'Application will terminate with return code 0' not in log:
    raise RuntimeError('Checked release has not completed successfully')
report=json.loads((root/'research/features-check.json').read_text('utf-8'))
if not report['passed'] or report['userSaveWritten']:raise RuntimeError('Feature verification failed')
library=json.loads((root/'AssetLibrary/chill-library.json').read_text('utf-8'))
for path in ('UnityProject/Assets/StreamingAssets/chill-library.json','WebPlayer/StreamingAssets/chill-library.json'):
    if json.loads((root/path).read_text('utf-8'))!=library:raise RuntimeError('Catalog mismatch: '+path)
framework=(root/'WebPlayer/Build/WebPlayer.framework.js').read_text('utf-8')
if 'PersonalTrackRemoved' not in framework or 'chill-personal-music' not in framework:raise RuntimeError('Personal music bridge missing from release')
html=(root/'WebPlayer/index.html').read_text('utf-8')
if "productVersion:'1.18.0'" not in html or 'ui/native.css' not in html:raise RuntimeError('Native loader has not been installed')
paths=['/','/ui/native.css','/Build/WebPlayer.loader.js','/Build/WebPlayer.framework.js','/Build/WebPlayer.data','/Build/WebPlayer.wasm','/StreamingAssets/chill-story.json','/StreamingAssets/chill-library.json']
paths+=['/assetpacks/'+a['bundle'] for a in library['assets'] if a['name'] in ('bulbul_systemse_click','bulbul_systemse_cancel','bulbul_systemse_completetask')]
requests=[]
for path in paths:
    with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:8093'+path,method='HEAD'),timeout=10) as response:
        if response.status!=200:raise RuntimeError('HTTP failure: '+path)
        requests.append(dict(path=path,status=response.status,contentType=response.headers.get('Content-Type'),bytes=int(response.headers.get('Content-Length',0))))
status=json.loads((root/'build-status.json').read_text('utf-8'))
status.update(stage='standalone-features-1-3-6-7-8-preview-ready',completedAt=datetime.now().astimezone().isoformat(),productVersion='1.18.0',
    assets=len(library['assets']),bundles=len(library['bundles']),bundleBytes=sum(b['bytes'] for b in library['bundles']),playerBytes=sum(p.stat().st_size for p in (root/'WebPlayer/Build').glob('*')),
    playerBuildLog='logs/unity-features-release.log',featureReleaseBuildBytes=int(match[1]),selectedFeatureGroups=[1,3,6,7,8],
    nativeLanguageControls='En,Ja,ZhHans,ZhHant,Pt,Ko,Ru',nativeMusicTracks=43,nativeAmbientTracks=30,autonomyActionGroups=26,
    soundCueHeaderRepairs=3,nativeAudioChannels=['master','music','voice','sfx','ambient','timer'],nativeTutorialPages=4,nativeTutorialLocales=7,nativeCreditsGroups=15,
    customPlaylists=True,favorites=True,personalMusicIndexedDB=True,personalMusicFilesIncludedInSave=False,autoNextAndSeek=True,automaticActivities=True,characterClickReactions=True,
    tutorialCreditsSkip=True,automaticSceneTime=True,independentAmbientMixer=True,persistentFeatureSettings=True,persistentConversationHistory=True,
    postProfiles='4 original color/bloom/LiftGammaGain profiles adapted to URP; Beautify parity unverified',
    featureCheck='research/features-check.json',personalMusicCheck='research/personal-music-check.json',featureScope='research/features-1-3-6-7-8.md',
    sourceParityAudit='research/source-parity-audit.json',browserStorageEndToEndTested=False,automatedGameplayTests=False,mobileGameplayVerified=False,completeOriginalGameParity=False)
(root/'build-status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
(root/'research/features-http-check.json').write_text(json.dumps(dict(passed=True,requests=requests,gameplayTested=False),indent=2),encoding='utf-8')
readme=root/'README.md';text=readme.read_text('utf-8').replace('230 bundle','233 bundle')
readme.write_text(text,encoding='utf-8')
print(json.dumps(dict(stage=status['stage'],url=status['url'],playerBytes=status['playerBytes'],bundles=status['bundles'],httpPaths=len(requests)),ensure_ascii=False))
