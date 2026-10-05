# Camera nhiều chế độ & Cây hội thoại NPC

## Cài đặt (làm 1 lần)
1. Mở Unity, đợi script compile xong.
2. Menu **Tools ▸ Stealth Demo ▸ Setup Camera + Dialogue (DemoScene)**.
   Tool sẽ tự mở `DemoScene`, tạo camera, NPC, UI, cây hội thoại mẫu rồi lưu scene.
   Chạy lại bao nhiêu lần cũng được (đối tượng cũ sẽ bị thay thế).
3. Bấm **Play**.

> Vị trí NPC được tìm tự động gần điểm xuất phát (in ra Console). Có thể kéo `NPCs/NPC_Ivan`,
> `NPCs/NPC_Whisper` sang chỗ khác trong Scene view.

## Điều khiển
| Phím | Chức năng |
|---|---|
| WASD | Di chuyển (theo hướng camera) |
| C / Tab, 1–4, nút CAMERA | Đổi chế độ camera |
| Giữ chuột phải + kéo, Q / E | Xoay camera (Orbit / FPS) |
| Cuộn chuột | Zoom (Orbit) |
| E, nút NÓI CHUYỆN | Nói chuyện với NPC ở gần |
| Space / Enter / click | Hiện hết chữ / câu tiếp |
| 1–9 hoặc click | Chọn câu trả lời |
| Esc | Thoát hội thoại / nhả chuột ở FPS |

## Phần 4 – Điều khiển nhân vật & hệ thống camera

| # | Chế độ | Thành phần Cinemachine | Hệ quy chiếu di chuyển |
|---|---|---|---|
| 1 | Top-Down (Follow) | `CinemachineFollow` + `RotationComposer` | World (W = +Z) |
| 2 | Góc nhìn thứ ba (Orbit) | `OrbitalFollow` + `RotationComposer` + `Deoccluder` (né tường) | Hướng camera |
| 3 | Góc nhìn thứ nhất (FPS) | `HardLockToTarget` + `PanTilt` | Hướng camera, thân quay theo camera, ẩn mô hình |
| 4 | Camera an ninh (CCTV) | `ClearShot` + nhiều camera cố định + `ShotQualityEvaluator` | Hướng camera "chốt" khi bắt đầu bấm |
| – | Hội thoại (tự động) | Camera qua vai + `TargetGroup` (người chơi + NPC) | Khóa di chuyển |

- **Chuyển camera** = đổi `Priority`; `CinemachineBrain` tự blend. Có bảng blend riêng
  (`Generated/CameraBlends.asset`): cắt thẳng (Cut) vào CCTV, blend nhanh vào FPS, blend mượt vào hội thoại.
- **Di chuyển theo camera**: `CameraModeManager.GetMoveDirection()` chiếu hướng nhìn của camera
  xuống mặt phẳng XZ rồi ghép với input WASD. Chế độ CCTV "chốt" hướng để camera cắt cảnh không làm đổi hướng đi.
- **CCTV**: camera đặt trên trần dọc lộ trình tuần tra của ma. `ClearShot` chấm điểm từng camera
  (bị che hay không, khoảng cách) và tự cắt sang camera nhìn rõ người chơi nhất. HUD hiển thị "REC CAM-xx".
- **Camera hội thoại**: thử lần lượt vị trí qua vai phải / trái / góc ngang, chọn vị trí đầu tiên không bị tường che.

Script: `CameraSystem/CameraModeManager.cs`, `CameraSystem/CameraLookInput.cs`,
`Tutorial_Demo/Demo_Scripts/PlayerMovement.cs` (đã sửa).

## Phần 8 – Cây hội thoại (Dialogue Tree)

**Dữ liệu** (`DialogueTree` – ScriptableObject):
- `DialogueNode`: id, người nói, nội dung, hành động khi vào node, danh sách lựa chọn, node kế tiếp.
- `DialogueChoice`: nội dung, node đích, **điều kiện**, **hành động**, ẩn hoặc hiện dạng khóa kèm gợi ý.
- `DialogueEntry`: điểm vào có điều kiện → NPC chào khác nhau khi gặp lại.
- Điều kiện: `FlagSet/NotSet`, `HasKey/NotHasKey`, `NodeVisited/NotVisited`.
- Hành động: `SetFlag`, `ClearFlag`, `GiveKey`, `SlowEnemies`, `OpenDoor`, `AlertEnemies`, `ShowMessage`.

**Luồng chạy** (`DialogueManager`):
`Idle → [E] → StartDialogue (dừng thời gian, camera hội thoại) → EnterNode → chọn / tiếp tục → … → EndDialogue`.
Cờ (flag) dùng chung giữa mọi NPC.

**Kịch bản mẫu** (`Assets/_3DStealthGame/Dialogue/`):
- **Ivan** – người làm vườn bị kẹt: menu câu hỏi lặp lại, câu đố (trả lời đúng → nhận *chìa khóa xanh*;
  sai → thử lại), lựa chọn chỉ hiện khi đã có chìa khóa, câu thoại của chính người chơi.
- **Whisper** – hồn ma thân thiện: lựa chọn "Ivan gửi lời hỏi thăm" **bị khóa** cho tới khi hỏi Ivan về Whisper
  (trạng thái chia sẻ giữa 2 NPC). Sau đó chọn: *làm chậm ma 25 giây* hoặc *mở cửa đỏ*.
  Chọn câu khiêu khích → ma báo động → **bị bắt (thua)**.

**Công cụ Editor**: chọn asset hội thoại → nút **Mở đồ thị** (hoặc *Tools ▸ Stealth Demo ▸ Dialogue Tree Viewer*)
để xem cây dạng sơ đồ; nút **Kiểm tra lỗi** báo id trùng, liên kết hỏng, node không tới được.

## Gợi ý kịch bản demo (khoảng 3 phút)
1. Play ở Top-Down → bấm **2** (Orbit, xoay bằng chuột phải, áp sát tường để thấy camera né tường)
   → **3** (FPS) → **4** (CCTV, đi qua các phòng để thấy camera tự cắt).
2. Nói chuyện với **Whisper** trước → thấy lựa chọn bị khóa.
3. Nói chuyện với **Ivan**: hỏi về Whisper, giải câu đố → nhận chìa khóa xanh. Nói lại với Ivan → lời chào thay đổi.
4. Quay lại **Whisper** → lựa chọn đã mở → làm chậm ma / mở cửa đỏ.
5. (Tùy chọn) Chọn câu khiêu khích Whisper để minh họa hành động làm thua game.
