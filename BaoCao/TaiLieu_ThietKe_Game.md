# LEMON'S ESCAPE – Thoát khỏi dinh thự ma ám

Tài liệu thiết kế & kỹ thuật (Game Design Document) – đồ án môn học Lập trình Game 3D.
Công nghệ: **Unity 6 (6000.6) · URP · Cinemachine 3 · Input System · AI Navigation · UI Toolkit**.

---

## 1. Ý tưởng & gameplay

| Mục | Nội dung |
|---|---|
| Thể loại | Hành động lén lút (stealth-action) góc nhìn thứ ba, bối cảnh kinh dị nhẹ |
| Bối cảnh | Một dinh thự cổ bị nguyền rủa, chỉ tồn tại trong đêm – tan biến lúc bình minh |
| Cốt truyện | John Lemon tỉnh dậy trong dinh thự. Ivan (người làm vườn bị kẹt) cho biết cửa chính bị phong ấn bằng **3 Mảnh hồn**. John phải lén lút qua lũ ma, thu thập đủ 3 mảnh và thoát ra trước 06:00 sáng |
| Mục tiêu | Thu thập 3 Mảnh hồn → phá phong ấn cửa chính (Cánh Đông) → bước ra ngoài |
| Thắng | Bước vào vùng thoát sau khi phá phong ấn |
| Thua | Máu về 0 (bị ma đánh) → hồi sinh tại checkpoint; hoặc **hết giờ (bình minh)** → thua cả lượt |

### Gameplay loop
```
Khám phá phòng ─► Quan sát tầm nhìn ma (nón sáng) ─► Lẻn qua / né / đánh choáng
      ▲                                                        │
      │                                                        ▼
Checkpoint (lưu) ◄── Nhặt vật phẩm, mảnh vụn, chìa khóa ◄── Nhận gợi ý từ NPC
      │
      └─► Mở phong ấn bằng chìa khóa ─► Lấy Mảnh hồn ─► (đủ 3) ─► Phá cổng ─► Thoát
```

### Tính chơi lại
- 3 độ khó (Dễ / Thường / Khó): tốc độ, tầm nhìn, sát thương của ma, thời gian tới bình minh.
- **Xếp hạng S/A/B/C/D** dựa trên điểm: mảnh vụn, ma bị trục xuất, thời gian còn lại, thưởng không chết / không bị phát hiện.
- Kỷ lục (điểm cao nhất, thời gian nhanh nhất, số lần thắng) lưu lại và hiển thị ở menu.
- Nhiều cách giải: lén lút hoàn toàn, đánh choáng ma, nhờ Whisper làm chậm ma / mở cửa, đi đường tắt qua cửa màu.

### Nhiệm vụ, thử thách, phần thưởng
| Nhiệm vụ | Loại | Phần thưởng |
|---|---|---|
| Nói chuyện với Ivan | Chính | Mở danh sách nhiệm vụ, chỉ đường |
| Thu thập Mảnh hồn (0/3) | Chính | +1000 điểm / mảnh |
| Tìm chìa khóa đỏ (Cánh Tây – có tượng Gargoyle canh) | Chính | Mở Phong ấn Đỏ |
| Tìm chìa khóa xanh (phòng ô cờ) | Chính | Mở Phong ấn Xanh |
| Phá phong ấn cửa chính | Chính | Mở lối thoát |
| Trục xuất 3 hồn ma | Phụ | +200 / ma (Oan hồn +600), rơi mảnh vụn |
| Thu thập 20 mảnh vụn linh hồn | Phụ | Điểm + tiền đổi đồ với Ivan |

---

## 2. Hệ thống game

| Hệ thống | Mô tả |
|---|---|
| Menu chính | Tiếp tục (hiển thị độ khó, số Mảnh hồn, thời điểm lưu) · Chơi mới (chọn độ khó) · Hướng dẫn · Cài đặt · Thành tích · Thoát |
| Tạm dừng (Esc) | Tiếp tục · Nhật ký · Cài đặt · Hồi sinh tại checkpoint · Về menu · Thoát (có hộp xác nhận) |
| Cài đặt | Âm lượng tổng / nhạc / hiệu ứng, độ nhạy chuột, đảo trục Y, rung camera, chất lượng đồ họa, toàn màn hình, hiện FPS |
| Lưu / tải | Tự lưu khi thắp nến checkpoint (JSON, ghi file tạm rồi thay thế để chống hỏng). Lưu: vị trí checkpoint, máu, năng lượng, hành trang, vật phẩm đã nhặt, cửa/phong ấn đã mở, cờ hội thoại, thời gian, thống kê |
| HUD | Máu / thể lực / năng lượng đèn, đồng hồ (00:00 → 06:00), Mảnh hồn 3 ô, chìa khóa, mảnh vụn, bình máu, điểm, nhiệm vụ hiện tại, trạng thái (Bị phát hiện / Nghi ngờ / Ẩn nấp), dấu ?/! trên đầu ma, viền đỏ khi bị thương |
| Chỉ số | Máu 100, thể lực (chạy), năng lượng đèn (bật đèn & Linh Quang), mảnh vụn (tiền + điểm), bình máu |
| Màn hình kết thúc | Game Over (bị bắt / bình minh) · Chiến thắng (bảng điểm chi tiết, hạng, kỷ lục mới) |

---

## 3. Nhân vật (John Lemon)

| Hành động | Phím | Ghi chú |
|---|---|---|
| Di chuyển | WASD / cần trái | Theo hướng camera |
| Chạy | Shift | Tốn thể lực, gây tiếng động lớn; kiệt sức phải nghỉ |
| Ngồi | C / Ctrl | Chậm, gần như im lặng, ma khó thấy hơn (tầm nhìn x0.7, tốc độ phát hiện x0.45) |
| Nhảy | Space | Tiếp đất gây tiếng động |
| Linh Quang | Chuột trái | Nón sáng 6.5 m, sát thương 40 (x2 khi đánh lén), làm choáng 2.5 s, tốn 25 năng lượng |
| Đèn soi | F | Chiếu sáng, tốn năng lượng, nhưng ma thấy bạn xa hơn x1.4 |
| Tương tác | E | NPC, cửa, phong ấn, cổng |
| Bình máu | H | +45 máu |
| Nhật ký | Tab | Hành trang, nhiệm vụ + gợi ý, cốt truyện, phím |
| Camera | V / 1–4 | Góc nhìn thứ ba · Top-down · Thứ nhất · Camera an ninh |

- **Animation**: blend tree Idle → Walk → Run theo tốc độ + lớp procedural: co người khi ngồi, kéo giãn khi nhảy, nén khi tiếp đất, lao tới khi tấn công, giật lùi khi trúng đòn, ngã ngửa khi chết.
- **Máu / sát thương / chết**: bất tử 0.9 s sau mỗi đòn, bị đẩy lùi, rung camera, viền đỏ, nhịp tim khi máu thấp; chết → Game Over → hồi sinh tại checkpoint.

---

## 4. Môi trường & level design

Bản đồ: dinh thự nhiều phòng (Level A) – phòng ngủ, sảnh lớn, nhà bếp, phòng khách, phòng tắm, thư phòng, phòng ăn ô cờ, hành lang, phòng thoát.
Ánh sáng baked + đèn nến động, sương mù, post-processing (Scary Volume Profile: vignette, color grading, bloom).

| Khu vực | Độ khó | Nội dung |
|---|---|---|
| Phòng ngủ (xuất phát) | Hướng dẫn | Ivan, checkpoint đầu, mảnh vụn |
| Sảnh lớn + Cánh Tây | Dễ | Whisper, 1 tượng Gargoyle ở hành lang, **chìa khóa đỏ** |
| Bếp / phòng khách | Dễ | **Mảnh hồn Bình Minh** (không phong ấn), 1 hồn ma tuần tra |
| Hành lang giữa + thư phòng | Trung bình | 2 hồn ma, 2 Gargoyle, **chìa khóa xanh**, **Mảnh hồn Hoàng Hôn** (Phong ấn Đỏ) |
| Phòng ăn | Khó | **Oan hồn** (nhanh, máu x2.2, tầm nhìn rộng) canh **Mảnh hồn Nửa Đêm** (Phong ấn Xanh) |
| Cánh Đông + cửa chính | Khó | 2 hồn ma tuần tra, cổng phong ấn, vùng thoát |

**Checkpoint**: 4 ngọn nến (Phòng ngủ, Sảnh lớn, Hành lang giữa, Hành lang Đông) – thắp là lưu + hồi 25 máu.

---

## 5. NPC & kẻ địch

| Loại | Hành vi |
|---|---|
| **Hồn ma** | NavMesh + máy trạng thái: Idle → Patrol → Investigate → Chase → Attack → Stunned → Dead. Có mức cảnh giác 0–1 (hiện ?/!), nghe tiếng động, báo đồng bọn trong bán kính 7 m, mất dấu sau 4 s thì đi tìm |
| **Oan hồn** | Cùng AI, cấu hình mạnh hơn (EnemyConfig riêng) |
| **Tượng Gargoyle** | Bất tử, quay đầu quét; phát hiện → báo động, mọi ma trong 22 m kéo tới; Linh Quang làm lóa mắt |
| **Ivan** | Giao nhiệm vụ, chỉ đường, cửa hàng (6 mảnh vụn → bình máu, 4 → dầu đèn), ban phước khi đủ 3 Mảnh hồn |
| **Whisper** | Bị khóa trợ giúp cho tới khi hỏi Ivan; giúp làm chậm ma 40 s / mở cửa đỏ / nạp năng lượng; khiêu khích → báo động + mất máu |

Tầm nhìn của kẻ địch hiển thị bằng **nón sáng trên sàn** (bị tường cắt), đổi màu trắng → vàng → đỏ.

---

## 6. Chiến đấu & tương tác

- Collision / trigger: vật phẩm (trigger), checkpoint (trigger), vùng thoát (trigger), phong ấn / cửa / cổng (collider + NavMeshObstacle carve).
- Damage: `IDamageable` (người chơi, ma), `IStunnable` (ma, Gargoyle).
- Feedback: hạt Linh Quang, tia sáng flash, ma rung khi choáng, tan biến xoáy khi bị trục xuất, mảnh vụn rơi ra, rung camera (Cinemachine Impulse), số liệu nổi.

## 7–8. UI/UX, âm thanh & hiệu ứng

- UI Toolkit dựng bằng code, một phong cách (nền tối, viền vàng), font tiếng Việt; mọi bảng dùng chuột hoặc phím.
- **Âm thanh** (tự tạo bằng tổng hợp âm thanh): 3 nhạc nền (menu, khám phá, truy đuổi – tự chuyển khi bị đuổi), 27 hiệu ứng: bước chân (đi / chạy / ngồi), nhảy, tiếp đất, trúng đòn, chết, Linh Quang, bật đèn, ma trúng đòn / tan biến / tấn công / phát hiện, báo động, nhặt đồ, chìa khóa, Mảnh hồn, checkpoint, cửa, khóa, UI, thắng, thua, nhịp tim.
- **Hiệu ứng hạt**: lấp lánh khi nhặt, Linh Quang, trúng đòn, trục xuất (+ sóng vòng), checkpoint, Mảnh hồn, phá phong ấn, bụi tiếp đất, hồi máu, hào quang Mảnh hồn.

---

## 10. Kiến trúc kỹ thuật

```
Assets/_3DStealthGame/Scripts
├── Core        GameServices (bootstrap, chuyển scene + loading), GameSession (trạng thái lượt chơi),
│               GameEvents (event bus), GameInput (phím), GameSettings, SaveSystem, AudioManager,
│               VFXManager (pool), CameraShake, UITheme, SoundLibrary, VFXLibrary
├── Player      PlayerMovement, PlayerStats, PlayerInventory, PlayerCombat, PlayerInteractor, PlayerAnimation
├── AI          EnemyAI, SentryAI, EnemyConfig, VisionCone, NoiseSystem
├── World       Pickup, KeySeal, Checkpoint, ExitGate, ExitZone (+ Key, Door trong Tutorial_Demo)
├── Quest       QuestDefinition (ScriptableObject), QuestManager
├── Dialogue    DialogueTree (ScriptableObject), DialogueManager, NPCDialogue
├── CameraSystem CameraModeManager, CameraLookInput
├── UI          MainMenuUI, HUDController, GameHUD, DialogueUI, JournalUI, PauseMenuUI, EndScreenUI, SettingsPanel…
├── Debug       AutoPlayTest (kiểm thử tự động bản build)
└── Editor      GameBuilder (dựng toàn bộ game), SampleDialogues, DialogueTreeGraphWindow, MapCapture
```

Nguyên tắc:
- **Module hóa**: mỗi hệ thống một component; giao tiếp qua `GameEvents` / interface (`IDamageable`, `IStunnable`, `IInteractable`).
- **Data-driven**: kẻ địch (`EnemyConfig`), nhiệm vụ (`QuestDefinition`), hội thoại (`DialogueTree`), âm thanh (`SoundLibrary`), hiệu ứng (`VFXLibrary`), giao diện (`UITheme`) đều là ScriptableObject; độ khó tập trung trong `DifficultyProfile`.
- **Không phụ thuộc thứ tự scene**: `GameServices` tự khởi tạo trước scene đầu tiên (RuntimeInitializeOnLoadMethod).
- **Hiệu năng**: object pool cho âm thanh & hạt, raycast nón tầm nhìn cách frame, AI dùng NavMesh baked, không `Find` trong Update.

---

## 11. Build, chạy & kiểm thử

**Chạy bản build có sẵn**: `Builds/Windows/LemonsEscape.exe`.

**Dựng lại từ Unity**
1. Mở project → menu **Tools ▸ Stealth Game ▸ 1. Build Everything (Assets + Scenes)**.
2. Mở `Assets/_3DStealthGame/Scenes/MainMenu.unity` → Play; hoặc **Tools ▸ Stealth Game ▸ 2. Build Windows Player**.

**Dòng lệnh (không cần mở Unity)**
```
Unity.exe -batchmode -projectPath "<thư mục project>" -executeMethod StealthGame.EditorTools.GameBuilder.BatchBuildAll -logFile build.log
```

**Kiểm thử tự động** (đi hết luồng Menu → Chơi → Chiến đấu → Chết → Hồi sinh → Chiến thắng → Menu, chụp 20 ảnh + báo cáo):
```
LemonsEscape.exe -screen-fullscreen 0 -autotest -autotestOut <thư mục kết quả>
```

### Kịch bản demo trước giảng viên (~6 phút)
1. Menu: Cài đặt, Hướng dẫn → Chơi mới (Thường).
2. Nói chuyện với Ivan (hội thoại rẽ nhánh, nhiệm vụ xuất hiện), Tab xem Nhật ký.
3. Vào bếp: quan sát nón tầm nhìn, ngồi lén lấy Mảnh hồn 1; thử Linh Quang đánh lén hồn ma.
4. Đổi camera (V) để giới thiệu 4 góc nhìn.
5. Cánh Tây: lẻn qua Gargoyle lấy chìa khóa đỏ (cố tình bị thấy để minh họa báo động + nhạc truy đuổi).
6. Thắp checkpoint, cố tình chết → Game Over → Hồi sinh (save hoạt động).
7. Mở Phong ấn Đỏ / Xanh, đối mặt Oan hồn, phá cổng → màn hình Chiến thắng + xếp hạng.
