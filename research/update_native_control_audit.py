"""Update the static comparison after the native control hookup."""
import json
from datetime import datetime
from pathlib import Path

root = Path(__file__).resolve().parents[1]
report_path = root / 'research/source-parity-audit.json'
report = json.loads(report_path.read_text('utf-8-sig'))
changes = {
    6: ('Đã port phần điều khiển', 'Có start/pause/reset/skip, số vòng 1–99, phút 1–999 và năm preset lưu trong save web; mặc định 2 vòng, 25/5 phút. Phần liên kết Satone, âm báo và lịch còn thiếu ở mục 8; chưa xác nhận gameplay browser đầy đủ.'),
    7: ('Đã port phần đồng hồ', 'Có chuyển chế độ, đếm lên, start/pause/resume/reset, lưu trạng thái và cộng thời gian làm việc/EXP. Lời thoại và phản ứng Satone theo Countup chưa nối; xem mục 8.'),
    15: ('Một phần', 'Đã nối chọn đọc/viết/gõ trong General và lưu lựa chọn. Chuyển bàn/sofa/giường và controller di chuyển chưa port.'),
    17: ('Một phần', 'Đã nối 4 thời điểm, 24 cảnh cửa sổ và lựa chọn chỉ bầu trời. Thiếu nhiều ambient độc lập, mute/volume từng loại, preset và tự đổi theo giờ thực.'),
    21: ('Một phần', 'Có chương/continue/auto/choices và cửa sổ history native readonly, giữ trong phiên. Mở history tạm dừng auto-next. Skip native vẫn đổi thành Stop; chưa tương đương SkipStory và chưa đủ loại message/trạng thái của nguồn.'),
    27: ('Một phần', 'Đã nối En/Ja, master/music volume và chế độ nhẹ 30/60 FPS. Thiếu voice/ambient/SE/timer volume-mute riêng, tự thoại/âm báo, render scale/quality đầy đủ, giờ/ngày đầu tuần, lưu/reset settings. Ngôn ngữ khác chỉ giữ dữ liệu.'),
    28: ('Một phần', 'Có localStorage/JSON, import/export, 1 backup, timer/Pomodoro preset và thông báo lưu/lỗi trên UI native. Chưa giữ music/settings/profile/preset phòng và các hệ thống chưa port; chưa nhận ES3 Windows/migrate save gốc; chưa có UI khôi phục backup.'),
    30: ('Một phần', 'Shader/effects, camera/presentation/rig đã có; bật lại đèn phụ theo thông số nguồn và kiểm tra đèn bàn bằng ảnh bật/tắt. Chưa tương đương hoàn toàn với Shader Graph/postprocessing/Cinemachine; MagicaCloth thay bằng xoay xương giới hạn; voice distortion thay bằng pitch đơn giản; màu browser chưa xác nhận.'),
    32: ('Một phần', 'Canvas/sprite/font đã phục dựng, padding Level/timer được khôi phục và nút con Talk Log được nối lại. Trạng thái hover/selected của timer theo binding nguồn nhưng chưa có tween. Animator UI tắt; các nút chưa port bị disable. Thiếu open/close animation, hover/click SE, tooltip, lưu vị trí panel và đầy đủ quy tắc khóa theo truyện/timer.'),
}
for feature in report['features']:
    if feature['id'] in changes:
        feature['status'], feature['remaining'] = changes[feature['id']]
    for ref in feature.get('port', []):
        path = root / ref['path']
        if path.exists():
            matches = [i for i, line in enumerate(path.read_text('utf-8-sig').splitlines(), 1) if ref['anchor'] in line]
            if matches:
                ref['line'] = matches[0]
report['updatedAt'] = datetime.now().astimezone().isoformat()
report['nativeControlsCheck'] = 'research/native-controls-check.json'
report['nativeHudCheck'] = 'research/native-hud-check.json'
report['deskLampCheck'] = 'research/desk-lamp-render-check.json'
report['method'] = 'Static source comparison updated after native UI hookup; focused Unity Editor callbacks/renders; no browser gameplay test'
report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), 'utf-8')
lines = ['# Đối chiếu code gốc và bản web — 13/10/2026', '',
         'Đối chiếu tĩnh C# decompile với runtime port; cập nhật sau khi nối các nút native. Kiểm tra callback/dựng ảnh trong Unity Editor, chưa xác nhận gameplay trình duyệt.', '',
         'Nguồn có 1429 file C#. Canvas/asset native và callback web không đồng nghĩa mọi hệ thống của nguồn đã được port.', '',
         'CommandType có 50 giá trị và đủ case; ngữ nghĩa từng case vẫn cần đối chiếu.', '', '## Trạng thái và phần còn thiếu', '']
def links(refs):
    return ', '.join(f"[{Path(r['path']).stem}:{r['line']}]({(root / r['path']).as_posix()}:{r['line']})" for r in refs)
for feature in report['features']:
    lines += [f"### {feature['id']}. {feature['feature']} — {feature['status']}", '', feature['remaining'], '',
              'Nguồn: ' + links(feature.get('original', [])) + '.',
              'Port: ' + links(feature.get('port', [])) + '.', '']
lines += ['## Ưu tiên', '',
          '1. Đã nối UI đọc/viết/gõ, 24 cảnh cửa sổ, En/Ja, history và phản hồi lưu/lỗi; cần người dùng thử bản browser.',
          '2. Hoàn thiện phát nhạc: tua, tự chuyển bài, metadata/playlist và lưu trạng thái.',
          '3. Port AI sinh hoạt, click reaction và liên kết Pomodoro/Countup với Satone.',
          '4. Nhiều trang note/todo list, lịch, thói quen và thống kê.',
          '5. Mở khóa/điểm, tùy biến phòng, sự kiện, settings/mobile và đối chiếu đồ họa.', '',
          '## Giới hạn', '',
          '- Sáu ID localization thiếu trong dữ liệu trích xuất; không tự viết thêm lời thoại.',
          '- Account/cloud/shop/Steam/OS cần phương án web riêng; không gọi backend nhà phát hành.',
          '- Không tính phần trăm hoàn thành từ số file/dòng; chưa xác nhận browser/mobile hoặc màu giống bản Windows.', '']
(root / 'research/source-parity-audit.md').write_text('\n'.join(lines), 'utf-8')
print('Updated native UI status for features:', ', '.join(map(str, changes)))
