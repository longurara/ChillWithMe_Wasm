# Đối chiếu chức năng với code nguồn

Đối chiếu masters/controllers/UI nguồn, kiểm tra state/callback/raycast trong Unity; thao tác UI và save/reload, IndexedDB/audio/import trong Chrome riêng. Xem browser-gameplay-check.json và browser-storage-check.json; không xác nhận mobile/Safari hay toàn bộ engine Windows.

Bản port riêng. Cloud hoãn theo yêu cầu. Không xác nhận toàn bộ hành vi/đồ họa giống bản Windows.

| ID | Nhóm | Trạng thái | Phạm vi/còn khác |
| --- | --- | --- | --- |
| 1 | Ghi chú nhiều trang | Đã triển khai | Tạo/chọn/xóa/đổi tên nhiều trang, kéo thứ tự, nội dung và vị trí cuộn riêng. |
| 2 | Danh sách công việc | Đã triển khai | Nhiều danh sách, đổi tên, sửa việc, thứ tự, hạn và ngày hoàn thành; liên kết lịch. |
| 3 | Lịch và nhật ký theo ngày | Đã triển khai | Duyệt tháng/ngày, nhật ký riêng từng ngày, việc đến hạn/hoàn thành, đánh dấu thói quen và chỉnh thời gian. |
| 4 | Theo dõi thói quen | Đã triển khai | Tạo/sửa/xóa, lịch ngày trong tuần, lịch sử đánh dấu, tạm dừng và thứ tự; thuật toán lịch sử khoảng tạm dừng chưa giống toàn bộ nguồn. |
| 5 | Thống kê làm việc | Đã triển khai | Ghi thời gian theo ngày, chia phiên qua nửa đêm, biểu đồ tuần/tháng/năm và dữ liệu tổng từ save cũ. |
| 6 | Pomodoro đầy đủ | Đã triển khai | Start/pause/reset/skip, vòng/phút và năm preset; thống kê, EXP và âm thanh liên kết. |
| 7 | Đồng hồ đếm lên Countup | Đã triển khai | Countup start/pause/resume/reset, thời gian làm việc và lời thoại theo chế độ. |
| 8 | Timer liên kết Satone/âm báo | Đã triển khai | Chọn âm bắt đầu/kết thúc, volume/mute riêng từng chế độ, bật/tắt giọng Satone và phản ứng phiên ngắn/dài/liên tiếp. |
| 9 | Thông báo nhắc việc/timer | Đã triển khai cho web | Nhắc việc đến hạn và kết thúc timer; chỉ xin quyền khi bật. Thông báo hệ điều hành phụ thuộc quyền/trình duyệt, không thay dịch vụ chạy nền khi đóng trang. |
| 10 | Quản lý playlist | Đã port quản lý playlist web | Tạo/xóa/thêm/xóa/đổi thứ tự, favorites và lưu danh sách. Đổi thứ tự bằng nút; chưa có kéo thả theo UI gốc. |
| 11 | Nhập nhạc cá nhân | Đã port import cho trình duyệt | File/folder lưu IndexedDB, giải mã WAV khi phát và báo lỗi. Metadata file cá nhân dùng tên file; chưa đọc toàn bộ tag ID3. Chưa kiểm tra storage end-to-end trên trình duyệt. |
| 12 | Phát nhạc đúng hành vi gốc | Đã port các điều khiển chính | 43 bài MusicMaster, seek/auto-next/shuffle/repeat/metadata/khôi phục paused. Chưa tái hiện đầy đủ mọi fade/transition audio nguồn. |
| 13 | AI sinh hoạt tự nhiên của Satone | Đã mở rộng, còn khác nguồn | Trọng số/growth/cooldown theo nhóm gốc, work/break/wild/rời ghế, trà/ăn/sleep và mesh đồ ăn hiếm. Các nhánh chuyển tiếp nội bộ của AI chưa khớp hoàn toàn engine nguồn. |
| 14 | Bấm nhân vật để phản ứng | Đã nối phản ứng nhân vật | Bấm vùng renderer nhân vật, cooldown và các nhóm phản ứng/đánh thức. Hit test là bounds, chưa có mesh/collider picking chính xác hoàn toàn. |
| 15 | Chế độ làm việc/vị trí phòng | Một phần | Đã nối chọn đọc/viết/gõ trong General và lưu lựa chọn. Chuyển bàn/sofa/giường và controller di chuyển chưa port. |
| 16 | Trang trí và preset phòng | Đã triển khai | 29 nhóm lựa chọn, skin/màu nguồn, điều kiện mở khóa/điểm, reset, lưu/nạp/xóa preset phòng. Popup mua đồ dùng adapter web. |
| 17 | Cảnh cửa sổ và ambient | Một phần mở rộng | Giữ 24 cảnh cửa sổ và thêm 30 ambient độc lập, lưu mức, tự đổi ngày/chiều/đêm theo giờ cấu hình. Chưa port mọi phối hợp ambient/environment riêng của nguồn. |
| 18 | Trang phục/kiểu tóc theo ngày | Đã triển khai | Bốn kiểu tóc, năm trang phục, material/xương theo nguồn và chọn theo trọng số tháng mỗi ngày; các quy tắc sự kiện giới hạn thời gian chưa đầy đủ. |
| 19 | Mở khóa/điểm mua đồ | Đã triển khai | Master điều kiện story/level/points, mua một lần và kiểm tra số dư; mở khóa môi trường và nhạc. |
| 20 | Achievement | Đã triển khai cục bộ | Lưu achievement và thông báo, xem trong hồ sơ; không nối achievement/stats Steam. |
| 21 | UI truyện/nhật ký thoại | Một phần mở rộng | Talk Log/row con, Skip, lịch sử thoại tối đa 200 mục và bảo toàn resume khi hội thoại tự động. Chưa đồng bộ toàn bộ bố cục/stats log nguồn. |
| 22 | Ngữ nghĩa lệnh kịch bản | Một phần | Có case cho mọi giá trị CommandType nhưng hành vi chưa đều tương đương: command 25 vốn chỉ phát ở talk log được gộp BGM thường; command 50 vốn chờ tín hiệu controller được đổi thành chờ Next; PomodoroVoiceText chưa đồng bộ AI/timer. |
| 23 | Tutorial/trợ giúp | Đã nối tutorial native | Click Satone, focus timer/note/todo và bốn trang sprite nguồn ở bảy ngôn ngữ. Chưa đối chiếu toàn bộ lượt tutorial trong gameplay trình duyệt. |
| 24 | Credits/kết thúc | Đã nối credits native | 15 nhóm Canvas, fade/hold/delay lấy từ nguồn; có nút Credits và lệnh truyện. Chưa port service Windows/Steam liên quan ending. |
| 25 | Special/collaboration/sự kiện | Đã mở rộng, còn khác nguồn | Bảy nhánh special có EXP/level độc lập, các story unlock nối dữ liệu gốc; sinh nhật/lịch/chào/tạm biệt đã nối. Không khẳng định đủ mọi sự kiện/collaboration của bản Windows. |
| 26 | Hồ sơ người chơi/Satone | Đã triển khai | Tên, avatar preset/custom, trạng thái, sinh nhật; profile Satone volume/mute/self-talk; avatar custom lưu vào save web. |
| 27 | Cài đặt/ngôn ngữ | Một phần mở rộng | Bảy ngôn ngữ, AI, audio channels, auto-time, 12/24h, quality/FPS/render scale/post/fullscreen và fit UI. Chưa đủ mọi tùy chọn nguồn/plugin OS. |
| 28 | Save đầy đủ/tương thích nguồn | Đã mở rộng cho web | Save cục bộ, import/export/backup, migrate dữ liệu web cũ và kiểm tra dữ liệu nhập; nhạc riêng lưu IndexedDB, không nằm trong JSON. Không tương thích save Windows. |
| 29 | Account/cloud/Steam/tính năng OS | Hoãn theo yêu cầu | Cloud/account để sau theo lựa chọn của người dùng. API Steam và plugin OS native không chạy trong WebGL. |
| 30 | Đồ họa/camera/mô phỏng | Một phần mở rộng | Bốn post profile chuyển thông số Beautify/LiftGammaGain sang URP, giữ đèn bàn gốc. Chưa pixel parity/Cinemachine/MagicaCloth gốc đầy đủ. |
| 31 | UI mobile/vòng đời ứng dụng | Một phần mở rộng | Fit khung native, touch/gesture, save khi pagehide/focus, FPS nền và fullscreen theo trình duyệt. Chưa kiểm chứng mobile/Safari trên thiết bị thật. |
| 32 | Animation UI/khóa chức năng | Đã mở rộng, còn khác nguồn | Fade mở cửa sổ, kéo vị trí và lưu, raycast/callback gốc được thay bằng adapter, loại callback prefab cũ. Tween đóng/mở chưa giống toàn bộ animation nguồn. |
| 33 | Khởi động/chào/tạm biệt | Đã nối các luồng web chính | Chào theo thời gian lần ghé trước, phản ứng focus, tạm biệt và lưu; không đóng tab. Chưa mọi biến thể nguồn/lifecycle Windows. |
