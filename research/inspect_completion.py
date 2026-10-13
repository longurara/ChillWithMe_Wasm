import sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'src/graphics'))
from prepare_story import table
for name in ('GameParameterMaster','UnlockDecorationMaster','DecorationMaster','MusicMaster','HeroineAIMaster','CookieKindMaster','CupcakeKindMaster'):
    data=table(name)
    if name=='GameParameterMaster':print(name,json.dumps(data,ensure_ascii=False)[:15000])
    elif name=='DecorationMaster':print(name,[(k,str(v)[:650]) for k,v in data.items()])
    elif name=='HeroineAIMaster':print(name,json.dumps(data,ensure_ascii=False)[:5000])
    else:print(name,json.dumps(data,ensure_ascii=False)[:1500])
