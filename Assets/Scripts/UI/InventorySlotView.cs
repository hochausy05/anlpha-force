using Looter.Hub;
using Looter.Items;
using Looter.Persistence;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Looter.UI
{
    public sealed class InventorySlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private string emptyLabel;
        private BaseHubCanvas view;
        private PlayerStorage storage;
        private int index;
        private string draggedItemId;
        private bool dragTickLogged;
        private bool dropHighlighted;
        private bool dropIsValid;

        public PlayerStorage Storage => storage;
        public int Index => index;

        public void Configure(BaseHubCanvas owner, PlayerStorage slotStorage, int slotIndex,
            Image slotBackground, Image itemIcon, Text slotLabel, string placeholder = null)
        {
            view = owner;
            storage = slotStorage;
            index = slotIndex;
            background = slotBackground;
            icon = itemIcon;
            label = slotLabel;
            emptyLabel = placeholder;
            Refresh();
        }

        public void Refresh()
        {
            if (view == null || background == null) return;
            ItemDefinition definition = view.GetDefinition(storage, index);
            bool occupied = definition != null;
            icon.enabled = occupied && definition.Icon != null;
            if (icon.enabled) icon.sprite = definition.Icon;
            label.text = occupied
                ? $"{ShortName(definition.DisplayName)}\nBậc {definition.TierLabel}{(view.GetQuantity(storage, index) > 1 ? $" ×{view.GetQuantity(storage, index)}" : string.Empty)}"
                : string.IsNullOrWhiteSpace(emptyLabel) ? $"Ô {index + 1}" : emptyLabel;
            Color baseColor = occupied ? definition.TierColor : new Color32(51, 59, 70, 255);
            background.color = dropHighlighted
                ? (dropIsValid ? new Color32(58, 204, 112, 255) : new Color32(220, 70, 70, 255))
                : view.IsSelected(storage, index)
                    ? Color.white
                    : new Color(baseColor.r, baseColor.g, baseColor.b, occupied ? 0.8f : 0.7f);
        }

        public void SetDropHighlight(bool highlighted, bool valid)
        {
            dropHighlighted = highlighted;
            dropIsValid = valid;
            Refresh();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            ItemDefinition item = view != null ? view.GetDefinition(storage, index) : null;
            draggedItemId = item != null ? item.Id : null;
            dragTickLogged = false;
            view?.LogDragEvent($"OnBeginDrag item={draggedItemId ?? "<trống>"}; source={storage}[{index}]; " +
                                $"gameObject={gameObject.name}; pointerDrag={eventData.pointerDrag?.name ?? "null"}; " +
                                $"sourceRaycast={(background != null && background.raycastTarget)}.");
            view?.BeginDrag(storage, index, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragTickLogged)
            {
                view?.LogDragEvent($"OnDrag hoạt động; item={draggedItemId ?? "?"}; pointerDrag={eventData.pointerDrag?.name ?? "null"}.");
                dragTickLogged = true;
            }
            view?.MoveDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (view != null && view.LogDragDiagnostics)
            {
                List<RaycastResult> hits = new List<RaycastResult>();
                EventSystem.current?.RaycastAll(eventData, hits);
                List<string> hitSummary = new List<string>();
                foreach (RaycastResult hit in hits)
                {
                    InventorySlotView targetSlot = hit.gameObject.GetComponentInParent<InventorySlotView>();
                    hitSummary.Add(targetSlot != null ? $"{hit.gameObject.name}->{targetSlot.Storage}[{targetSlot.Index}]" : hit.gameObject.name);
                    if (hitSummary.Count >= 8) break;
                }
                view.LogDragEvent($"OnEndDrag item={draggedItemId ?? "?"}; pointerEnter={eventData.pointerEnter?.name ?? "null"}; " +
                                  $"raycasts={string.Join(" | ", hitSummary)}.");
            }
            view?.EndDrag();
            draggedItemId = null;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (view == null) return;
            view.LogDragEvent($"OnDrop receiver={gameObject.name} destination={storage}[{index}]; " +
                              $"pointerDrag={eventData.pointerDrag?.name ?? "null"}; pointerEnter={eventData.pointerEnter?.name ?? "null"}.");
            if (eventData.pointerDrag == null) return;
            InventorySlotView source = eventData.pointerDrag.GetComponent<InventorySlotView>();
            if (source == null || source.view != view)
            {
                view.LogDragEvent("OnDrop có pointerDrag nhưng không tìm thấy InventorySlotView nguồn cùng Canvas.");
                return;
            }
            string itemId = source.view.GetDefinition(source.storage, source.index)?.Id ?? source.draggedItemId ?? "?";
            bool moved = view.DropOn(source.storage, source.index, storage, index);
            view.LogDragEvent($"OnDrop giao dịch={(moved ? "thành công" : "bị từ chối")}; item={itemId}; " +
                              $"source={source.storage}[{source.index}]; destination={storage}[{index}].");
        }
        public void OnPointerClick(PointerEventData eventData) => view?.Select(storage, index);
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (view != null && view.IsDragging) view.HighlightDropTarget(this);
            else view?.ShowItemDetails(storage, index);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (view != null && view.IsDragging) SetDropHighlight(false, false);
            else view?.RestoreSelectedDetails();
        }

        private static string ShortName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Vật phẩm";
            return value.Length <= 12 ? value : value.Substring(0, 11) + "…";
        }
    }
}
