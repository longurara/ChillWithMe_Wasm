# Khả năng port Chill with You: Lo-Fi Story lên web

Ngày kiểm tra: 12/10/2026. Thư mục đầu vào:
`E:\02_Projects\decr\Chill with You Lo-Fi Story`.

## Kết luận

Có hướng port bằng cách phục dựng project Unity và build lại cho WebGL.
Chưa có bằng chứng rằng một bản port đầy đủ sẽ chạy được: chỉ mới phân tích
tĩnh, chưa phục dựng project, chưa build WebGL và chưa chạy game.
Đây là công việc port thực sự, không phải đóng ZIP bản Windows rồi thêm loader.

Điểm thuận lợi là bản Windows dùng Mono, còn tên lớp và logic C# đọc được,
và các file scene/asset có thể phân tích bằng UnityPy. Điểm khó nhất là phục
dựng shader, xử lý MagicaCloth 2, sửa phụ thuộc Steam và thay luồng tải âm thanh.

## Bằng chứng từ bản game trên máy

| Mục | Kết quả |
|---|---|
| Unity | `2022.3.62f2`, đọc từ serialized-file header |
| Phiên bản game | `1.17.3`, từ `PlayerSettings.bundleVersion` |
| Scripting backend | Mono; có `Managed/Assembly-CSharp.dll` và `MonoBleedingEdge` |
| Dung lượng thư mục | 315 file, 1.347.490.820 byte, khoảng 1,35 GB / 1,25 GiB |
| Assembly gameplay | 2.558.464 byte, 2.587 TypeDef và 14.534 MethodDef |
| C# đọc lại | ILSpy tạo 1.430 file `.cs`; đây là mã decompile, chưa phải project Unity build được |
| Pipeline | Có Universal Render Pipeline, Cinemachine, Shader Graph và baked lightmaps |
| Addressables | `settings.json` ghi `StandaloneWindows64`; catalog và bundle còn nguyên |
| Nhạc/ambient/SE trong audio bundle | 152 AudioClip, tổng khoảng 9.464 giây; bundle 458.553.723 byte |
| Lồng tiếng | 1.381 AudioClip, tổng khoảng 4.034 giây; bundle 44.871.438 byte |

Đã đọc metadata của `globalgamemanagers`, `globalgamemanagers.assets`,
`resources.assets`, `sharedassets1.assets`, `sharedassets3.assets` và cả năm
scene `level0`–`level4`. Đã đọc thêm ba bundle audio, voice và scriptableobjects.
Không chạy EXE hoặc DLL gameplay; ILSpy chỉ phân tích IL và UnityPy chỉ đọc dữ liệu.

## Những phần phải sửa

### Shader và hình ảnh

112 Shader đã kiểm tra trong bốn asset file chỉ có `platforms: [4]`
(Direct3D 11), không có bản GLES/WebGL, và không giữ `m_Script` source shader.
Ví dụ: `Shader Graphs/ScreenColorGlitch`, `ScreenBlackout`, `NoiseShader`,
các shader URP, vật liệu nhân vật và môi trường.

Các shader chuẩn có thể thay bằng shader từ đúng package Unity. Shader tùy
biến cần phục dựng hoặc viết lại. Không có bằng chứng rằng toàn bộ Shader Graph
gốc còn khôi phục nguyên vẹn được. Cần kiểm tra mắt, tóc, da, kính, nước, mưa,
ánh sáng và các hiệu ứng câu chuyện sau khi dựng lại scene.

Có tám ComputeShader trong hai asset file đã kiểm tra. Một số thuộc VFX/
light-probe tooling; sự hiện diện của chúng chưa chứng minh game bắt buộc dùng
compute trong lúc chơi. Cần loại phần tooling hoặc thay hiệu ứng nếu có phụ thuộc.

### MagicaCloth 2

Scene chính `level3` thực sự tham chiếu bốn `MagicaCloth`, bảy
`MagicaCapsuleCollider` và một `MagicaSphereCollider`. Vì vậy đây là phụ thuộc
đang có trong scene, không chỉ là DLL được đóng kèm.

Tài liệu của nhà cung cấp ghi MagicaCloth 2 không hỗ trợ WebGL. Bản prototype
có thể dùng animation/bone pose có sẵn với mô phỏng cloth tắt; muốn giữ chuyển
động tương đương bản gốc phải thay hệ mô phỏng. Chưa kiểm chứng ảnh hưởng tới
ngoại hình của Satone.
[Nguồn Magica Soft](https://magicasoft.jp/en/mc2_about/).

### Steam, Windows và dịch vụ tài khoản

`Bulbul.EntryBehavior.StartAsync` gọi `steamManager.Initialize()` và đợi
Steam sẵn sàng trước khi vào phòng. `ProjectRegister` đăng ký SteamManager,
SteamAchievements và SteamDefaultLanguageSupplier. `BulbulConstant` còn lấy
Steam user ID để tạo đường dẫn save. Các dịch vụ này phải có implementation
cho nền tảng web; không thể đưa nguyên `steam_api64.dll` vào trình duyệt.

`NestopiSystem.ScreenSystem` có 10 P/Invoke vào `user32.dll` để giữ cửa sổ
always-on-top và sửa menu Windows. Tính năng cửa sổ desktop cần bỏ khỏi UI web
hoặc thay bằng chức năng trình duyệt phù hợp.

Có Firebase native, Google/Apple sign-in, purchasing và cloud-save code.
Sự hiện diện của DLL không chứng minh mọi dịch vụ đều cần để chơi. `LoginFlow`
có nhánh không liên kết tài khoản (`AccountType.None`), vì vậy hướng prototype
ưu tiên chơi và lưu tại máy, rồi mới xem xét dịch vụ online. Không gọi các
backend của game trong đợt nghiên cứu này.

### Âm thanh

Nhạc có sẵn là AudioClip Unity; `EntryBehavior.LoadAsync` dựng playlist từ
`MasterDataLoader.MusicDataList`. Không cần bộ giải mã Windows để phát playlist
có sẵn.

Bộ `BulbulLocalAudioDecoder.dll` chỉ được chọn khi nhập nhạc đuôi `.m4a`, qua
`LocalAudioDecoderFactory`. `StreamingLocalAudioClip` dùng C# Thread và
`AudioClip.Create(..., stream: true, PCM callbacks)`. Luồng này không tương
thích WebGL 2022.3; phải thay bằng file picker và bộ giải mã/playback của browser,
hoặc tạm bỏ tính năng nhập nhạc ở prototype.

Trong audio bundle có 75 clip với LoadType Streaming, 59 DecompressOnLoad,
18 CompressedInMemory; voice bundle có 1.381 CompressedInMemory. Cần build lại
audio cho web và quản lý bộ nhớ theo clip/bài nhạc. Ước tính nếu giải mã mọi clip
ở hai bundle đồng thời thành float32 PCM, riêng âm thanh đã khoảng 4,39 GB
(chưa tính texture, scene và runtime). Đây là phép tính từ duration × channels
× sample rate × 4, không phải RAM đo được của game.

Âm lượng theo nhóm trong `AudioMixerGroupContainer` dùng `SetFloat`, phần này
có cơ sở giữ được. Hiệu ứng mixer khác cần kiểm tra riêng; Unity 2022.3 WebGL
chỉ hỗ trợ thay volume trong mixer, không hỗ trợ mọi hiệu ứng.
[Unity audio](https://docs.unity3d.com/2022.3/Documentation/Manual/webgl-audio.html),
[Unity threading](https://docs.unity3d.com/2022.3/Documentation/Manual/webgl-technical-overview.html).

### Save và đồng hồ

`SaveDataIO` dùng ES3.Location.File và đường dẫn tương đối chứa Steam ID.
Cần chuyển sang đường dẫn lưu của web, đồng bộ xuống IndexedDB, và hỗ trợ
export/import save. Không chỉ giữ nguyên đường dẫn Windows.

`PomodoroTimerService.Update` và `CountupTimerService` cộng `Time.deltaTime`.
Browser có thể giảm hoặc dừng cập nhật tab nền, đặc biệt khi khóa màn hình
trên điện thoại. Cần tính thời gian theo timestamp và bù thời gian khi quay
lại tab; không thể kết luận timer sẽ hoạt động đúng chỉ từ `runInBackground=true`.

## Đường triển khai phù hợp

1. Phục dựng scene, prefab, mesh, animation, texture và ScriptableObject bằng
   công cụ như AssetRipper; ghép C# đọc lại với đúng phiên bản package Unity.
   AssetRipper hỗ trợ dải Unity bao gồm 2022.3 nhưng không bảo đảm export thành
   một project hoàn chỉnh chạy ngay.
2. Dựng prototype desktop: phòng, Satone, animation cơ bản, một bài nhạc,
   một đoạn thoại, Pomodoro và save local. Tạm tắt cloth và các dịch vụ desktop.
3. Thay shader và build WebGL thực sự. Đánh giá việc phục dựng ở bước này trước
   khi cam kết port toàn bộ câu chuyện và tính năng.
4. Build lại Addressables/bundle cho WebGL. Unity xác định AssetBundle theo
   build target, nên không dùng nguyên bundle `StandaloneWindows64` làm dữ
   liệu chạy của bản WebGL.
5. Sau khi prototype chạy ổn mới tích hợp website: launcher nhỏ trên Vercel,
   data WebGL đã build trên Drive, tải qua Worker, lưu dữ liệu vào OPFS/cache.
   Có thể tải đầy đủ xuống ổ lưu trữ trước khi cho chơi theo yêu cầu hiện tại;
   chỉ đưa asset đang dùng vào RAM, không giải mã mọi bài nhạc cùng lúc.
6. Tối ưu điện thoại sau desktop: giảm texture/hiệu ứng, chia audio và voice,
   xử lý audio sau thao tác người dùng, kiểm tra suspend/resume và giới hạn RAM.

Lưu data trên Drive giúp giảm tải deployment, nhưng không chuyển Windows
runtime/shader/native plugin thành runtime web. Cần có `.wasm`, loader và data
được Unity build cho WebGL trước khi áp dụng hệ thống host hiện có.

[AssetRipper](https://github.com/AssetRipper/AssetRipper),
[Unity build WebGL](https://docs.unity3d.com/2022.3/Documentation/Manual/webgl-building.html),
[AssetBundle build target](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/BuildAssetBundlesParameters-targetPlatform.html).

## Giới hạn của kết quả

### Bổ sung: chiến lược đồ họa

Kiểm tra quan hệ Material → Shader cho thấy 105 material trong bốn asset file
dùng họ lilToon: 89 opaque, 4 cutout và 12 transparent. Có cả `mt_Face`,
`mt_Hair`, `mt_Body`, `mt_Outfit`, cùng nhiều đồ nội thất. Material giữ
`_lilToonVersion = 43`. Shader lilToon có source công khai; ưu tiên phục hồi
source tương thích với bộ property này, rồi biên dịch cho WebGL và giới hạn
feature theo nền tảng. Chưa xác minh version release tương ứng với số 43 hoặc
mọi feature trong material có thể hoạt động trên WebGL.
[Source lilToon](https://github.com/lilxyzw/lilToon).

Vì vậy 112 shader đã biên dịch D3D11 không đồng nghĩa phải viết tay 112 shader.
Nhiều shader thuộc URP, TMP, UI và các package sẵn có. Chỉ những shader tùy biến
không có source phù hợp mới cần phục dựng riêng.

Chiến lược đề xuất là giữ Unity/URP, mesh, rig, animation và texture gốc;
khôi phục shader theo nhóm, ưu tiên diện mạo Satone. Restore lightmap, reflection
và các preset ánh sáng gốc nếu liên kết/UV còn đúng; bake lại phần không phục hồi
được. Dùng Unlit/Simple Lit cho vật thể phù hợp sau khi so hình ảnh. Bầu trời,
mưa và post-processing nên có bản đơn giản hơn cho mobile; các hiệu ứng chuyển
cảnh/glitch ảnh hưởng câu chuyện cần được giữ chức năng và phục dựng riêng.
[Simple Lit](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/simple-lit-shader.html).

Prototype giữ animation nhân vật, tắt MagicaCloth; sau đó thay phần chuyển động
phụ cần thiết bằng mô phỏng bone nhẹ hoặc animation bake. Preset mobile ban đầu
có thể nhắm 30 FPS và render scale 0,75–0,85, nhưng đây là mục tiêu đề xuất,
chưa có benchmark. Desktop có thể giữ chất lượng cao hơn trong cùng project.

Chưa export bằng AssetRipper, chưa cài Unity Editor, chưa build, chưa chạy
prototype và chưa benchmark RAM/FPS. Tìm kiếm web chưa tìm được bản WebGL hoàn
chỉnh đã xác minh cho game này. Các repo mod tìm thấy là BepInEx desktop.
Package npm `chill-with-you-core-v2@1.0.1` chỉ có tiện ích Pomodoro đơn giản
(`startPomodoro`, `breakTime`), không phải engine hay bản port game.

Các dữ liệu phân tích tạm và C# đọc lại nằm trong `scratch/chill-web-research`.
Không thay đổi thư mục game gốc, website hoặc deployment trong đợt nghiên cứu này.
