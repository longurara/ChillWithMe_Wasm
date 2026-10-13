import sys, re, json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'src/graphics'))
from prepare_project import ROOT,split_yaml,field,go_id

def read(file):
    _, blocks=split_yaml(file.read_text('utf-8-sig'))
    gos={i:b for t,i,b in blocks if t==1}
    ts={i:(go_id(b),b) for t,i,b in blocks if t==224 or t==4}
    def path(i):
        g,b=ts[i];p=re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1)
        return (path(p)+'/' if p in ts else '')+field(gos[g],'m_Name')
    rows={}
    for i,(g,b) in ts.items():
        p=path(i)
        if 'UI_FacilityPomodoro/PomodoroTimer/PomodoroUsing' not in p:continue
        rows[p]={'active':field(gos[g],'m_IsActive'),**{k:field(b,k) for k in ('m_LocalScale','m_AnchorMin','m_AnchorMax','m_AnchoredPosition','m_SizeDelta')}}
        rows[p]['components']=[{'id':j,**{k:field(cb,k) for k in ('m_Script','m_Enabled','m_Sprite','m_Color','m_Type','m_PreserveAspect','m_Alpha','m_FillAmount')}} for ct,j,cb in blocks if ct==114 and go_id(cb)==g]
    return rows
sources={'original':ROOT/'Recovered/ExportedProject/Assets/App/Scenes/RoomScene.unity','port':ROOT/'UnityProject/Assets/Prototype/Room.unity'}
report={k:read(p) for k,p in sources.items()}
(ROOT/'research/timer-layout-source.json').write_text(json.dumps(report,indent=2),'utf-8')
for source,rows in report.items():
    print(source)
    for p,row in rows.items():
        if 'PomodoroInterface' in p: print(p.split('PomodoroUsing/')[1],json.dumps(row))
