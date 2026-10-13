"""Install the loader for the game's restored native Unity canvas."""
from pathlib import Path
import shutil

root = Path(__file__).resolve().parent
source = root / 'src/gameplay-ui'
output = root / 'WebPlayer'
ui = output / 'ui'
ui.mkdir(parents=True, exist_ok=True)
shutil.copy2(source / 'native.css', ui / 'native.css')
html = (source / 'index-native.html').read_text(encoding='utf-8')
(output / 'index.html').write_text(html, encoding='utf-8')
print('Standalone native gameplay UI ready:', output / 'index.html')
