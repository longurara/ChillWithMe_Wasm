> Ghi chép đợt 1.18. Các nhóm ghi chú/công việc/lịch/thói quen/profile/trang trí đã được mở rộng trong 1.19; xem source-parity-audit.md và completion-check.json.

# Bản port các nhóm 1, 3, 6, 7, 8

Đầu vào là code, controller, master và Canvas gốc trong `Recovered/ExportedProject/Assets`; thư mục nguồn chỉ được đọc. Bản thử chạy riêng tại http://127.0.0.1:8093/.

## 1. Sinh hoạt Satone

Đã nối ba hoạt động làm việc, sáu hoạt động nghỉ, biến thể chuyển động, ngủ/thức, uống trà, duỗi người, rời ghế/sofa/bếp, quay về với đồ ăn và phản ứng khi bấm Satone. Dùng controller, animation/event/đạo cụ gốc. Thời gian đổi hoạt động, xác suất tự nói/rời ghế và cooldown lấy từ master/controller nguồn. Pomodoro nghỉ tự chuyển sang hoạt động nghỉ; General có công tắc AI, tự nói và Work/Break.

Chưa tái hiện hoàn toàn bộ tích lũy trọng số mong muốn của AI nguồn: biến thể hiện chọn ngẫu nhiên trong nhóm, một số loại đồ ăn hiếm/chuyển tiếp vẫn chưa có. Không khẳng định giống mọi chuỗi hành động của bản Windows.

## 3. Nhạc

Playlist chỉ liệt kê **43 bài của MusicMaster**, có tên bài/tác giả gốc; không trộn mọi AudioClip trong catalog. Có seek, tự chuyển bài, shuffle tránh lặp ngay, repeat, favorites, tạo/xóa playlist, thêm/xóa/đổi thứ tự bài, lưu bài/vị trí và khôi phục ở trạng thái tạm dừng. Truyện dùng BGM riêng rồi trả lại bài đang nghe; chỉnh volume truyện không ghi đè lựa chọn nhạc người dùng.

Nút Import file/folder dùng hộp chọn file trình duyệt. File cá nhân lưu trong IndexedDB của origin, chuyển đổi sang PCM WAV khi phát và giải phóng clip trước khi đổi bài. Giới hạn 200 file, 32 MB/file, 10 phút/bài và 128 MB PCM giải mã; lỗi thiếu file/bộ nhớ/quyền lưu được báo trên UI. Hỗ trợ định dạng mà Web Audio trên trình duyệt hiện tại giải mã được.

Save JSON chỉ chứa metadata, danh sách và tiến trình; **không chứa file nhạc cá nhân**. Đổi trình duyệt/origin hoặc xóa dữ liệu site phải nhập lại file. Folder import tùy hỗ trợ của trình duyệt.

## 6. Truyện và các luồng phụ

Đã nối Skip, tutorial click nhân vật/focus timer-note-todo và bốn trang hướng dẫn bằng sprite nguồn ở bảy ngôn ngữ; credits dùng 15 nhóm Canvas gốc và thời gian fade/hold nguồn. Có chào khi mở game, phản ứng chuyển focus, lời tạm biệt và sinh nhật theo ngày nhập trong General. Hội thoại tự động giữ lại chương đang đọc dở; nhật ký thoại lưu tối đa 200 mục trong save.

Các chương special/collaboration nguồn có thể chọn đọc trong Talk Log. Trigger tự động mới nối sinh nhật và một số sự kiện có ngày phát hành chính xác trong nguồn; chưa đủ mọi lịch sự kiện. Nút tạm biệt lưu game, không đóng tab. Không nối Steam achievement/cloud hoặc dịch vụ Windows.

## 7. Âm thanh và thời gian

Có master, music, voice, SFX, ambient và timer riêng. Âm thao tác/lên cấp/timer dùng GUID của 12 SystemSe và 7 TimerSe nguồn; bản này dùng các âm mặc định start/end/pause, chưa nối toàn bộ tùy chọn giọng báo timer. Bộ trộn 30 ambient phát nhiều nguồn đồng thời, mute từng nguồn/stop tất cả và lưu mức âm lượng. Bundle tải khi cần, không giải mã toàn bộ thư viện audio vào RAM.

General có tự đổi ngày/chiều/đêm theo đồng hồ máy, giờ ranh giới cấu hình và đồng hồ 12/24 giờ. Truyện vẫn ưu tiên cảnh và âm thanh kịch bản khi đang đọc.

## 8. Đồ họa, UI và save

Có Low/Medium/High, FPS active/background, render scale, post-processing, fullscreen và fit cửa sổ native theo viewport. Bốn VolumeProfile lấy brightness/contrast/saturation/bloom và LiftGammaGain từ nguồn; Beautify được thích nghi sang URP, **chưa bảo đảm màu/hiệu ứng giống từng pixel bản Windows**. Giữ đèn bàn PerPixel và lightmap đã chuyển đổi trước đó. Safari gesture zoom/touch scrolling trên canvas bị chặn.

Save web lưu thiết lập mới, nhạc, ambient, ngày sinh và lịch sử thoại; đọc save web cũ, kiểm tra dữ liệu nhập và lưu khi mất focus/pagehide. Không tương thích trực tiếp ES3 Windows. Solver tóc/áo vẫn là mô phỏng CPU giới hạn, chưa thay bằng MagicaCloth nguồn. Mobile/Safari trên thiết bị thật và toàn bộ gameplay chưa được kiểm chứng.

## Bằng chứng kiểm tra

- `features-source-data.json`: 43 bài, 30 ambient, 26 nhóm hành động và các policy nguồn.
- `features-check.json`: playlist/favorites/seek target, settings/save migration, từ chối dữ liệu không hợp lệ, tutorial, native raycast và 17 đường dẫn animation. Render các bảng; không ghi save người dùng, không chạy gameplay trình duyệt.
- `personal-music-check.json`: WAV header, stereo/interleaving, clipping, giới hạn PCM và lỗi file mất. Chưa kiểm tra IndexedDB end-to-end trên trình duyệt.
- `logs/unity-features-release.log`: kết quả build WebGL và kiểm tra shader của release.

Các nhóm không được chọn (giọng báo timer đầy đủ, note/todo nhiều danh sách, lịch/thói quen, trang trí/cửa hàng/unlock đầy đủ) giữ phạm vi của bản port trước.
