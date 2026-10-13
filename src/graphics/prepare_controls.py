"""Extract native desk-object bindings from the original room controller."""
import json, re
from prepare_project import ROOT, split_yaml, field, go_id

source=ROOT/'Recovered/ExportedProject/Assets'
_, blocks=split_yaml((source/'App/Scenes/RoomScene.unity').read_text('utf-8-sig'))
objects={i:field(b,'m_Name') for t,i,b in blocks if t==1}
transforms={i:(go_id(b),re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1)) for t,i,b in blocks if t in (4,224)}
go_transform={g:i for i,(g,p) in transforms.items()}
components={i:(t,b) for t,i,b in blocks}
def path_of(go):
    tid=go_transform.get(go)
    if tid is None:return objects.get(go,'')
    parent=transforms[tid][1]
    return (path_of(transforms[parent][0])+'/' if parent in transforms else '')+objects.get(go,'')
body=next(b for t,i,b in blocks if t==114 and '  _deskObjectAnimator:' in b)
fields={}
for name in ('_handPen','_handPenForSpin','_roomPen','_handNote','_handCop','_roomCop','_coffeeMakerCop','_workBookAnimator','_deskObjectAnimator','_chairAnimator'):
    match=re.search(r'^  '+name+r': \{fileID: (-?\d+)\}',body,re.M)
    if not match or match.group(1)=='0':continue
    t,b=components[match.group(1)]
    go=match.group(1) if t==1 else go_id(b)
    fields[name[1:]]=path_of(go)
# The story controller lives on a different object than its animated held book.
book_ref=re.search(r'^  _bearsRestaurantBook: \{fileID: (-?\d+)\}',body,re.M)
if book_ref and book_ref.group(1)!='0':
    book_body=components[book_ref.group(1)][1]
    animator_ref=re.search(r'^  animator: \{fileID: (-?\d+)\}',book_body,re.M)
    if animator_ref and animator_ref.group(1)!='0':
        fields['bearsRestaurantBookAnimator']=path_of(go_id(components[animator_ref.group(1)][1]))
target=ROOT/'UnityProject/Assets/Prototype/desk-controls.json'
target.write_text(json.dumps(fields,indent=2),encoding='utf-8')
print(json.dumps(fields,indent=2))
