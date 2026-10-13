import sys, re
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]/'src/graphics'))
from prepare_project import split_yaml, field, go_id, ROOT
_, blocks=split_yaml((ROOT/'UnityProject/Assets/Prototype/Room.unity').read_text('utf-8-sig'))
objects={i:field(b,'m_Name') for t,i,b in blocks if t==1}
transforms={i:(go_id(b),field(b,'m_Father'),b) for t,i,b in blocks if t in (4,224)}
bygo={g:i for i,(g,_,_) in transforms.items()}
def path(tid):
    g,f,b=transforms[tid]; p=re.search(r'fileID: (-?\d+)',f).group(1)
    return (path(p)+'/' if p in transforms else '')+objects[g]
for tid,(g,_,b) in transforms.items():
    p=path(tid)
    if any(x in p for x in ('UI_FacilityNote','UI_FacilityEnviroment','UI_FacilitySetting/General/ScrollView/Viewport/Content')):
        limit=9 if 'UI_FacilityNote' in p else (8 if 'UI_FacilityEnviroment' in p else 10)
        if p.count('/')>limit:continue
        details=' '.join(k+'='+field(b,k) for k in ('m_AnchorMin','m_AnchorMax','m_AnchoredPosition','m_SizeDelta','m_Pivot'))
        comps=[(i,field(cb,'m_Script'),(field(cb,'m_Text') or field(cb,'m_text'))[:60],field(cb,'m_CellSize'),field(cb,'m_ConstraintCount')) for ct,i,cb in blocks if ct==114 and go_id(cb)==g]
        print(p, details, comps)
