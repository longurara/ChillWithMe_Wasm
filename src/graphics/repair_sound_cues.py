"""Repair zero-length RIFF fields in three recovered PCM copies, never the source export."""
import hashlib,json,struct
from pathlib import Path
root=Path(__file__).resolve().parents[2]
records=[]
for name in ('bulbul_systemse_cancel','bulbul_systemse_click','bulbul_systemse_completetask'):
    path=root/'UnityProject/Assets/Recovered/AudioClip'/(name+'.wav')
    source=root/'Recovered/ExportedProject/Assets/AudioClip'/(name+'.wav')
    raw=bytearray(path.read_bytes())
    if raw[:4]!=b'RIFF' or raw[8:12]!=b'WAVE' or raw[36:40]!=b'data' or len(raw)<44:
        raise ValueError('Unexpected PCM container: '+name)
    if struct.unpack_from('<HH',raw,20)!=(1,2) or struct.unpack_from('<H',raw,34)[0]!=16:
        raise ValueError('Unexpected audio format: '+name)
    struct.pack_into('<I',raw,4,len(raw)-8);struct.pack_into('<I',raw,40,len(raw)-44)
    path.write_bytes(raw)
    records.append(dict(name=name,bytes=len(raw),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),payloadUnchanged=raw[44:]==source.read_bytes()[44:]))
(root/'research/sound-cue-repair.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print('Repaired RIFF/data sizes in 3 project copies; PCM payloads unchanged.')
