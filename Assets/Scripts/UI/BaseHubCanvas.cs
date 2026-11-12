using System.Collections.Generic;
using Looter.Combat;
using Looter.Economy;
using Looter.Hub;
using Looter.Items;
using Looter.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Looter.UI
{
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public sealed class BaseHubCanvas : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Font uiFont;
        [SerializeField] private Sprite characterPortrait;
        [SerializeField] private bool logDragDiagnostics;

        private readonly List<InventorySlotView> slots = new List<InventorySlotView>();
        private Canvas canvas;
        private BaseHubController controller;
        private PlayerProfileStore profile;
        private Text currencyText;
        private Text bagTitle;
        private Text stashTitle;
        private Text detailText;
        private Text statusText;
        private Text carriedValueText;
        private Button equipButton;
        private GameObject inventoryPanel;
        private GameObject shopPanel;
        private GameObject repairPanel;
        private Text repairInfoText;
        private Button repairDroneButton;
        private GameObject confirmPanel;
        private InventorySlotView selectedSlot;
        private PlayerStorage selectedStorage;
        private int selectedIndex = -1;
        private PlayerStorage equipSourceStorage;
        private int equipSourceIndex = -1;
        private bool isDragging;
        private PlayerStorage dragSourceStorage;
        private int dragSourceIndex = -1;
        private Image dragGhost;
        private bool built;

        private static readonly Color PanelColor = new Color32(24, 30, 38, 246);
        private static readonly Color PanelLight = new Color32(37, 46, 58, 255);
        private static readonly Color Accent = new Color32(82, 184, 195, 255);

        private void Start()
        {
            canvas = GetComponent<Canvas>();
            controller = FindAnyObjectByType<BaseHubController>();
            profile = PlayerProfileStore.Instance;
            if (profile == null) profile = FindAnyObjectByType<PlayerProfileStore>();
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void OnDestroy()
        {
            if (profile != null) profile.Changed -= RefreshAll;
        }

        public ItemDefinition GetDefinition(PlayerStorage storage, int index) => profile != null
            ? profile.GetDefinition(storage, index) : null;
        public int GetQuantity(PlayerStorage storage, int index) => profile != null
            ? profile.GetQuantity(storage, index) : 0;

        public bool IsSelected(PlayerStorage storage, int index) =>
            selectedIndex == index && selectedStorage == storage;

        public void Select(PlayerStorage storage, int index)
        {
            selectedStorage = storage;
            selectedIndex = index;
            selectedSlot = null;
            foreach (InventorySlotView slot in slots)
                if (slot.Storage == storage && slot.Index == index) { selectedSlot = slot; break; }
            ShowItemDetails(storage, index);
            RefreshSlots();
        }

        public void ShowItemDetails(PlayerStorage storage, int index)
        {
            if (detailText == null) return;
            ItemDefinition item = GetDefinition(storage, index);
            UpdateEquipButton(storage, index, item);
            if (item == null)
            {
                if (storage == selectedStorage && index == selectedIndex) detailText.text = "Chọn một vật phẩm để xem thông tin.";
                return;
            }
            detailText.text = $"{item.DisplayName}  ·  Bậc {item.TierLabel}\n{item.Description}\nGiá bán: {item.SellValue} Xu";
            if (item.Type == ItemType.Drone && profile != null)
            {
                ItemStack drone = profile.GetItemStack(storage, index);
                if (drone != null)
                    detailText.text += $"\nĐộ bền: {drone.CurrentDurability}/{drone.EffectiveMaxDurability} " +
                                       $"(gốc {drone.BaseMaxDurability}) — {(drone.IsDroneDisabled ? "Đã vô hiệu hóa" : "Đang hoạt động")}";
            }
        }

        public void RestoreSelectedDetails()
        {
            if (selectedIndex >= 0) ShowItemDetails(selectedStorage, selectedIndex);
            else UpdateEquipButton(PlayerStorage.Stash, -1, null);
        }

        public void BeginDrag(PlayerStorage storage, int index, PointerEventData eventData)
        {
            ItemDefinition item = GetDefinition(storage, index);
            if (item == null || canvas == null) return;
            isDragging = true;
            dragSourceStorage = storage;
            dragSourceIndex = index;
            Select(storage, index);
            GameObject ghostObject = new GameObject("Vật phẩm đang kéo", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            ghostObject.transform.SetParent(canvas.transform, false);
            dragGhost = ghostObject.GetComponent<Image>();
            dragGhost.sprite = item.Icon;
            dragGhost.color = item.Icon != null ? Color.white : item.TierColor;
            dragGhost.raycastTarget = false;
            RectTransform rect = dragGhost.rectTransform;
            rect.sizeDelta = new Vector2(62f, 62f);
            CanvasGroup group = ghostObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0.8f;
            MoveDrag(eventData);
        }

        public void MoveDrag(PointerEventData eventData)
        {
            if (dragGhost == null) return;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position,
                    eventData.pressEventCamera, out Vector2 localPosition))
                dragGhost.rectTransform.localPosition = localPosition;
        }

        public void EndDrag()
        {
            if (dragGhost != null) Destroy(dragGhost.gameObject);
            dragGhost = null;
            isDragging = false;
            dragSourceIndex = -1;
            foreach (InventorySlotView slot in slots)
                if (slot != null) slot.SetDropHighlight(false, false);
        }

        public bool IsDragging => isDragging;

        public bool CanDropOn(PlayerStorage storage, int index, out string rejection)
        {
            if (!isDragging || dragSourceIndex < 0)
            { rejection = "Chưa có vật phẩm đang được kéo."; return false; }
            if (controller == null) { rejection = "Không tìm thấy bộ điều khiển BaseHub."; return false; }
            return controller.CanMove(dragSourceStorage, dragSourceIndex, storage, index, out rejection);
        }

        public void HighlightDropTarget(InventorySlotView target)
        {
            if (target == null || !isDragging) return;
            bool valid = CanDropOn(target.Storage, target.Index, out _);
            target.SetDropHighlight(true, valid);
        }

        public bool DropOn(PlayerStorage sourceStorage, int sourceIndex, PlayerStorage destinationStorage, int destinationIndex)
        {
            string itemId = profile != null ? profile.GetDefinition(sourceStorage, sourceIndex)?.Id : null;
            bool moved = MoveItem(sourceStorage, sourceIndex, destinationStorage, destinationIndex);
            if (logDragDiagnostics)
            {
                string outcome = moved ? "thành công" : (profile != null ? profile.StatusMessage : "không có profile");
                Debug.Log($"[PixelLooter][Drag] Giao dịch item={itemId ?? "?"}; nguồn={sourceStorage}[{sourceIndex}]; " +
                          $"đích={destinationStorage}[{destinationIndex}]; kết quả={outcome}.", this);
            }
            return moved;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;
            if (logDragDiagnostics)
            {
                List<RaycastResult> hits = new List<RaycastResult>();
                if (EventSystem.current != null) EventSystem.current.RaycastAll(eventData, hits);
                List<string> hitSummary = new List<string>();
                foreach (RaycastResult hit in hits)
                {
                    InventorySlotView slot = hit.gameObject.GetComponentInParent<InventorySlotView>();
                    hitSummary.Add(slot != null ? $"{hit.gameObject.name}->{slot.Storage}[{slot.Index}]" : hit.gameObject.name);
                    if (hitSummary.Count >= 8) break;
                }
                Debug.LogWarning($"[PixelLooter][Drag] Canvas nhận OnDrop thay slot; pointerDrag={eventData.pointerDrag.name}; " +
                                 $"pointerEnter={eventData.pointerEnter?.name ?? "null"}; raycasts={string.Join(" | ", hitSummary)}.", this);
            }
            SetStatus("Hãy thả vật phẩm trực tiếp vào một ô inventory hợp lệ.");
        }

        private bool MoveItem(PlayerStorage sourceStorage, int sourceIndex, PlayerStorage destinationStorage, int destinationIndex)
        {
            if (profile == null)
            {
                SetStatus("Không tìm thấy hồ sơ người chơi; không thể trang bị.");
                LogDragEvent("Từ chối giao dịch: profile null.");
                return false;
            }
            if (sourceIndex < 0)
            {
                SetStatus("Ô nguồn không hợp lệ; hãy chọn lại vật phẩm.");
                LogDragEvent("Từ chối giao dịch: source index không hợp lệ.");
                return false;
            }
            selectedStorage = sourceStorage;
            selectedIndex = sourceIndex;
            bool moved = controller != null && controller.TryMove(sourceStorage, sourceIndex, destinationStorage, destinationIndex);
            if (moved)
            {
                if (selectedStorage == sourceStorage && selectedIndex == sourceIndex)
                {
                    selectedIndex = -1;
                    selectedSlot = null;
                }
                SetStatus("Đã chuyển vật phẩm và lưu tiến trình.");
            }
            else SetStatus(profile == null ? "Không tìm thấy hồ sơ người chơi." : controller == null ? "Không tìm thấy bộ điều khiển BaseHub." :
                (!string.IsNullOrEmpty(profile.StatusMessage) ? profile.StatusMessage : "Không thể chuyển vật phẩm vào ô này."));
            RefreshAll();
            return moved;
        }

        private void EquipSelected()
        {
            if (equipSourceIndex < 0 || profile == null) return;
            ItemDefinition definition = GetDefinition(equipSourceStorage, equipSourceIndex);
            if (definition == null) { SetStatus("Vật phẩm đã rời khỏi ô được chọn."); return; }
            PlayerStorage destination = EquipmentStorageFor(definition.Type);
            if (destination == PlayerStorage.Stash) { SetStatus("Chỉ có thể trang bị vũ khí, balo hoặc drone."); return; }
            MoveItem(equipSourceStorage, equipSourceIndex, destination, 0);
        }

        private void UpdateEquipButton(PlayerStorage storage, int index, ItemDefinition definition)
        {
            if (equipButton == null) return;
            PlayerStorage destination = definition != null ? EquipmentStorageFor(definition.Type) : PlayerStorage.Stash;
            bool canEquip = definition != null && destination != PlayerStorage.Stash &&
                            storage != destination && storage != PlayerStorage.Safe && index >= 0;
            equipSourceStorage = storage;
            equipSourceIndex = canEquip ? index : -1;
            equipButton.gameObject.SetActive(canEquip);
        }

        private static PlayerStorage EquipmentStorageFor(ItemType type) => type switch
        {
            ItemType.Weapon => PlayerStorage.WeaponEquipment,
            ItemType.Backpack => PlayerStorage.BackpackEquipment,
            ItemType.Drone => PlayerStorage.DroneEquipment,
            _ => PlayerStorage.Stash
        };

        public bool LogDragDiagnostics => logDragDiagnostics;

        public void LogDragEvent(string message)
        {
            if (logDragDiagnostics) Debug.Log($"[PixelLooter][Drag] {message}", this);
        }

        private void Build()
        {
            if (built) return;
            built = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)transform;
            Image background = GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = new Color32(12, 17, 23, 255);

            BuildHeader(root);
            BuildInventory(root);
            BuildShop(root);
            BuildRepair(root);
            BuildFooter(root);
            BuildConfirmDialog(root);
            if (profile != null) profile.Changed += RefreshAll;
            if (profile == null || !profile.IsReady)
                SetStatus(profile != null ? profile.StatusMessage : "Không tìm thấy dữ liệu người chơi.");
            else if (profile.IsReadOnly)
                SetStatus(profile.StatusMessage);
            else if (!string.IsNullOrEmpty(profile.LastRaidSummary))
                SetStatus(profile.LastRaidSummary);
            ShowInventory();
            if (logDragDiagnostics)
            {
                EventSystem eventSystem = EventSystem.current;
                GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
                Debug.Log($"[PixelLooter][BaseHubUI] EventSystem={(eventSystem != null ? eventSystem.name : "THIẾU")}; " +
                          $"GraphicRaycaster={(raycaster != null && raycaster.isActiveAndEnabled ? "bật" : "THIẾU/TẮT")}; " +
                          $"Canvas={canvas.renderMode}.", this);
                foreach (InventorySlotView slot in slots)
                {
                    if (slot.Storage != PlayerStorage.WeaponEquipment && slot.Storage != PlayerStorage.BackpackEquipment &&
                        slot.Storage != PlayerStorage.DroneEquipment) continue;
                    Image slotImage = slot.GetComponent<Image>();
                    LogDragEvent($"Slot={slot.Storage}[{slot.Index}]; active={slot.gameObject.activeInHierarchy}; " +
                                 $"InventorySlotView={slot != null}; Image={(slotImage != null ? "có" : "thiếu")}; " +
                                 $"raycastTarget={(slotImage != null && slotImage.raycastTarget)}; handlerDrop={slot is IDropHandler}.");
                }
            }
            RefreshAll();
        }

        private void BuildHeader(RectTransform root)
        {
            RectTransform header = CreatePanel(root, "Thanh trên", 12f, 12f, 1256f, 76f, PanelColor);
            CreateText(header, "Tiêu đề", "PIXEL LOOTER", 22, FontStyle.Bold, TextAnchor.MiddleLeft,
                22f, 14f, 230f, 44f, Accent);
            Button inventoryTab = CreateButton(header, "Tab Kho đồ", "KHO ĐỒ", 17, 248f, 18f, 130f, 40f, PanelLight);
            inventoryTab.onClick.AddListener(() => ShowInventory());
            Button shopTab = CreateButton(header, "Tab Cửa hàng", "CỬA HÀNG", 17, 384f, 18f, 150f, 40f, PanelLight);
            shopTab.onClick.AddListener(() => ShowShop());
            Button repairTab = CreateButton(header, "Tab Sửa chữa", "SỬA CHỮA", 16, 540f, 18f, 138f, 40f, PanelLight);
            repairTab.onClick.AddListener(ShowRepair);
            currencyText = CreateText(header, "Số dư", "Xu: 0", 20, FontStyle.Bold, TextAnchor.MiddleRight,
                1000f, 14f, 230f, 42f, new Color32(246, 203, 94, 255));
        }

        private void BuildInventory(RectTransform root)
        {
            inventoryPanel = new GameObject("Màn hình kho đồ", typeof(RectTransform));
            inventoryPanel.transform.SetParent(root, false);
            RectTransform inventoryRect = inventoryPanel.GetComponent<RectTransform>();
            SetTopLeft(inventoryRect, 12f, 98f, 1256f, 508f);

            RectTransform character = CreatePanel(inventoryRect, "Khu vực nhân vật", 0f, 0f, 242f, 508f, PanelColor);
            CreateText(character, "Nhân vật", "NHÂN VẬT", 18, FontStyle.Bold, TextAnchor.MiddleLeft, 16f, 12f, 210f, 28f, Accent);
            Image portrait = CreateImage(character, "Ảnh nhân vật", 54f, 48f, 134f, 106f, new Color32(44, 59, 67, 255));
            if (characterPortrait != null) { portrait.sprite = characterPortrait; portrait.preserveAspect = true; }
            CreateText(character, "Chân dung", "NHÀ THÁM HIỂM", 14, FontStyle.Bold, TextAnchor.MiddleCenter, 54f, 117f, 134f, 28f, Color.white);
            CreateText(character, "Trang bị", "TRANG BỊ", 15, FontStyle.Bold, TextAnchor.MiddleLeft, 16f, 168f, 210f, 26f, Accent);
            CreateEquipmentSlot(character, "VŨ KHÍ", PlayerStorage.WeaponEquipment, 0, 202f);
            CreateEquipmentSlot(character, "BALO", PlayerStorage.BackpackEquipment, 1, 280f);
            CreateEquipmentSlot(character, "DRONE", PlayerStorage.DroneEquipment, 2, 358f);
            CreateText(character, "Thông tin trang bị", "Các ô trang bị chỉ nhận đúng loại vật phẩm.", 12,
                FontStyle.Normal, TextAnchor.UpperLeft, 14f, 438f, 215f, 58f, Color.white);

            RectTransform carried = CreatePanel(inventoryRect, "Khu vực balo", 254f, 0f, 366f, 508f, PanelColor);
            bagTitle = CreateText(carried, "Tiêu đề balo", "BALO", 18, FontStyle.Bold, TextAnchor.MiddleLeft, 16f, 12f, 330f, 28f, Accent);
            CreateText(carried, "Chỉ dẫn balo", "Kéo vật phẩm để chuyển hoặc hoán đổi.", 12,
                FontStyle.Normal, TextAnchor.MiddleLeft, 16f, 38f, 330f, 22f, Color.white);
            for (int i = 0; i < 30; i++)
                CreateSlot(carried, PlayerStorage.Carried, i, 16f + (i % 5) * 59f, 66f + (i / 5) * 56f, 52f);
            CreateText(carried, "Ô an toàn", "Ô AN TOÀN", 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                16f, 410f, 180f, 25f, Accent);
            CreateSlot(carried, PlayerStorage.Safe, 0, 16f, 440f, 52f);
            CreateSlot(carried, PlayerStorage.Safe, 1, 76f, 440f, 52f);

            RectTransform stash = CreatePanel(inventoryRect, "Khu vực kho vĩnh viễn", 634f, 0f, 622f, 508f, PanelColor);
            stashTitle = CreateText(stash, "Tiêu đề kho", "KHO VĨNH VIỄN — 60 Ô", 18, FontStyle.Bold,
                TextAnchor.MiddleLeft, 16f, 12f, 580f, 28f, Accent);
            CreateText(stash, "Chỉ dẫn kho", "Kéo đồ sang balo, ô an toàn hoặc trang bị.", 12,
                FontStyle.Normal, TextAnchor.MiddleLeft, 16f, 38f, 580f, 22f, Color.white);
            CreateStashScroll(stash);
            detailText = CreateText(stash, "Thông tin vật phẩm", "Chọn một vật phẩm để xem thông tin.",
                12, FontStyle.Normal, TextAnchor.MiddleLeft, 16f, 402f, 590f, 46f, Color.white);
            equipButton = CreateButton(stash, "Trang bị vật phẩm", "TRANG BỊ", 13,
                16f, 454f, 160f, 38f, Accent);
            equipButton.onClick.AddListener(EquipSelected);
            equipButton.gameObject.SetActive(false);
            Button sell = CreateButton(stash, "Bán vật phẩm", "BÁN VẬT PHẨM ĐÃ CHỌN", 13,
                184f, 454f, 220f, 38f, new Color32(124, 65, 54, 255));
            sell.onClick.AddListener(ConfirmSell);
            statusText = CreateText(inventoryRect, "Thông báo", string.Empty, 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, 270f, 480f, 360f, 24f, new Color32(255, 224, 152, 255));
        }

        private void CreateEquipmentSlot(RectTransform parent, string title, PlayerStorage storage, int index, float y)
        {
            CreateText(parent, title + " nhãn", title, 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                14f, y + 9f, 74f, 28f, Color.white);
            CreateSlot(parent, storage, index, 94f, y, 58f, emptyLabel: title);
            ItemDefinition current = GetDefinition(storage, index);
            CreateText(parent, title + " phụ đề", current != null ? current.DisplayName : "Trống",
                11, FontStyle.Normal, TextAnchor.MiddleLeft, 158f, y + 6f, 76f, 42f, Color.white);
        }

        private void CreateStashScroll(RectTransform parent)
        {
            GameObject viewportObject = new GameObject("Kho cuộn", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportObject.transform.SetParent(parent, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            SetTopLeft(viewport, 12f, 66f, 598f, 330f);
            viewportObject.GetComponent<Image>().color = new Color32(17, 23, 30, 255);

            GameObject contentObject = new GameObject("Ô kho", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(-12f, 0f);
            GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(82f, 78f);
            grid.spacing = new Vector2(6f, 6f);
            grid.padding = new RectOffset(6, 6, 6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < 60; i++) CreateSlot(content, PlayerStorage.Stash, i, 0f, 0f, 82f, 78f, useLayout: true);
        }

        private void BuildShop(RectTransform root)
        {
            shopPanel = new GameObject("Màn hình cửa hàng", typeof(RectTransform), typeof(Image));
            shopPanel.transform.SetParent(root, false);
            RectTransform rect = shopPanel.GetComponent<RectTransform>();
            SetTopLeft(rect, 12f, 98f, 1256f, 508f);
            shopPanel.GetComponent<Image>().color = PanelColor;
            CreateText(rect, "Cửa hàng tiêu đề", "CỬA HÀNG CĂN CỨ", 22, FontStyle.Bold,
                TextAnchor.MiddleLeft, 22f, 16f, 420f, 40f, Accent);
            CreateText(rect, "Cửa hàng mô tả", "Giá mua và vật phẩm được cấu hình trong ShopCatalog.", 14,
                FontStyle.Normal, TextAnchor.MiddleLeft, 22f, 60f, 600f, 30f, Color.white);
            GameObject viewportObject = new GameObject("Danh sách hàng hóa", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportObject.transform.SetParent(rect, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            SetTopLeft(viewport, 22f, 106f, 1200f, 380f);
            viewportObject.GetComponent<Image>().color = new Color32(17, 23, 30, 255);
            GameObject contentObject = new GameObject("Nội dung hàng hóa", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform offerRect = contentObject.GetComponent<RectTransform>();
            offerRect.anchorMin = new Vector2(0f, 1f);
            offerRect.anchorMax = new Vector2(1f, 1f);
            offerRect.pivot = new Vector2(0f, 1f);
            offerRect.anchoredPosition = Vector2.zero;
            offerRect.sizeDelta = new Vector2(-12f, 0f);
            GridLayoutGroup offersGrid = contentObject.GetComponent<GridLayoutGroup>();
            offersGrid.cellSize = new Vector2(280f, 120f);
            offersGrid.spacing = new Vector2(12f, 12f);
            offersGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            offersGrid.constraintCount = 4;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect offerScroll = viewportObject.GetComponent<ScrollRect>();
            offerScroll.viewport = viewport;
            offerScroll.content = offerRect;
            offerScroll.horizontal = false;
            offerScroll.vertical = true;
            offerScroll.movementType = ScrollRect.MovementType.Clamped;

            if (controller != null && controller.Shop != null)
            {
                foreach (ShopOffer offer in controller.Shop.Offers)
                {
                    if (offer == null || offer.Item == null) continue;
                    RectTransform card = CreatePanel(offerRect, "Thẻ " + offer.Item.Id, 0f, 0f, 280f, 120f, PanelLight);
                    CreateText(card, "Tên " + offer.Item.Id, offer.Item.DisplayName, 15, FontStyle.Bold,
                        TextAnchor.MiddleLeft, 12f, 8f, 194f, 28f, offer.Item.TierColor);
                    CreateText(card, "Giá " + offer.Item.Id, $"Bậc {offer.Item.TierLabel}  ·  ×{offer.PurchaseQuantity}  ·  {(long)offer.BuyPrice * offer.PurchaseQuantity} Xu",
                        13, FontStyle.Normal, TextAnchor.MiddleLeft, 12f, 39f, 194f, 24f, Color.white);
                    Button buy = CreateButton(card, "Mua " + offer.Item.Id, "MUA", 13, 204f, 72f, 64f, 34f, Accent);
                    buy.onClick.AddListener(() => Buy(offer));
                    if (offer.Item.Icon != null)
                    {
                        Image icon = CreateImage(card, "Icon " + offer.Item.Id, 210f, 10f, 48f, 48f, Color.white);
                        icon.sprite = offer.Item.Icon;
                        icon.preserveAspect = true;
                    }
                }
            }
        }

        private void BuildRepair(RectTransform root)
        {
            repairPanel = new GameObject("Màn hình sửa chữa", typeof(RectTransform), typeof(Image));
            repairPanel.transform.SetParent(root, false);
            RectTransform rect = repairPanel.GetComponent<RectTransform>();
            SetTopLeft(rect, 12f, 98f, 1256f, 508f);
            repairPanel.GetComponent<Image>().color = PanelColor;
            CreateText(rect, "Tiêu đề sửa chữa", "SỬA CHỮA DRONE", 22, FontStyle.Bold,
                TextAnchor.MiddleLeft, 30f, 24f, 520f, 42f, Accent);
            repairInfoText = CreateText(rect, "Thông tin drone sửa chữa", string.Empty, 18, FontStyle.Normal,
                TextAnchor.UpperLeft, 40f, 100f, 720f, 210f, Color.white);
            repairDroneButton = CreateButton(rect, "Nút sửa drone", "SỬA CHỮA", 18,
                40f, 340f, 240f, 54f, Accent);
            repairDroneButton.onClick.AddListener(RepairEquippedDrone);
        }

        private void BuildFooter(RectTransform root)
        {
            RectTransform footer = CreatePanel(root, "Thanh dưới", 12f, 612f, 1256f, 92f, PanelColor);
            carriedValueText = CreateText(footer, "Giá trị mang theo", "Giá trị balo và ô an toàn: 0 Xu",
                14, FontStyle.Bold, TextAnchor.MiddleLeft, 18f, 10f, 720f, 30f, new Color32(246, 203, 94, 255));
            Button raid = CreateButton(footer, "Bắt đầu raid", "BẮT ĐẦU RAID", 18, 940f, 18f, 280f, 56f, Accent);
            raid.onClick.AddListener(StartRaid);
        }

        private void BuildConfirmDialog(RectTransform root)
        {
            RectTransform overlay = CreatePanel(root, "Xác nhận bán", 0f, 0f, 1280f, 720f, new Color32(0, 0, 0, 180));
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            RectTransform box = CreatePanel(overlay, "Hộp xác nhận", 410f, 260f, 460f, 190f, PanelColor);
            CreateText(box, "Xác nhận nội dung", "Bán vật phẩm đã chọn?", 19, FontStyle.Bold,
                TextAnchor.MiddleCenter, 20f, 25f, 420f, 50f, Color.white);
            Button yes = CreateButton(box, "Đồng ý bán", "BÁN", 15, 78f, 116f, 130f, 42f, new Color32(149, 65, 55, 255));
            yes.onClick.AddListener(ConfirmSellNow);
            Button no = CreateButton(box, "Hủy bán", "HỦY", 15, 250f, 116f, 130f, 42f, PanelLight);
            no.onClick.AddListener(() => confirmPanel.SetActive(false));
            confirmPanel = overlay.gameObject;
            confirmPanel.SetActive(false);
        }

        private InventorySlotView CreateSlot(RectTransform parent, PlayerStorage storage, int index,
            float x, float y, float size, float height = -1f, bool useLayout = false, string emptyLabel = null)
        {
            float actualHeight = height > 0f ? height : size;
            GameObject slot = new GameObject($"Ô {storage} {index + 1}", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(parent, false);
            RectTransform rect = slot.GetComponent<RectTransform>();
            if (!useLayout) SetTopLeft(rect, x, y, size, actualHeight);
            Image background = slot.GetComponent<Image>();
            background.color = new Color32(51, 59, 70, 255);
            background.raycastTarget = true;
            Image icon = CreateImage(slot.transform, "Biểu tượng", 5f, 4f, size - 10f, actualHeight - 23f, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Text label = CreateText(slot.transform, "Nhãn ô", $"Ô {index + 1}", 10, FontStyle.Normal,
                TextAnchor.MiddleCenter, 2f, actualHeight - 22f, size - 4f, 22f, Color.white);
            label.raycastTarget = false;
            InventorySlotView slotView = slot.AddComponent<InventorySlotView>();
            slotView.Configure(this, storage, index, background, icon, label, emptyLabel);
            slots.Add(slotView);
            return slotView;
        }

        private void RefreshAll()
        {
            if (!built) return;
            RefreshSlots();
            if (profile == null || !profile.IsReady) return;
            if (currencyText != null) currencyText.text = $"{profile.Currency:N0} Xu";
            if (bagTitle != null)
            {
                ItemDefinition equippedBackpack = GetDefinition(PlayerStorage.BackpackEquipment, 0);
                string capacityLabel = equippedBackpack == null ? "BALO CƠ BẢN" : $"BALO BẬC {equippedBackpack.TierLabel}";
                bagTitle.text = $"{capacityLabel} — {CountOccupied(PlayerStorage.Carried)}/{profile.Capacity(PlayerStorage.Carried)} Ô";
            }
            if (stashTitle != null) stashTitle.text = $"KHO VĨNH VIỄN — {CountOccupied(PlayerStorage.Stash)}/60 Ô";
            int carriedValue = SumValue(PlayerStorage.Carried) + SumValue(PlayerStorage.Safe);
            if (carriedValueText != null) carriedValueText.text = $"Giá trị balo và ô an toàn: {carriedValue:N0} Xu";
            RefreshLoadoutLabels();
            RefreshRepairPanel();
            if (!string.IsNullOrEmpty(profile.StatusMessage)) SetStatus(profile.StatusMessage);
        }

        private void RefreshSlots()
        {
            foreach (InventorySlotView slot in slots)
            {
                if (slot == null) continue;
                slot.gameObject.SetActive(profile != null && slot.Index < profile.Capacity(slot.Storage));
                if (slot.gameObject.activeSelf) slot.Refresh();
            }
        }

        private void RefreshLoadoutLabels()
        {
            foreach (Transform child in inventoryPanel.transform.Find("Khu vực nhân vật"))
            {
                if (child.name == "VŨ KHÍ phụ đề") SetChildText(child, PlayerStorage.WeaponEquipment);
                else if (child.name == "BALO phụ đề") SetChildText(child, PlayerStorage.BackpackEquipment);
                else if (child.name == "DRONE phụ đề") SetChildText(child, PlayerStorage.DroneEquipment);
            }
        }

        private void SetChildText(Transform child, PlayerStorage storage)
        {
            Text text = child.GetComponent<Text>();
            ItemDefinition item = GetDefinition(storage, 0);
            if (text != null) text.text = item != null ? item.DisplayName : "Trống";
        }

        private int CountOccupied(PlayerStorage storage)
        {
            int count = 0;
            for (int i = 0; i < profile.Capacity(storage); i++) if (GetDefinition(storage, i) != null) count++;
            return count;
        }

        private int SumValue(PlayerStorage storage)
        {
            int value = 0;
            for (int i = 0; i < profile.Capacity(storage); i++)
            {
                ItemDefinition item = GetDefinition(storage, i);
                if (item != null) value += item.SellValue * GetQuantity(storage, i);
            }
            return value;
        }

        private void ShowInventory()
        {
            inventoryPanel.SetActive(true);
            shopPanel.SetActive(false);
            repairPanel.SetActive(false);
        }

        private void ShowShop()
        {
            inventoryPanel.SetActive(false);
            shopPanel.SetActive(true);
            repairPanel.SetActive(false);
        }

        private void ShowRepair()
        {
            inventoryPanel.SetActive(false);
            shopPanel.SetActive(false);
            repairPanel.SetActive(true);
            RefreshRepairPanel();
        }

        private void RefreshRepairPanel()
        {
            if (repairInfoText == null || controller == null) return;
            ItemStack drone = profile != null ? profile.GetItemStack(PlayerStorage.DroneEquipment, 0) : null;
            if (drone == null || drone.Item == null)
            {
                repairInfoText.text = "Chưa trang bị drone. Hãy trang bị drone tại tab KHO ĐỒ trước khi sửa.";
                if (repairDroneButton != null) repairDroneButton.interactable = false;
                return;
            }

            bool canRepair = DroneRepairService.TryCalculateBaseHubRepair(drone, controller.DroneSettings,
                out DroneRepairResult repair, out string reason);
            string condition = drone.IsDroneDisabled ? "Đã vô hiệu hóa" : "Đang hoạt động";
            if (canRepair)
                repairInfoText.text = $"{drone.Item.DisplayName} — Bậc {drone.Item.TierLabel}\n" +
                                      $"Độ bền: {drone.CurrentDurability}/{drone.EffectiveMaxDurability}\n" +
                                      $"Độ bền gốc: {drone.BaseMaxDurability}\nTrạng thái: {condition}\n" +
                                      $"Giá sửa: {repair.Cost} Xu\nĐộ bền tối đa sau sửa: {repair.EffectiveMaxDurability}";
            else
                repairInfoText.text = $"{drone.Item.DisplayName} — Bậc {drone.Item.TierLabel}\n" +
                                      $"Độ bền: {drone.CurrentDurability}/{drone.EffectiveMaxDurability}\n" +
                                      $"Độ bền gốc: {drone.BaseMaxDurability}\nTrạng thái: {condition}\n{reason}";
            if (repairDroneButton != null)
                repairDroneButton.interactable = canRepair && profile != null && profile.Currency >= repair.Cost && !profile.IsReadOnly;
        }

        private void RepairEquippedDrone()
        {
            if (controller == null) SetStatus("Không tìm thấy dịch vụ sửa chữa.");
            else if (controller.TryRepairEquippedDrone(out string result)) SetStatus(result);
            else SetStatus(result);
            RefreshAll();
            RefreshRepairPanel();
        }

        private void Buy(ShopOffer offer)
        {
            if (controller == null || !controller.TryBuy(offer)) SetStatus(!string.IsNullOrEmpty(profile != null ? profile.StatusMessage : null) ? profile.StatusMessage : "Không đủ Xu hoặc kho đã đầy.");
            else SetStatus($"Đã mua {offer.Item.DisplayName} ×{offer.PurchaseQuantity}.");
            RefreshAll();
        }

        private void ConfirmSell()
        {
            if (selectedStorage != PlayerStorage.Stash || selectedIndex < 0 || GetDefinition(selectedStorage, selectedIndex) == null)
            {
                SetStatus("Hãy chọn vật phẩm trong kho vĩnh viễn để bán.");
                return;
            }
            confirmPanel.SetActive(true);
        }

        private void ConfirmSellNow()
        {
            confirmPanel.SetActive(false);
            if (controller == null || !controller.TrySell(selectedStorage, selectedIndex)) SetStatus("Không thể bán vật phẩm đã chọn.");
            else
            {
                selectedIndex = -1;
                selectedSlot = null;
                SetStatus("Đã bán vật phẩm và lưu tiến trình.");
            }
            RefreshAll();
        }

        private void StartRaid()
        {
            if (controller == null || !controller.BeginRaid())
                SetStatus(profile != null && profile.IsReadOnly ? profile.StatusMessage : "Không thể bắt đầu raid. Kiểm tra scene và save.");
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text ?? string.Empty;
        }

        private static RectTransform CreatePanel(Transform parent, string name,
            float x, float y, float width, float height, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            SetTopLeft(rect, x, y, width, height);
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private Image CreateImage(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            SetTopLeft(obj.GetComponent<RectTransform>(), x, y, width, height);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
            TextAnchor alignment, float x, float y, float width, float height, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            SetTopLeft(obj.GetComponent<RectTransform>(), x, y, width, height);
            Text text = obj.GetComponent<Text>();
            text.font = uiFont;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(Transform parent, string name, string value, int size,
            float x, float y, float width, float height, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            SetTopLeft(obj.GetComponent<RectTransform>(), x, y, width, height);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            Button button = obj.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            button.colors = colors;
            CreateText(obj.transform, "Nhãn nút", value, size, FontStyle.Bold,
                TextAnchor.MiddleCenter, 3f, 2f, width - 6f, height - 4f, Color.white);
            return button;
        }

        private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
