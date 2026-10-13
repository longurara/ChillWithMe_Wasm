"""Use lilToon's non-DOTS SM3.5 passes on both Editor and WebGL.

UsePass wrappers otherwise inherit the first, SM4.5-only pass and WebGL
silently selects URP/Unlit. Keep vendor GUIDs and the original shading code.
"""
import re
from prepare_project import ROOT

plugin = ROOT/'UnityProject/Assets/lilToon'
importer = plugin/'Editor/lilShaderContainerImporter.cs'
text = importer.read_text('utf-8-sig')
old = '                sb.AppendLine(sb1.ToString());\n                sb.AppendLine();'
marker = '                // Chill WebGL: emit only the non-DOTS SM3.5 subshader.'
if old in text:
    importer.write_text(text.replace(old, marker, 1), encoding='utf-8')
elif marker not in text:
    raise RuntimeError('Unknown lilToon importer; review SM3.5 patch before building.')

# The vendor build callback rewrites UsePass shaders with optimized variants
# immediately before packing. Keep the same prepared shaders as the captures.
processor = plugin/'Editor/lilToonEditorUtils.cs'
text = processor.read_text('utf-8-sig')
for method in ('OnPreprocessBuild', 'OnPostprocessBuild'):
    head = 'public void '+method+'(UnityEditor.Build.Reporting.BuildReport report)\n        {\n'
    guard = '            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL) return; // Chill: preserve prepared toon passes.\n'
    if head+guard in text: continue
    if head not in text: raise RuntimeError('Unknown lilToon build processor: '+method)
    text = text.replace(head, head+guard, 1)
processor.write_text(text, encoding='utf-8')

def closing_brace(text, start):
    depth = 0
    token = re.compile(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"|[{}]', re.S)
    for match in token.finditer(text, start):
        if match.group() == '{': depth += 1
        elif match.group() == '}':
            depth -= 1
            if depth == 0: return match.end()
    raise RuntimeError('Unbalanced shader subshader.')

changed = 0
for path in (plugin/'Shader').glob('*.shader'):
    text = path.read_text('utf-8-sig')
    match = re.search(r'SubShader\s*\{\s*Tags\s*\{[^}]*"ShaderModel"\s*=\s*"4\.5"[^}]*\}', text)
    if not match: continue
    end = closing_brace(text, text.index('{', match.start()))
    fallback = re.match(r'\s*Fallback\s+"Universal Render Pipeline/Unlit"', text[end:])
    if fallback: end += fallback.end()
    remaining = text[:match.start()] + text[end:]
    if not re.search(r'SubShader\s*\{', remaining):
        raise RuntimeError('No WebGL subshader left: '+str(path))
    path.write_text(remaining, encoding='utf-8')
    changed += 1
print('lilToon WebGL SM3.5 patch ready; updated shader files:', changed)
