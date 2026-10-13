"""Update the source comparison after the story and daily-life fixes."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
changes={
 11:("Đã port import cho trình duyệt", "File/folder, metadata cơ bản, giải mã audio và IndexedDB đã kiểm tra trong Chrome riêng. Chưa đọc toàn bộ định dạng tag/plugin nhạc của Windows."),
 13:("Đã mở rộng, còn khác nguồn", "Work/break/wild/rời ghế, trà/ăn và mesh đồ ăn hiếm; thêm sofa có trọng số/cooldown, buồn ngủ tăng/giảm theo master, giữ tư thế nghe nhạc, giới hạn nghỉ PC theo bàn. Chặn thoại chen vào chuỗi hoạt động; thêm lời mời đọc tập mới khi nghỉ. Chưa tương đương toàn bộ AI, chuyển tiếp và readiness của đồ vật trong engine gốc."),
 21:("Đã sửa bố cục/lớp thoại theo nguồn", "Tách MainStoryTextParent và NormalTextParent theo Scenario; lời chào/phản ứng ở trên thanh nhạc. Đóng cửa sổ trả nhóm UI về thứ tự scene gốc. Truyện dài ẩn đúng 11 nhóm HUD từ UIShowManagerForPC, giữ nền/nút thoại và History, kết thúc trả lại trạng thái HUD trước đó. Tutorial còn giữ controls cho adapter web, chưa port toàn bộ pha ẩn/hiện tutorial. Talk Log, Skip/lịch sử và bảo toàn resume được nối; chưa toàn bộ stats log nguồn."),
 22:("Đã sửa các lệnh chờ/thoại/nhạc", "Auto chờ chữ và voice kết thúc rồi mới tính Arg2; tốc độ chữ đọc master từng ngôn ngữ. Glitch/ScreenMove 200/205 dừng sau Arg2, -1 giữ liên tục, resume không khôi phục hiệu ứng đã hết hạn. Command 25 chỉ dùng trong Talk Log; 50 chờ click controller và hiện guide nguồn. Command 22 nhận PlayPomodoroVoice từ animation, khớp loại cue và bỏ cue cũ khi hủy. BGM dùng ratio/offset/fade, resume theo scene history; command 26 chọn playlist. Live story/Talk Log được lưu riêng. Chưa xác nhận mọi tập và mọi tổ hợp hiệu ứng, chưa port đầy đủ pause/tag của Febucci; scenario BGM vẫn dùng chung kênh với playlist trong adapter web.")
}
path=root/'research/source-parity-audit.json'
data=json.loads(path.read_text(encoding='utf8'))
for feature in data['features']:
 if feature['id'] in changes:
  feature['status'],feature['remaining']=changes[feature['id']]
 if feature['id'] in (13,21,22):
  feature['verification']=['research/dialogue-layout-check.json','research/story-command-check.json','research/daily-life-check.json','research/browser-story-daily-check.json']
  if feature['id']==21:feature['verification'].append('research/browser-story-layer-check.json')
  if feature['id'] in (21,22):feature['verification']+=['research/story-presentation-check.json','research/browser-story-presentation-check.json']
data['portRuntimeCsFiles']=len(list((root/'src/graphics/Runtime').glob('*.cs')))
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
path=root/'research/source-parity-audit.md'
lines=path.read_text(encoding='utf8').splitlines()
for i,line in enumerate(lines):
 for key,(status,remaining) in changes.items():
  if line.startswith(f'| {key} |'):
   pieces=line.split('|');pieces[3]=' '+status+' ';pieces[4]=' '+remaining+' ';lines[i]='|'.join(pieces)
path.write_text('\n'.join(lines)+'\n',encoding='utf8')
