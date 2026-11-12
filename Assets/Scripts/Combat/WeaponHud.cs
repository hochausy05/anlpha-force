using UnityEngine;

namespace Looter.Combat
{
    public sealed class WeaponHud : MonoBehaviour
    {
        [SerializeField] private WeaponController controller;
        private GUIStyle style;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<WeaponController>();
        }

        private void OnGUI()
        {
            if (controller == null || controller.Definition == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, fontSize = 13 };
            var item = controller.ActiveWeapon != null ? controller.ActiveWeapon.Item : null;
            if (item == null) return;
            string label = controller.Definition.AttackMode switch
            {
                WeaponAttackMode.Pistol => $"{item.DisplayName} {item.TierLabel} — {controller.MagazineCount}/{controller.Definition.MagazineCapacity}  ·  Dự trữ: {controller.ReserveCount}",
                WeaponAttackMode.Bow => $"{item.DisplayName} {item.TierLabel} — Mũi tên: {controller.ReserveCount}",
                _ => $"{item.DisplayName} {item.TierLabel}"
            };
            if (controller.IsReloading) label += "  ·  ĐANG NẠP";
            GUI.Box(new Rect(16f, Screen.height - 64f, 390f, 32f), label, style);
            if (!string.IsNullOrEmpty(controller.Notice))
            {
                style.alignment = TextAnchor.MiddleCenter;
                GUI.Box(new Rect(Screen.width * 0.5f - 150f, Screen.height - 112f, 300f, 30f), controller.Notice, style);
                style.alignment = TextAnchor.MiddleLeft;
            }
        }
    }
}
