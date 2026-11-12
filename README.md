# Pixel Looter

Game **Unity 2D Pixel Art PvE Extraction Looter** cho Windows, chơi offline. Người chơi khám phá bản đồ, đánh quái, nhặt vật phẩm sáu bậc rồi rút quân để giữ chiến lợi phẩm và nâng cấp trang bị.

## Công nghệ

- Unity 2D, C#; đồ họa Pixel Art placeholder trong giai đoạn đầu.
- Góc nhìn side-view; một bản đồ cố định với spawn và loot ngẫu nhiên.
- Giao diện tiếng Việt; thời lượng mỗi trận dự kiến 20 phút.

## Trạng thái hiện tại

**Milestone 1.1–1.3 đã được nghiệm thu theo xác nhận của người dùng.** Người dùng xác nhận lỗi trang bị balo/drone của M1.5 hoạt động ổn định; kiểm thử tổng thể/save-load còn chờ. M1.6 đã được người dùng kiểm thử và xác nhận hoạt động ổn định trong Unity. Người dùng hiện xác nhận các chức năng chính M1.7 hoạt động ổn định; một số kiểm thử chi tiết chưa được xác nhận riêng. M1.8 đang triển khai: AI theo archetype, spawn theo vùng, boss hai phase, rương/corpse loot và tích hợp vòng raid đã có mã/installer; chưa tích hợp scene hoặc kiểm thử Play Mode.

## Mở bản thử nghiệm

1. Mở dự án `Looter` trong Unity.
2. Mở `Assets/Scenes/FoundationPlayground.unity`.
3. Nếu cần cài/cập nhật Loot & Inventory, chạy **Pixel Looter → Tích hợp Loot & Inventory vào FoundationPlayground**.
4. Chạy **Pixel Looter → Sắp xếp hierarchy FoundationPlayground** để nhóm các đối tượng hiện có và gán layer Player/Enemy; công cụ giữ nguyên world transform.
5. Nếu scene chưa có vòng extraction, chạy **Pixel Looter → Tích hợp Milestone 1.4 vào FoundationPlayground**. Installer có thể chạy lặp và không dựng lại scene.
6. Có thể chỉnh `Raid Duration Seconds` trên RaidManager xuống 30–60 giây để kiểm thử; giá trị mặc định là 1200 giây.
7. Nhấn **Play**. Kiểm tra Player đi xuyên World Item, vật phẩm rơi/đứng trên địa hình và vẫn nhặt được bằng E.

Nếu chưa có scene, dùng menu **Pixel Looter → Tạo scene Foundation** sau khi Unity biên dịch xong.

## BaseHub và save M1.5 (đang chờ kiểm thử Unity)

1. Chờ Unity import và compile script. Trong menu **Pixel Looter**, chạy lại **Tạo hoặc cập nhật BaseHub** để thêm/cập nhật camera nền dưới `BaseHub Systems`. Installer đồng bộ `Assets/Scenes/BaseHub.unity`, catalog và Build Settings; installer không dựng lại `FoundationPlayground`.
2. Mở scene `BaseHub` và nhấn Play. Profile JSON được tạo/nạp tại `Application.persistentDataPath`; save mới nhận 1.000 Xu một lần. Bản save hỏng sẽ được giữ nguyên và profile chuyển sang chỉ đọc hoặc dùng backup chỉ đọc.
3. Tab **KHO ĐỒ** cho phép kéo vật phẩm giữa kho, balo, ô an toàn và ba slot trang bị luôn hiện diện. Khi chưa trang bị balo, dung lượng cơ bản là 10 ô; balo I có 10 ô, balo II có 14 ô. Bấm **BÁN VẬT PHẨM ĐÃ CHỌN** để xác nhận bán vật phẩm đang trong kho. Tab **CỬA HÀNG** cho phép mua vào kho.
4. Nhấn **BẮT ĐẦU RAID** để vào `FoundationPlayground`. Extraction giữ balo/ô an toàn và quay về BaseHub; chết/hết giờ xử lý đồ mang theo và trang bị theo luật rủi ro hiện tại.
5. Sau khi installer chạy, kiểm tra Console không còn cảnh báo không có camera; kiểm tra tab không chồng, kéo thả/đổi trang bị, giao dịch, thoát/mở lại, extraction và thất bại. Các nội dung này đang chờ kiểm thử Play Mode trước khi nghiệm thu M1.5.

**Chẩn đoán trang bị M1.5:** Trong Unity Editor, chạy **Pixel Looter → Chạy tự kiểm tra giao dịch trang bị (save tạm)** để kiểm tra giao dịch trên profile cô lập, không dùng save người chơi. Để ghi log đường kéo thả trong Play Mode, bật `Log Drag Diagnostics` trên `BaseHubCanvas` và `Log Equipment Transactions` trên `PlayerProfileStore`. Log ghi vị trí nguồn/đích, `pointerDrag`, raycast tại điểm thả và kết quả giao dịch. Nút **TRANG BỊ** trong bảng thông tin vật phẩm và kéo thả đi qua cùng giao dịch profile.

## Milestone 1.6 — Drone & Damage (đã nghiệm thu)

Người dùng đã kiểm thử Milestone 1.6 trong Unity và xác nhận hệ thống hoạt động ổn định. M1.6 gồm drone theo sau và hấp thụ sát thương theo bậc, HUD độ bền, Repair Kit dùng trong raid, sửa drone tại BaseHub và lưu độ bền theo ItemInstance. Installer và menu self-test vẫn có thể dùng để tích hợp hoặc chẩn đoán bổ sung; chưa có xác nhận riêng rằng menu self-test tự động đã được chạy.

## Milestone 1.7 — Vũ khí & đạn (người dùng xác nhận chức năng chính ổn định)

1. Installer M1.7 đã chạy trong Unity và báo tích hợp thành công. Nếu cần đồng bộ lại sau thay đổi, chạy **Pixel Looter → Tích hợp Milestone 1.7 — Vũ khí & Đạn**. Installer tạo/cập nhật definitions cho gậy, dao I–III, kiếm III–VI, cung I–IV, súng lục III–VI, hai loại ammo, placeholder icon/projectile, catalog, shop và loot table; không dựng lại scene.
2. Mở BaseHub, mua vũ khí và ammo rồi chuyển vũ khí vào WeaponSlot, ammo vào balo. Súng lục bắt đầu với băng rỗng, không được cấp đạn miễn phí; nạp bằng R khi vào raid.
3. Mở `FoundationPlayground` và kiểm thử cận chiến/chuột trái, cung ngắm-bắn bằng chuột, súng lục bắn chuột trái và nạp bằng R. Ammo chỉ được lấy từ balo; extraction giữ đồ còn lại, thất bại theo luật mất đồ hiện có.
4. Self-test đã chạy trong Unity và báo `[PixelLooter][WeaponSelfTest] ĐẠT`: kiểm tra stack 40+30 thành 60+10, tiêu hao ammo, tách loại ammo và round-trip magazine/Instance ID; không dùng save người chơi. Có thể chạy lại bằng **Pixel Looter → Chạy tự kiểm tra vũ khí & đạn (không save thật)**.
5. Play Mode checklist chi tiết nằm trong `TASKS.md`. Tách stack thủ công chưa triển khai; các kiểm tra về ngắm, hitbox, projectile, UI, shop/loot và save/extraction vẫn cần chạy trước nghiệm thu.

## Milestone 1.8 — Enemy Variety & Bosses (đang chờ tích hợp Unity)

1. Dừng Play Mode sau khi đã chủ động bảo đảm dữ liệu căn cứ được lưu; mở `Assets/Scenes/FoundationPlayground.unity`.
2. Chạy **Pixel Looter → Tích hợp Milestone 1.8 — Enemy & Boss**. Installer tạo cấu hình/prefab enemy, điểm spawn theo ba vùng, boss arena và BossChest idempotent; không dựng lại map và giữ nguyên transform scene đã có.
3. Chạy **Pixel Looter → Chạy tự kiểm tra Enemy & Boss (không save thật)**. Bài kiểm tra dùng object tạm, không đọc/ghi save người chơi.
4. Play Mode checklist về Slime/Raider/Archer/Elite, Line of Sight, đạn, leash, boss phase/telegraph, rương, hòm xác, extraction và raid mới nằm trong `TASKS.md`.
5. Chưa đánh dấu M1.8 hoàn thành trước khi chạy installer, kiểm tra Console/self-test và kiểm thử Play Mode.

**Điều khiển:** `A/D` hoặc `←/→` để đi; `Space`, `W` hoặc `↑` để nhảy; `E` tương tác một mục tiêu gần nhất (nhặt, mở container hoặc rút quân); `I` hoặc `Tab` mở inventory; chuột trái tấn công; `R` nạp đạn khi đang dùng súng lục. Khi raid kết thúc, dùng **Bắt đầu trận mới** trên màn hình kết quả. Loot rút được chỉ giữ trong bộ nhớ phiên, chưa được lưu vĩnh viễn.

## Background của FoundationPlayground

VISUAL-01 Parallax đã bị hủy theo quyết định thiết kế. Phương án tiếp theo là một background panorama cố định phủ vùng camera; chưa được triển khai. Năm PNG Parallax cũ được giữ làm tài liệu tham khảo tại `Assets/Art/Background/Parallax/`.

## Tài liệu dự án

- `AGENTS.md` — quy tắc làm việc dành cho AI.
- `PLAN.md` — mục tiêu và lộ trình.
- `TASKS.md` — checklist công việc.
- `changelog.md` — lịch sử thay đổi (mới nhất ở trên cùng, giờ Việt Nam).

Chỉ triển khai từng milestone sau khi được yêu cầu và kiểm thử trước khi tiếp tục.
