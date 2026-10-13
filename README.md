# Chill with You — bản thử WebGL độc lập

Workspace: `E:\02_Projects\chill-with-you-web`. Chạy riêng trên máy, không dùng route, Worker hay deployment của hoilauchay. Game Windows gốc chỉ được đọc khi trích xuất.

## Mở trên trình duyệt

Chạy `start-preview.ps1`, rồi mở **http://127.0.0.1:8093/** bằng Chrome hoặc Edge. Giữ server đang chạy; không mở trực tiếp HTML bằng `file://`.

```powershell
cd E:\02_Projects\chill-with-you-web
powershell -ExecutionPolicy Bypass -File .\start-preview.ps1
```

Trong phòng, dùng thanh icon native: nút nhạc phía dưới để chọn bài/phát/tạm dừng, icon ghi chú/công việc bên phải để mở bảng, icon truyện phía dưới bên trái để chọn đoạn đọc. Pomodoro ở góc trên bên phải. Mở bánh răng → General để lưu ngay, xuất hoặc nhập save web. Bấm icon mắt để ẩn UI; bấm lại icon hiện ở góc dưới bên phải hoặc Esc để mở lại. Esc cũng đóng bảng đang mở.

## Giao diện native

Canvas, bố cục, sprite, font và prefab ô nhạc/truyện/công việc/lựa chọn lấy từ giao diện gốc trong RoomScene. Không dùng HUD HTML tự thiết kế trên bản native hiện tại. Lớp adapter `ChillNativeUI` thay các callback phụ thuộc Windows/VContainer/R3 bằng logic bản web đã port.

Canvas chính dùng **Screen Space Overlay**, ưu tiên hiển thị 100, nên thanh dưới và các nút nằm trên cảnh 3D: tay, áo và sách không che UI. Ảnh Editor dùng camera riêng cho UI và ghép render UI trên render cảnh để kiểm tra đúng thứ tự lớp.

Các sprite UI xuất từ atlas bị mất kích thước trước khi cắt phần trong suốt. `ChillNativeSpriteBuild` phục hồi rect/padding từ `atlasRectOffset` và `uvTransform`, giữ nguyên pixel/texture atlas nguồn và không sửa thư mục Recovered. Icon mắt/cài đặt/thoát giữ vùng gốc 80×80; play/stop/skip của Pomodoro giữ 52×52, tránh kéo cao icon điện thoại và chồng các nút.

Playlist dùng chiều cao prefab native và LayoutElement cho từng hàng, hiển thị 43 bài từ MusicMaster và cuộn được. Âm lượng chỉ hiện khi trỏ vào nút/đang kéo; icon đổi đúng theo mute. Pomodoro hiện `hh:mm:ss`, giữ trạng thái tạm dừng, ẩn skip lúc pause và cập nhật vòng tiến độ. Kiểm tra Editor: `research/native-hud-check.json`; không chạy gameplay trình duyệt hay ghi save người dùng trong kiểm tra này.

Đã nối bảng nhạc/phát/tạm dừng/đổi bài/âm lượng, Pomodoro, ghi chú, công việc, danh sách truyện/tiếp tục truyện/lựa chọn/tự đọc và công cụ save web. Các bảng dùng khung native; tay cầm kéo bảng được nối lại. Nhãn dùng bảng dịch ở bảy ngôn ngữ nguồn (English/Japanese/Chinese Simplified/Chinese Traditional/Portuguese/Korean/Russian). Font bổ sung hỗ trợ ký tự thiếu trong ghi chú.

General có ba chế độ đọc/viết/gõ và dropdown bảy ngôn ngữ nguồn. Bảng môi trường có 24 cảnh cửa sổ, bấm cảnh đang chọn để bỏ chọn, hoặc chọn chỉ bầu trời. Khi truyện đang chạy, nút đổi chế độ/cảnh/ngôn ngữ bị khóa. History mở từ General hoặc nút trong hội thoại; giữ lời thoại của phiên và tạm dừng tự chuyển câu khi đang mở. Thông báo lưu/import/lỗi dùng khung native phía trên.

`research/native-controls-check.json` ghi kiểm tra callback trong Editor: 3 chế độ, 24 cảnh, đổi ngôn ngữ, history, khóa điều khiển trong truyện và báo lỗi import. Kiểm tra không ghi save người dùng và không chạy gameplay trình duyệt.

Bản 1.19 hỗ trợ nhiều ghi chú, danh sách công việc, lịch, thói quen, trang trí và hồ sơ local. Pomodoro có số vòng tùy chỉnh và preset; Countup có start/pause/resume/reset. Cloud và dịch vụ Windows để sau. Kiểm tra gameplay Chrome được ghi riêng tại `research/browser-gameplay-check.json`.

## Cốt truyện và save

- 610 nhóm truyện/hội thoại, 2.289 lệnh và 2.111 bản ghi lời thoại được chuyển từ master gốc. Có đọc từng câu/tự đọc, lựa chọn nhánh, lồng tiếng, animation, đổi cảnh và skin trong truyện.
- Giao diện đọc hỗ trợ bảy ngôn ngữ nguồn; thiếu bản dịch của từng ID thì fallback English. Không tự tạo bản dịch tiếng Việt cho lời thoại.
- Bản thử mặc định mở các chương để kiểm tra ngay. Pomodoro tăng EXP theo bảng game gốc.
- Save gồm chương/vị trí đang đọc, các đoạn đã hoàn thành, lựa chọn, cấp độ, thiết lập phòng, Pomodoro, ghi chú và công việc. Đóng/mở lại trang rồi bấm **Tiếp tục truyện đang đọc**.
- Bánh răng → **General** có lưu ngay, xuất JSON và nhập JSON. File được kiểm tra trước khi thay tiến trình. Đây là định dạng save của bản web, không nhận trực tiếp file ES3 của bản Windows.
- Pomodoro dùng timestamp và bù thời gian khi quay lại tab; dừng sau số phiên đã chọn. Countup và preset cũng được lưu trong save web. Nhật ký thoại giữ tối đa 200 mục trong save web.
- Save nằm trong localStorage của origin `http://127.0.0.1:8093`. Đổi port/trình duyệt hoặc xóa dữ liệu site sẽ dùng kho lưu khác; xuất JSON để chuyển tiến trình.

Animation humanoid chạy trên Satone; event gốc đồng bộ sách, bút, cốc, đồ ăn, ghế, cửa sổ và hộp nhạc trong phòng. Animation đồ vật khác trong thư viện cần prefab tương ứng. Texture, sprite, mesh, material, prefab và YAML có chế độ xem. Các prefab ô nhạc, truyện, công việc và lựa chọn đã được nối với logic bản web.

Server chỉ nghe `127.0.0.1`. Ctrl+C dừng server chạy từ terminal. Nếu Codex đã mở server nền, có thể dùng ngay đường dẫn trên; `stop-preview.ps1` dừng server nền đã được ghi nhận.

## Các thư mục

| Thư mục | Vai trò |
| --- | --- |
| `UnityProject` | Project Unity 2022.3.62f2; thêm thư mục này vào Unity Hub |
| `WebPlayer` | Bộ chạy WebGL của phòng 3D |
| `AssetLibrary` | Catalog và 233 bundle WebGL, khoảng 474 MB |
| `Recovered` | Export gốc, đầu vào chuyển đổi |
| `src/graphics` | Mã chuyển đổi, shader, runtime và build Editor |
| `research`, `logs` | Metadata khảo sát và log Unity |

Thư viện gồm **4.517 mục**: 1.554 audio (152 nhạc, 1.381 thoại, 21 âm thanh), 495 animation, 544 texture, 932 sprite, 241 mesh, 235 material chính, 91 prefab chính, font/controller/tài nguyên phụ và 304 bản ghi dữ liệu. Bundle tải theo lựa chọn; chuyển mục giải phóng bundle không còn dùng. Không nạp mọi audio thành PCM vào RAM.

## Phạm vi bản thử

Đây là bản port độc lập có phòng 3D, trình chạy kịch bản và save local; **chưa phải bản sao hoàn chỉnh mọi chức năng của game Windows**. Steam/cloud và một số hệ thống sự kiện/minigame riêng chưa triển khai; mua trang trí bằng điểm local đã có. Giao diện gameplay dùng canvas native đã phục dựng và adapter cho bản web.

Các hiệu ứng nhiễu, chuyển động màn hình, đen màn hình và tách màu được tái hiện cho WebGL. Camera mở đầu dùng controller gốc; 15 góc nhìn trên điện thoại dùng vị trí/FOV phục hồi, chuyển góc với nội suy nhẹ. Desktop giữ cách đóng khung cố định của nguồn. Chưa tái tạo đầy đủ hệ thống Cinemachine hoặc hiệu ứng hậu kỳ của bản Windows. Dữ liệu nguồn thiếu sáu ID lời thoại; bản web hiển thị thông báo thay vì tự viết thêm nội dung. Báo cáo ở `research/story-data-report.json`.

Mesh, rig, animation, audio, UI texture, font SDF và bốn bộ lightmap lấy từ asset nguồn. Shader D3D11 thiếu mã Shader Graph được viết lại cho WebGL; chưa bảo đảm hình ảnh giống từng chi tiết bản Windows. Mô phỏng phụ tóc/áo/mũ đã bật: chỉ xoay các xương phụ trong giới hạn, không ghi vị trí xương hoặc xoay các khớp humanoid. Đây là mô phỏng CPU nhẹ dựa trên root/collider nguồn, không phải solver MagicaCloth gốc. Kính dùng shader alpha WebGL riêng; font Noto Sans hỗ trợ chữ tiếng Việt của giao diện. Compute shader/plugin native không đưa vào WebGL.

## Chuyển động và trình bày truyện

- Dùng controller nhân vật gốc cho truyện, giữ các lớp biểu cảm, hướng nhìn và cử động miệng trong ba chế độ làm việc. Ánh mắt chuyển theo thời gian/ease trong kịch bản; các hội thoại ngắn dùng IK và blendshape mắt.
- Tên/đường dẫn trạng thái lấy từ controller và điều kiện `MotionType`, bao gồm ba giá trị kịch bản không có tên trong enum phục hồi. Truyện chờ đúng hoạt ảnh hoàn thành hoặc thoát trạng thái trước khi chuyển tiếp.
- Event hoạt ảnh điều khiển trang sách, bút quay/rơi, cốc, từng giai đoạn ăn, ghế, cửa sổ và hộp nhạc. Controller ghế chỉ giữ đường cong chuyển động/blendshape để không ghi đè material của skin đang dùng.
- Khi dừng truyện hoặc đổi chế độ làm việc, sách cầm tay và đồ ăn được ẩn, các đạo cụ trở về pose bàn. Hiệu ứng mũ khôi phục cả trạng thái tóc; ánh sáng kính râm dùng emission của material, có khôi phục khi dừng truyện.
- Save ghi đường đi đã đọc để dựng lại đúng nhánh, pose và đạo cụ khi tiếp tục. Save web cũ vẫn đọc được; nếu chưa có lịch sử nhánh, chỉ dùng chuyển động gần nhất trước vị trí lưu.
- Âm thanh gõ phím/viết được mở và dừng theo event, âm thanh thao tác tải từ bundle khi cần. Âm thanh vẫn cần một thao tác người dùng trên trình duyệt. AI tự chọn work/break/wild/leave chair đã nối theo master/controller nguồn; trọng số mong muốn và một số chuyển tiếp/mini-game riêng chưa đầy đủ.

`research/motion-presentation-audit.json` ghi kiểm tra Editor cho 106 chuyển động kịch bản, 42 thao tác ghế/bút, 15 camera, 128 tên event và 94 xương phụ. Mô phỏng được chạy 1.080 frame ở 30/60 FPS, kiểm tra pose hữu hạn, giới hạn xoay, khớp humanoid, vị trí xương và việc khôi phục đạo cụ/material. Ảnh `preview-motion-300.png`, `preview-motion-50.png`, `preview-motion-1702.png` là các mẫu dựng Editor; chưa thay cho việc đối chiếu gameplay trên trình duyệt.

Sách cầm tay Bears Restaurant ẩn trong ba chế độ làm việc, chỉ bật khi animation truyện gọi `PlayBearsRestaurantBookAnim` với tên chuyển động gốc. Dừng truyện hoặc đổi chế độ làm việc sẽ ẩn lại đạo cụ này. Trạng thái phòng mới tắt mũ beret, đèn lồng và các nhóm trang trí không có skin mặc định (sách sự kiện, huy hiệu, hộp nhạc), theo code startup gốc. Không thay mesh/material đã chuyển đổi. `preview-typing.png` là ảnh pose gõ phím dựng trong Editor.

`research/scene-parity-check.json` đối chiếu 6.890 transform chung với scene nguồn: camera desktop giữ FOV 35°, vị trí/rotation và scale actor không đổi. Điều này không chứng minh tư thế/đóng khung khi chạy giống ảnh game: ảnh tham chiếu là hoạt động nghỉ, còn bản web khởi động ở vòng đọc sách làm việc. AI đổi hoạt động và bốn post profile đã nối, nhưng chưa đủ thuật toán AI/hiệu ứng shader của nguồn. Màu chưa được xác nhận giống bản Windows.

Pomodoro/Countup dùng binding và trạng thái giao diện gốc, gồm số vòng, thời lượng và năm preset lưu trong save web. Vòng Level giữ vị trí gốc nhưng đã khôi phục phần padding của từng frame sprite để không chồng lên timer. Cả nút gốc và nút con của mỗi dòng Talk Log đều mở hội thoại. Các trạng thái và đường nhận click được kiểm tra trong Editor (`research/native-hud-check.json`).

Đã bật lại ánh sáng phụ PerPixel cho đèn bàn có sẵn trong scene; giữ nguyên vị trí, màu, cường độ và phạm vi của đèn gốc. Ảnh bật/tắt kiểm tra phần mặt bàn nhận sáng; `research/desk-lamp-lighting.md` ghi đối chiếu nguồn. Cần build lại bundle cùng player sau khi thay chế độ ánh sáng để tránh shader đã bị loại biến thể trong bundle cũ.

Các mức quality cùng dùng `ChillURP`; chỉ số mặc định WebGL được đặt trong phạm vi các mức hiện có. Ánh sáng preset dùng ambient mode Custom để tránh Trilight của scene nguồn tạo lại probe sau khi khởi động. Ảnh Editor áp dụng cùng preset ngày/đêm như runtime; `preview-typing-night.png` dùng preset Đêm. Đây là kiểm tra ảnh Editor, chưa phải xác nhận hai backend GPU cho màu giống từng pixel trên trình duyệt.

`prepare_toon_web.py` giữ nhánh lilToon SM3.5 không dùng DOTS cho cả Editor/WebGL. `ChillToonWeb` sinh các shader riêng theo nhóm material, dùng nguyên mã tô màu/ánh sáng của lilToon và chỉ bật những nhánh đang dùng ở nhóm đó. Material giữ giá trị, texture và render queue gốc; pass outline dư của shader nền không được thêm vào các material vốn không có outline. Shader không chuyển âm thầm sang Unlit khi không được hỗ trợ.

Callback tối ưu shader của lilToon được bỏ qua riêng với target WebGL: callback này từng sinh lại wrapper và gây cảnh báo loại shader ngay trước/sau đóng gói. Bản web dùng các pass đã chuẩn bị, giống ảnh Editor. Script áp dụng bản sửa này lại khi build; nguồn lilToon phục hồi trong `Recovered` không bị thay đổi.

Build biên dịch các biến thể GLES3 của từng nhóm (không shadow, shadow và cascade với soft shadow), kiểm tra bảng texture binding thực tế và dừng nếu vượt ngân sách **16 texture**. Cấu hình hiện tại gồm **55 nhóm cho 103 material**, **165 biến thể**, tối đa **9 texture**; cấu hình shader chung trước đó dùng 50–54 texture và bị GPU của trình duyệt từ chối. Báo cáo ở `research/toon-web-profiles.json` và `research/toon-web-texture-budget.json`. Sau thay đổi shader phải build lại cả player lẫn bundle material.

Đã chạy import/compile/build Unity, kiểm tra rig/animation và dựng ảnh trong Editor. Kết quả kiểm tra chức năng/render Unity ở `research/completion-check.json` và `research/features-check.json`; kiểm tra trình duyệt ghi riêng trong `research/browser-gameplay-check.json`.

## Build lại

Cần Unity **2022.3.62f2**, license đã kích hoạt, WebGL Build Support và Python 3.12. Đóng Editor đang mở project này rồi chạy:

```powershell
powershell -ExecutionPolicy Bypass -File E:\02_Projects\chill-with-you-web\build.ps1
```

Script đồng bộ runtime/editor, sinh shader, lấy bốn bộ lightmap, lấy event/binding chuyển động, kiểm tra rig/animation và build thư viện/player. Log: `logs/unity-standalone-build.log`.

Không chạy `prepare_project.py` cho build thường: đó là bước tái tạo project, cần chạy tiếp `asset_pipeline.py` và `generate_shaders.py` nếu dùng.

Server ánh xạ `/assetpacks/` tới `AssetLibrary/assetpacks` để không chép trùng thư viện. Nếu host nơi khác, giữ ánh xạ này hoặc đặt `assetpacks` cạnh `Build`/`StreamingAssets`; MIME `.wasm` phải là `application/wasm`.

## Các nhóm 1, 3, 6, 7, 8

Đã thêm sinh hoạt tự động của Satone và phản ứng click; playlist/favorites/import nhạc riêng; Skip/tutorial/credits/chào/tạm biệt; bộ trộn 30 ambient, audio channels và giờ tự đổi cảnh; cài đặt đồ họa/FPS/render scale/fit UI và lưu thiết lập. Mở General để điều chỉnh AI/thời gian/ambient, Audio để chỉnh kênh âm, Graphics để đổi chất lượng. Import nhạc cá nhân bằng nút file/folder trong Playlist.

File nhạc cá nhân lưu IndexedDB của origin, không đi kèm file save JSON. Bài/vị trí khôi phục ở trạng thái tạm dừng. Xem phạm vi, giới hạn và bằng chứng kiểm tra trong `research/features-1-3-6-7-8.md`. Beautify đang được thích nghi sang URP; chưa pixel parity hoặc xác nhận mobile/Safari thực tế.

## Bản hoàn thiện chức năng 1.19

Ghi chú nhiều trang và danh sách công việc riêng; lịch, nhật ký, thói quen và thống kê tuần/tháng/năm đã nối vào cửa sổ native. Save web cũ được migrate; thời gian phiên được chia theo ngày khi qua nửa đêm.

Trang trí/skin, mua bằng điểm, preset phòng, tóc/trang phục và thay đổi theo ngày đã có lưu cục bộ. Special có tiến trình độc lập. Hồ sơ có tên, avatar preset hoặc ảnh riêng, trạng thái, sinh nhật, achievement và nhắc việc. Pomodoro/Countup có chọn âm bắt đầu/kết thúc, volume/mute và giọng Satone riêng.

Nhạc cá nhân được kiểm tra bằng IndexedDB và giải mã WAV thực trong phiên Chrome riêng, gồm file chooser/import, giới hạn 200 bài, cập nhật file trùng và xóa; không ghi vào profile trình duyệt của người dùng. File nhạc không đi cùng save JSON.

Cloud để sau theo yêu cầu. Khác biệt còn lại gồm API Steam/OS native, solver MagicaCloth, toàn bộ nhánh AI/sự kiện và độ chính xác đồ họa Beautify. Xem đối chiếu cập nhật `research/source-parity-audit.md`; không khẳng định parity 100% với bản Windows.

## Source trên GitHub

Repo lưu mã port, công cụ chuyển đổi/build và báo cáo kiểm tra. `Recovered`, `UnityProject`, `AssetLibrary`, `WebPlayer`, cache, log và ảnh thử không đưa vào Git. Clone repo chưa có sẵn bản chạy hoặc dữ liệu game.

Để dựng trên máy khác, cần dữ liệu game Windows và Unity/license của riêng bạn. Cài Python dependencies bằng `python -m pip install -r requirements.txt`; trích xuất bằng `node src/graphics/recover-project.mjs "<thư mục game Windows>"`, rồi chạy `prepare_project.py`, `asset_pipeline.py` và `build.ps1` theo thứ tự trong `src/graphics/README.md`. Không chạy bước tái tạo project trên project đang mở hoặc đang build.

Lịch sử commit được chia theo giai đoạn chức năng từ các thay đổi ngày 12–13/10/2026. Thời điểm commit là thời điểm ghi nhận thực tế, không được backdate.
