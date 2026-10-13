"""Install the loader for the game's restored native Unity canvas."""
from pathlib import Path
import shutil

root = Path(__file__).resolve().parent
source = root / 'src/gameplay-ui'
output = root / 'WebPlayer'
ui = output / 'ui'
ui.mkdir(parents=True, exist_ok=True)
shutil.copy2(source / 'native.css', ui / 'native.css')
shutil.copy2(source / 'startup.js', ui / 'startup.js')
import sys
sys.path.insert(0,str(root/'src/graphics'))
from prepare_loading_screen import prepare
prepare(ui/'loading')
html = (source / 'index-native.html').read_text(encoding='utf-8')
(output / 'index.html').write_text(html, encoding='utf-8')
print('Standalone native gameplay UI ready:', output / 'index.html')
