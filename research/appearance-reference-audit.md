# Đối chiếu hình ảnh phòng với ảnh game gốc

Ngày 12/10/2026. Đối chiếu scene nguồn, code khởi động và ảnh dựng Editor. Chưa chạy gameplay trong trình duyệt.

## Phát hiện và sửa

- Scene export giữ mũ beret và đèn lồng đang bật. Code `HatChangeService.Setup` tắt mũ, trả blendshape tóc về 0; controller đèn lồng giới hạn sự kiện. Bản web nay bắt đầu với hai vật này tắt.
- `DecorationService.Setup` áp dụng decoration mặc định từ master/save. Các nhóm không có skin mặc định gồm sách sự kiện, huy hiệu và hộp nhạc đang được giữ từ scene export. Nay tắt ba nhóm này khi build phòng mới; lệnh truyện vẫn có thể bật lại.
- `ChangeHairService.ForceNormal` dùng kiểu tóc thường với người chơi mới. Nay kích hoạt Hair1, tắt Hair_type2 và ba kẹp tóc theo binding nguồn. Chưa port hệ thống xổ số/đổi tóc theo ngày.
- Ngày trên HUD thiếu năm. Nay có năm như ảnh tham chiếu.

Không thay các mesh/material phòng đã chuyển đổi bằng skin nguồn chưa kiểm chứng.

## Tỉ lệ và tư thế

`scene-parity-check.json` so sánh 6.890 transform chung. Transform ba root nhân vật và camera desktop không đổi; FOV 35°, actor scale (1,1,1).

`appearance-pose-check.json` lấy hai hoạt ảnh trên cùng rig/camera:

| Tư thế | Vị trí đầu theo chiều ngang viewport | Khoảng cách theo trục camera |
| --- | --- | --- |
| Đọc sách làm việc | 0,467 | 1,086 |
| Đọc sách lúc nghỉ | 0,736 | 1,332 |

Animation nghỉ tự đưa nhân vật lùi và sang phải, giảm kích thước biểu kiến. Ảnh game người dùng gửi có tư thế nghỉ; bản port khởi động ở vòng đọc sách làm việc. Đây là một nguyên nhân của khác biệt đóng khung, không cần sửa scale/camera để bù. Không khẳng định hai ảnh đã trùng tư thế, frame hoặc vị trí từng đạo cụ.

Ảnh dựng: `preview-appearance-work.png`, `preview-appearance-rest.png`. Các mẫu này chưa phải ảnh gameplay WebGL.

## Còn khác

- Màu/ánh sáng, bầu trời và bóng trên mặt/tóc chưa giống ảnh game gốc. Cần đối chiếu cùng thời điểm, frame và shader/lightmap/hậu kỳ; shader viết lại không bảo đảm pixel parity.
- Đạo cụ/tóc trong tư thế nghỉ chưa được xác nhận khớp ảnh tham chiếu. Cần kiểm tra toàn bộ chuyển tiếp vào hoạt động, thay vì chỉ sample một trạng thái animator.
- Cập nhật 13/10: Countup đã port, gồm trạng thái 00:00:00, start/pause/resume/reset. Pomodoro có số vòng tùy chỉnh và preset. Đèn bàn đã bật lại ánh sáng phụ theo thông số nguồn; xem `desk-lamp-lighting.md`. Những sửa này chưa xác nhận màu/ánh sáng browser giống hoàn toàn bản Windows.
- AI tự chọn/đổi hoạt động nghỉ và các hệ thống còn thiếu được ghi riêng trong `source-parity-audit.md`.

Các sửa trạng thái mặc định không phải triển khai đầy đủ sự kiện hoặc hệ thống tùy biến phòng.
