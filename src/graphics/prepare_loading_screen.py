"""Export Boot's loading UI and its original animation curves for the pre-Wasm screen."""
import json, math, re, shutil, sys
from pathlib import Path
from prepare_project import ROOT, split_yaml, meta_guid
sys.path[:0] = [str(ROOT/'.tools/python'), str(ROOT/'research/python')]
import yaml

def prepare(output=None):
    source = ROOT/'Recovered/ExportedProject/Assets'
    output = output or ROOT/'WebPlayer/ui/loading'
    output.mkdir(parents=True, exist_ok=True)
    _, blocks = split_yaml((source/'App/Scenes/Boot.unity').read_text('utf-8-sig'))
    data = {i: (t, next(iter(yaml.safe_load(b).values()))) for t,i,b in blocks}
    objects = {i:d for i,(t,d) in data.items() if t==1}
    rects = {i:d for i,(t,d) in data.items() if t==224}
    graphics = {str(d['m_GameObject']['fileID']):d for t,d in data.values() if t==114 and 'm_Sprite' in d}
    groups = {str(d['m_GameObject']['fileID']):d for t,d in data.values() if t==225}
    scalers = {str(d['m_GameObject']['fileID']):d for t,d in data.values() if t==114 and 'm_ReferenceResolution' in d}
    sprites = {meta_guid(p):p for p in (source/'Sprite').glob('t_ui_loading*.asset')}
    assets = {}
    def vector(v, axes='xy'): return [float(v[a]) for a in axes]
    def node(tid, path=''):
        r=rects[tid];go=objects[str(r['m_GameObject']['fileID'])]
        name=go['m_Name'];path=path+'/'+name if path else name
        result={'name':name, 'path':path, 'active':bool(go['m_IsActive']),
                'anchorMin':vector(r['m_AnchorMin']), 'anchorMax':vector(r['m_AnchorMax']),
                'pivot':vector(r['m_Pivot']), 'position':vector(r['m_AnchoredPosition']),
                'size':vector(r['m_SizeDelta']), 'scale':vector(r['m_LocalScale'])}
        if str(r['m_GameObject']['fileID']) in groups:result['groupAlpha']=groups[str(r['m_GameObject']['fileID'])]['m_Alpha']
        if str(r['m_GameObject']['fileID']) in graphics:
            g=graphics[str(r['m_GameObject']['fileID'])];image={'color':vector(g['m_Color'],'rgba'),'enabled':bool(g['m_Enabled'])}
            ref=g['m_Sprite'];guid=ref.get('guid')
            if guid:
                p=sprites[guid];_,sb=split_yaml(p.read_text('utf-8-sig'));sprite=yaml.safe_load(sb[0][2])['Sprite']
                name=sprite['m_Name'];texture=source/'Texture2D'/f'{name}.png'
                if name!=p.stem:raise ValueError('Unexpected loading sprite name')
                if meta_guid(texture)!=sprite['m_RD']['texture']['guid']:raise ValueError('Loading sprite is not its standalone texture: '+name)
                shutil.copy2(texture,output/f'{name}.png');assets[name]=f'ui/loading/{name}.png';image['sprite']=name
            result['image']=image
        result['children']=[node(str(c['fileID']),path) for c in r['m_Children']]
        return result
    layouts={}
    for tid,r in rects.items():
        name=objects[str(r['m_GameObject']['fileID'])]['m_Name']
        if name in ('LoadingCanvas','LoadingCanvasMobile'):
            parent=next(c for c in r['m_Children'] if objects[str(rects[str(c['fileID'])]['m_GameObject']['fileID'])]['m_Name']=='Parent')
            root=node(str(parent['fileID']))
            def relative(n):
                n['path']=n['path'].removeprefix('Parent/');
                if n['path']=='Parent':n['path']=''
                for c in n['children']:relative(c)
            relative(root)
            scaler=scalers[str(r['m_GameObject']['fileID'])]
            if scaler['m_MatchWidthOrHeight']!=0:raise ValueError('Unsupported loading canvas scale')
            layouts['portrait' if name.endswith('Mobile') else 'landscape']={'width':scaler['m_ReferenceResolution']['x'],'root':root}
    clips={}
    for key,name in [('loop','anim_ui_loading'),('finish','anim_ui_loading_finish'),('fade','anim_ui_loading_fadein'),('errorFinish','anim_ui_loading_finish_error'),('errorFade','anim_ui_loading_fadein_error'),('gameEnd','anim_ui_loading_gameend'),('creditsIn','anim_ui_finish_endcredits_fadein')]:
        _,cb=split_yaml((source/'AnimationClip'/f'{name}.anim').read_text('utf-8-sig'))
        clip=yaml.safe_load(cb[0][2])['AnimationClip'];tracks=[]
        def curve(row, prop, axis=None):
            keys=[]
            for k in row['curve']['m_Curve']:
                v=lambda field:float(k[field][axis] if axis else k[field])
                incoming,outgoing=v('inSlope'),v('outSlope')
                if k.get('weightedMode',0):raise ValueError('Unsupported weighted loading curve')
                keys.append({'time':float(k['time']),'value':v('value'),'inSlope':incoming if math.isfinite(incoming) else 0,
                             'outSlope':outgoing if math.isfinite(outgoing) else 0,'step':not math.isfinite(outgoing) or prop=='active'})
            tracks.append({'path':row['path'],'property':prop,'keys':keys})
        properties={'m_AnchoredPosition.x':'x','m_AnchoredPosition.y':'y','m_Color.a':'alpha','m_IsActive':'active','m_Alpha':'groupAlpha'}
        for row in clip['m_FloatCurves']:
            if row['attribute'] not in properties:raise ValueError('Unknown loading property '+row['attribute'])
            curve(row,properties[row['attribute']])
        for row in clip['m_EulerCurves']:curve(row,'rotation','z')
        for row in clip['m_ScaleCurves']:
            for axis in 'xy':curve(row,'scale'+axis.upper(),axis)
        if clip['m_PositionCurves'] or clip['m_PPtrCurves']:raise ValueError('Unmapped loading tracks')
        clips[key]={'name':name,'duration':clip['m_AnimationClipSettings']['m_StopTime'],'tracks':tracks}
    _, controller = split_yaml((source/'AnimatorController/Loading.controller').read_text('utf-8-sig'))
    transitions={}
    for typ,_,body in controller:
        if typ!=1101:continue
        t=yaml.safe_load(body)['AnimatorStateTransition']
        for condition in t['m_Conditions']:
            if condition['m_ConditionEvent'] in ('SplashFinish','SplashFadeIn'):
                if t['m_TransitionDuration']!=0 or t['m_HasExitTime'] and t['m_ExitTime']!=1:raise ValueError('Unmapped loading transition')
                transitions[condition['m_ConditionEvent']]={'waitLoopEnd':bool(t['m_HasExitTime']),'blendSeconds':t['m_TransitionDuration']}
    manifest={'source':'Boot.unity / Loading.controller','layouts':layouts,'assets':assets,'clips':clips,'transitions':transitions}
    (output/'screen.json').write_text(json.dumps(manifest,separators=(',',':'),allow_nan=False)+'\n','utf-8')
    report={'passed':True,'source':manifest['source'],'layouts':list(layouts),'sprites':len(assets),
            'clips':{k:{'name':v['name'],'seconds':v['duration'],'tracks':len(v['tracks'])} for k,v in clips.items()},
            'originalPixels':True,'originalRectTransforms':True,'originalHermiteKeyframes':True,'transitions':transitions}
    (ROOT/'research/loading-screen-source-check.json').write_text(json.dumps(report,indent=2)+'\n','utf-8')
    return report

if __name__=='__main__':print(json.dumps(prepare()))
