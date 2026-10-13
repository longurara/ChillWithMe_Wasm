import sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'src/graphics'))
from prepare_features import component
from prepare_story import table
for name in ('CostumeChangeService','CostumeSkinChanger','HairSource','SpecialSelectListUI','TimerSoundTypeChangeButton','HabitItemDateUI'):
    print(name,json.dumps(component(name),ensure_ascii=False)[:7000])
for name in ('CostumeSkinDB','CostumeModelDB'):
    print(name,json.dumps(table(name),ensure_ascii=False)[:7000])
