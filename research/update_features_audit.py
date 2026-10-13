import json,re
from datetime import datetime
from pathlib import Path
root=Path(__file__).resolve().parent.parent
path=root/'research/source-parity-audit.json'
data=json.loads(path.read_text('utf-8'))
changes={
8:('Một phần','Đã nối hoạt động nghỉ theo Pomodoro và âm start/end/pause mặc định vào kênh timer. Chưa port toàn bộ chọn giọng báo timer/sound preset.'),
10:('Đã port quản lý playlist web','Tạo/xóa/thêm/xóa/đổi thứ tự, favorites và lưu danh sách. Đổi thứ tự bằng nút; chưa có kéo thả theo UI gốc.'),
11:('Đã port import cho trình duyệt','File/folder lưu IndexedDB, giải mã WAV khi phát và báo lỗi. Metadata file cá nhân dùng tên file; chưa đọc toàn bộ tag ID3. Chưa kiểm tra storage end-to-end trên trình duyệt.'),
12:('Đã port các điều khiển chính','43 bài MusicMaster, seek/auto-next/shuffle/repeat/metadata/khôi phục paused. Chưa tái hiện đầy đủ mọi fade/transition audio nguồn.'),
13:('Một phần mở rộng','Đã có work/break/sleep/drink/wild/leave chair/snack theo controller và thời gian/xác suất nguồn. Chưa đầy đủ tích lũy trọng số AI, đồ ăn hiếm và mọi chuyển tiếp.'),
14:('Đã nối phản ứng nhân vật','Bấm vùng renderer nhân vật, cooldown và các nhóm phản ứng/đánh thức. Hit test là bounds, chưa có mesh/collider picking chính xác hoàn toàn.'),
17:('Một phần mở rộng','Giữ 24 cảnh cửa sổ và thêm 30 ambient độc lập, lưu mức, tự đổi ngày/chiều/đêm theo giờ cấu hình. Chưa port mọi phối hợp ambient/environment riêng của nguồn.'),
21:('Một phần mở rộng','Talk Log/row con, Skip, lịch sử thoại tối đa 200 mục và bảo toàn resume khi hội thoại tự động. Chưa đồng bộ toàn bộ bố cục/stats log nguồn.'),
23:('Đã nối tutorial native','Click Satone, focus timer/note/todo và bốn trang sprite nguồn ở bảy ngôn ngữ. Chưa đối chiếu toàn bộ lượt tutorial trong gameplay trình duyệt.'),
24:('Đã nối credits native','15 nhóm Canvas, fade/hold/delay lấy từ nguồn; có nút Credits và lệnh truyện. Chưa port service Windows/Steam liên quan ending.'),
25:('Một phần mở rộng','Special/collaboration đọc được qua Talk Log; sinh nhật và một số trigger ngày phát hành. Chưa đầy đủ lịch trigger/minigame riêng.'),
27:('Một phần mở rộng','Bảy ngôn ngữ, AI, audio channels, auto-time, 12/24h, quality/FPS/render scale/post/fullscreen và fit UI. Chưa đủ mọi tùy chọn nguồn/plugin OS.'),
28:('Một phần mở rộng','Save web giữ settings/playlist/music position/ambient/birthday/history và giữ resume; đọc save web cũ. Không tương thích ES3 Windows/cloud; JSON không chứa file nhạc cá nhân.'),
30:('Một phần mở rộng','Bốn post profile chuyển thông số Beautify/LiftGammaGain sang URP, giữ đèn bàn gốc. Chưa pixel parity/Cinemachine/MagicaCloth gốc đầy đủ.'),
31:('Một phần mở rộng','Fit khung native, touch/gesture, save khi pagehide/focus, FPS nền và fullscreen theo trình duyệt. Chưa kiểm chứng mobile/Safari trên thiết bị thật.'),
33:('Đã nối các luồng web chính','Chào theo thời gian lần ghé trước, phản ứng focus, tạm biệt và lưu; không đóng tab. Chưa mọi biến thể nguồn/lifecycle Windows.')}
for feature in data['features']:
    if feature['id'] in changes:
        feature['status'],feature['remaining']=changes[feature['id']]
        feature['verification']='research/features-check.json; research/features-1-3-6-7-8.md'
        feature['port']=[]
        names=['ChillStoryRuntime.Features.cs','ChillNativeUI.Features.cs']
        if feature['id'] in (13,14,8):names.append('ChillStoryRuntime.Autonomy.cs')
        for name in names:
            feature['port'].append(dict(path='src/graphics/Runtime/'+name,line=1,anchor='Feature implementation'))
data['date']=datetime.now().astimezone().isoformat()
data['method']='Source masters/controllers/UI comparison; focused Unity Editor state/callback/raycast/render checks and browser audio helper checks. No automated browser gameplay.'
data['portRuntimeCsFiles']=len(list((root/'src/graphics/Runtime').glob('*.cs')))
data['selectedFeatureGroups']=[1,3,6,7,8]
path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
md=['# Đối chiếu chức năng với code nguồn','',data['method'],'','Chi tiết đợt mới: [features-1-3-6-7-8.md](features-1-3-6-7-8.md). Chưa đủ toàn bộ game nguồn.','','| ID | Nhóm | Trạng thái | Phạm vi/còn thiếu |','| --- | --- | --- | --- |']
for f in data['features']:md.append('| '+str(f['id'])+' | '+f['feature']+' | '+f['status']+' | '+f['remaining'].replace('|','/')+' |')
(root/'research/source-parity-audit.md').write_text('\n'.join(md)+'\n',encoding='utf-8')
readme=root/'README.md';text=readme.read_text('utf-8')
text=text.replace('hiển thị 149 bài từ catalog','hiển thị 43 bài từ MusicMaster')
text=text.replace('Nhãn dùng bảng dịch English/Japanese của game.','Nhãn dùng bảng dịch ở bảy ngôn ngữ nguồn (English/Japanese/Chinese Simplified/Chinese Traditional/Portuguese/Korean/Russian).')
text=text.replace('dropdown English/Japanese','dropdown bảy ngôn ngữ nguồn')
text=text.replace('Giao diện đọc hiện hỗ trợ English/Japanese; các bản dịch nguồn khác được giữ trong dữ liệu.','Giao diện đọc hỗ trợ bảy ngôn ngữ nguồn; thiếu bản dịch của từng ID thì fallback English.')
text=text.replace('Nhật ký thoại hiện giữ trong phiên mở trang.','Nhật ký thoại giữ tối đa 200 mục trong save web.')
text=text.replace('AI tự chọn hoạt động ngẫu nhiên và các mini-game riêng chưa được triển khai.','AI tự chọn work/break/wild/leave chair đã nối theo master/controller nguồn; trọng số mong muốn và một số chuyển tiếp/mini-game riêng chưa đầy đủ.')
text=text.replace('AI đổi hoạt động và hậu kỳ/ánh sáng của nguồn chưa đầy đủ.','AI đổi hoạt động và bốn post profile đã nối, nhưng chưa đủ thuật toán AI/hiệu ứng shader của nguồn.')
if '## Các nhóm 1, 3, 6, 7, 8' not in text:
    text+='\n## Các nhóm 1, 3, 6, 7, 8\n\nĐã thêm sinh hoạt tự động của Satone và phản ứng click; playlist/favorites/import nhạc riêng; Skip/tutorial/credits/chào/tạm biệt; bộ trộn 30 ambient, audio channels và giờ tự đổi cảnh; cài đặt đồ họa/FPS/render scale/fit UI và lưu thiết lập. Mở General để điều chỉnh AI/thời gian/ambient, Audio để chỉnh kênh âm, Graphics để đổi chất lượng. Import nhạc cá nhân bằng nút file/folder trong Playlist.\n\nFile nhạc cá nhân lưu IndexedDB của origin, không đi kèm file save JSON. Bài/vị trí khôi phục ở trạng thái tạm dừng. Xem phạm vi, giới hạn và bằng chứng kiểm tra trong `research/features-1-3-6-7-8.md`. Beautify đang được thích nghi sang URP; chưa pixel parity hoặc xác nhận mobile/Safari thực tế.\n'
readme.write_text(text,encoding='utf-8')
print('Updated parity audit and README for feature groups 1, 3, 6, 7, 8.')
