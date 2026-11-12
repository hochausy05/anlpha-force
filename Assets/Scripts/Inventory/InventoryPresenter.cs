using System.Collections.Generic;
using Looter.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Looter.Inventory
{
    public sealed class InventoryPresenter : MonoBehaviour
    {
        [SerializeField] private InventoryModel inventory;
        private bool isOpen;
        private bool gameplayInputEnabled = true;
        private LootContainer activeContainer;
        private InventoryStorage selectedStorage;
        private int selectedIndex = -1;
        private string message;
        private float messageUntil;
        private GUIStyle titleStyle;
        private GUIStyle itemStyle;
        private GUIStyle detailStyle;

        public bool IsOpen => isOpen;
        public InventoryModel Inventory => inventory;

        public void SetGameplayInputEnabled(bool enabled)
        {
            gameplayInputEnabled = enabled;
            if (!enabled) CloseWindow();
        }

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<InventoryModel>();
        }

        private void Update()
        {
            if (!gameplayInputEnabled) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.iKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
            {
                isOpen = !isOpen;
                activeContainer = null;
                ClearSelection();
            }
            if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                isOpen = false;
                activeContainer = null;
                ClearSelection();
            }
        }

        private void OnDisable()
        {
            isOpen = false;
            activeContainer = null;
            ClearSelection();
        }

        public void OpenContainer(LootContainer container)
        {
            if (!IsContainerAvailable(container)) return;
            activeContainer = container;
            isOpen = true;
            ClearSelection();
        }

        public void ShowMessage(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 1.8f;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (isOpen && inventory != null) DrawInventoryWindow();
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 170f, 22f, 340f, 34f), message, detailStyle);
            }
        }

        private void DrawInventoryWindow()
        {
            if (!ReferenceEquals(activeContainer, null) && !IsContainerAvailable(activeContainer))
                CloseWindow();

            LootContainer container = IsContainerAvailable(activeContainer) ? activeContainer : null;
            bool hasContainer = container != null;
            float panelWidth = hasContainer ? 865f : 590f;
            const float panelHeight = 430f;
            float x = (Screen.width - panelWidth) * 0.5f;
            float y = (Screen.height - panelHeight) * 0.5f;
            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none);
            GUI.Label(new Rect(x + 16f, y + 12f, panelWidth - 60f, 32f),
                hasContainer ? $"Túi đồ — {activeContainer.DisplayName}" : "Túi đồ", titleStyle);
            if (GUI.Button(new Rect(x + panelWidth - 42f, y + 12f, 28f, 28f), "X"))
            {
                CloseWindow();
                return;
            }

            GUI.Label(new Rect(x + 16f, y + 48f, 500f, 20f),
                $"Dung lượng túi đồ — {inventory.BackpackCapacity} ô", detailStyle);
            DrawStorageGrid(InventoryStorage.Backpack, x + 16f, y + 70f, 5, 96f, 56f);

            GUI.Label(new Rect(x + 16f, y + 202f, 300f, 20f), "Ô an toàn — 2 ô", detailStyle);
            DrawStorageGrid(InventoryStorage.Safe, x + 16f, y + 224f, 2, 96f, 56f);

            DrawSelectedItem(x + 16f, y + 294f, hasContainer ? 520f : 550f);
            if (selectedIndex >= 0 && GUI.Button(new Rect(x + 16f, y + 370f, 190f, 34f), "Bỏ vật phẩm xuống đất"))
                DropSelectedItem();
            GUI.Label(new Rect(x + 220f, y + 365f, hasContainer ? 325f : 350f, 45f),
                "Chọn ô nguồn rồi chọn ô đích để chuyển hoặc gộp. Nhấn I/Tab hoặc Esc để đóng.", detailStyle);

            if (hasContainer) DrawContainerContents(container, x + 572f, y + 54f);
        }

        private void DrawStorageGrid(InventoryStorage storage, float x, float y, int columns, float slotWidth, float slotHeight)
        {
            int slotCount = storage == InventoryStorage.Backpack ? inventory.BackpackCapacity : inventory.SafeSlotCapacity;
            const float gap = 7f;
            for (int i = 0; i < slotCount; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect rect = new Rect(x + column * (slotWidth + gap), y + row * (slotHeight + gap), slotWidth, slotHeight);
                DrawInventorySlot(storage, i, rect);
            }
        }

        private void DrawInventorySlot(InventoryStorage storage, int index, Rect rect)
        {
            ItemStack stack = inventory.GetStack(storage, index);
            bool selected = selectedIndex == index && selectedStorage == storage;
            Color tierColor = stack != null && stack.Item != null ? stack.Item.TierColor : new Color(0.38f, 0.4f, 0.43f);
            if (selected) tierColor = Color.white;
            GUI.color = new Color(tierColor.r, tierColor.g, tierColor.b, 0.38f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = Color.white;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) HandleInventorySlotClick(storage, index);

            if (stack == null || stack.Item == null)
            {
                GUI.Label(new Rect(rect.x + 4f, rect.y + 3f, rect.width - 8f, 18f), $"Ô {index + 1}", detailStyle);
                return;
            }

            if (stack.Item.Icon != null)
                GUI.DrawTexture(new Rect(rect.x + 5f, rect.y + 19f, 31f, 31f), stack.Item.Icon.texture, ScaleMode.ScaleToFit, true);
            itemStyle.normal.textColor = stack.Item.TierColor;
            GUI.Label(new Rect(rect.x + 39f, rect.y + 10f, rect.width - 42f, 32f),
                $"{stack.Item.DisplayName}\nx{stack.Quantity}", itemStyle);
            GUI.Label(new Rect(rect.x + 2f, rect.y + rect.height - 18f, rect.width - 4f, 16f),
                $"Bậc {stack.Item.TierLabel}", detailStyle);
        }

        private void DrawSelectedItem(float x, float y, float width)
        {
            ItemStack selected = selectedIndex >= 0 ? inventory.GetStack(selectedStorage, selectedIndex) : null;
            if (selected == null || selected.Item == null)
            {
                GUI.Label(new Rect(x, y, width, 52f), "Chọn vật phẩm để xem thông tin.", detailStyle);
                return;
            }
            GUI.Label(new Rect(x, y, width, 24f),
                $"{selected.Item.DisplayName} — Bậc {selected.Item.TierLabel} — Giá trị {selected.Item.SellValue}", itemStyle);
            GUI.Label(new Rect(x, y + 23f, width, 38f), selected.Item.Description, detailStyle);
        }

        private void DrawContainerContents(LootContainer container, float x, float y)
        {
            if (!IsContainerAvailable(container))
            {
                CloseWindow();
                return;
            }

            GUI.Label(new Rect(x, y, 260f, 22f), "Vật phẩm trong rương", itemStyle);
            IReadOnlyList<ItemStack> contents = container.Contents;
            if (contents.Count == 0)
            {
                GUI.Label(new Rect(x, y + 32f, 250f, 45f), "Rương trống.", detailStyle);
                return;
            }

            for (int i = 0; i < contents.Count; i++)
            {
                ItemStack stack = contents[i];
                Rect rect = new Rect(x, y + 32f + i * 64f, 260f, 56f);
                GUI.Box(rect, GUIContent.none);
                if (stack == null || stack.Item == null)
                {
                    GUI.Label(rect, "Đã lấy", detailStyle);
                    continue;
                }
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    if (!container.TryTake(i, inventory)) ShowMessage("Balo đã đầy hoặc không đủ chỗ.");
                    else ShowMessage($"Đã lấy {stack.Item.DisplayName} x{stack.Quantity}.");
                }
                if (stack.Item.Icon != null)
                    GUI.DrawTexture(new Rect(rect.x + 5f, rect.y + 10f, 34f, 34f), stack.Item.Icon.texture, ScaleMode.ScaleToFit, true);
                itemStyle.normal.textColor = stack.Item.TierColor;
                GUI.Label(new Rect(rect.x + 43f, rect.y + 5f, rect.width - 46f, 46f),
                    $"{stack.Item.DisplayName} x{stack.Quantity}\nBậc {stack.Item.TierLabel} · Giá {stack.Item.SellValue}", itemStyle);
            }
        }

        private void HandleInventorySlotClick(InventoryStorage storage, int index)
        {
            ItemStack clicked = inventory.GetStack(storage, index);
            if (selectedIndex < 0)
            {
                if (clicked != null) { selectedStorage = storage; selectedIndex = index; }
                return;
            }

            if (selectedStorage == storage && selectedIndex == index)
            {
                ClearSelection();
                return;
            }

            if (inventory.TryTransfer(selectedStorage, selectedIndex, storage, index))
            {
                ClearSelection();
                ShowMessage("Đã chuyển vật phẩm.");
            }
            else if (storage == InventoryStorage.Safe && clicked == null)
            {
                ShowMessage("Không thể cất vũ khí, balo, drone hoặc vật phẩm không hợp lệ vào ô an toàn.");
            }
            else if (clicked != null)
            {
                selectedStorage = storage;
                selectedIndex = index;
            }
        }

        private void DropSelectedItem()
        {
            ItemStack stack = inventory.GetStack(selectedStorage, selectedIndex);
            if (stack == null || stack.Item == null) { ClearSelection(); return; }
            Vector3 dropPosition = transform.position + Vector3.right * 0.9f + Vector3.up * 0.2f;
            WorldItemPickup pickup = WorldItemPickup.Spawn(stack.Item, stack.Quantity, dropPosition);
            if (pickup == null) { ShowMessage("Không thể thả vật phẩm này."); return; }
            if (!inventory.TryRemove(selectedStorage, selectedIndex, out _))
            {
                Destroy(pickup.gameObject);
                ShowMessage("Không thể bỏ vật phẩm khỏi ô đồ.");
                return;
            }
            ClearSelection();
            ShowMessage("Đã thả vật phẩm xuống đất.");
        }

        private void ClearSelection()
        {
            selectedIndex = -1;
        }

        private void CloseWindow()
        {
            isOpen = false;
            activeContainer = null;
            ClearSelection();
        }

        private static bool IsContainerAvailable(LootContainer container)
        {
            return container != null && container.isActiveAndEnabled && container.gameObject.activeInHierarchy;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            itemStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        }
    }
}
