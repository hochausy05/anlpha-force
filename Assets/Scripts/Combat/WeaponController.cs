using System.Collections.Generic;
using Looter.Foundation;
using Looter.Inventory;
using Looter.Items;
using Looter.Persistence;
using Looter.Raid;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Looter.Combat
{
    [RequireComponent(typeof(CombatHealth), typeof(PlayerController2D))]
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask meleeObstacles = ~0;
        [SerializeField, Min(0)] private int unarmedDamage = 5;
        [SerializeField, Min(0.1f)] private float reloadDuration = 1.5f;
        [SerializeField] private Sprite fallbackProjectileSprite;

        private CombatHealth health;
        private PlayerController2D movement;
        private EquipmentLoadout loadout;
        private InventoryModel inventory;
        private ItemStack activeWeapon;
        private WeaponDefinition definition;
        private float nextAttackTime;
        private float reloadEndTime;
        private bool reloading;
        private int reservedReloadAmount;
        private SpriteRenderer weaponVisual;
        private RaidManager raidManager;
        private InventoryPresenter inventoryPresenter;
        private string notice;
        private float noticeUntil;

        public ItemStack ActiveWeapon => activeWeapon;
        public WeaponDefinition Definition => definition;
        public bool IsReloading => reloading;
        public int MagazineCount => activeWeapon != null ? activeWeapon.CurrentMagazine : 0;
        public int ReserveCount => definition != null ? AmmoService.Count(inventory, definition.Ammunition) : 0;
        public string Notice => Time.time <= noticeUntil ? notice : string.Empty;
        public event System.Action Changed;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            movement = GetComponent<PlayerController2D>();
            loadout = GetComponent<EquipmentLoadout>();
            inventory = GetComponent<InventoryModel>();
            inventoryPresenter = GetComponent<InventoryPresenter>();
            raidManager = FindAnyObjectByType<RaidManager>();
            GameObject visual = new GameObject("Weapon Visual");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0.22f, 0.12f, 0f);
            weaponVisual = visual.AddComponent<SpriteRenderer>();
            weaponVisual.sortingOrder = 9;
            weaponVisual.enabled = false;
        }

        private void Update()
        {
            RefreshWeapon();
            if (health == null || !health.IsAlive || !RaidIsActive()) return;
            if (reloading && Time.time >= reloadEndTime) FinishReload();
            if (inventoryPresenter != null && inventoryPresenter.IsOpen) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && definition != null &&
                definition.AttackMode == WeaponAttackMode.Pistol) TryReload();
            Mouse mouse = Mouse.current;
            if (mouse == null || IsPointerOverUi() || inventoryPresenter != null && inventoryPresenter.IsOpen) return;
            if (mouse.leftButton.wasPressedThisFrame) TryAttack(mouse.position.ReadValue());
        }

        public bool TryAttack(Vector2 screenPosition)
        {
            if (health == null || !health.IsAlive || !RaidIsActive() || reloading || Time.time < nextAttackTime) return false;
            if (definition == null)
            {
                if (activeWeapon != null)
                {
                    ShowNotice("Vũ khí chưa có cấu hình chiến đấu.");
                    return false;
                }
                nextAttackTime = Time.time + 0.5f;
                return TryUnarmedAttack();
            }
            Vector2 origin = GetMuzzlePosition();
            if (!TryGetAim(screenPosition, origin, out Vector2 direction)) return false;
            nextAttackTime = Time.time + definition.AttackCooldown;

            if (definition.AttackMode == WeaponAttackMode.Melee) return TryMelee(origin, direction);
            if (definition.AttackMode == WeaponAttackMode.Bow)
            {
                if (!AmmoService.TryConsume(inventory, definition.Ammunition, 1))
                { ShowNotice("Không còn mũi tên."); Changed?.Invoke(); return false; }
                SpawnProjectile(origin, direction, definition.ProjectileSpeed, definition.ProjectileGravityScale);
                Changed?.Invoke();
                return true;
            }
            if (definition.AttackMode == WeaponAttackMode.Pistol)
            {
                if (activeWeapon == null || activeWeapon.CurrentMagazine <= 0)
                { ShowNotice("Súng hết đạn trong băng. Nhấn R để nạp."); Changed?.Invoke(); return false; }
                int previousMagazine = activeWeapon.CurrentMagazine;
                activeWeapon.SetCurrentMagazine(activeWeapon.CurrentMagazine - 1);
                if (!PersistMagazine())
                {
                    activeWeapon.SetCurrentMagazine(previousMagazine);
                    ShowNotice("Không thể lưu băng đạn; phát bắn đã bị hủy.");
                    return false;
                }
                SpawnProjectile(origin, direction, definition.ProjectileSpeed, definition.ProjectileGravityScale);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public bool TryReload()
        {
            if (reloading || definition == null || definition.AttackMode != WeaponAttackMode.Pistol ||
                activeWeapon == null || definition.Ammunition == null) return false;
            int needed = definition.MagazineCapacity - activeWeapon.CurrentMagazine;
            if (needed <= 0) { ShowNotice("Băng đạn đã đầy."); return false; }
            int amount = Mathf.Min(needed, AmmoService.Count(inventory, definition.Ammunition));
            if (amount <= 0) { ShowNotice("Không có đạn súng lục trong balo."); return false; }
            reservedReloadAmount = amount;
            reloading = true;
            reloadEndTime = Time.time + reloadDuration;
            ShowNotice("Đang nạp đạn...");
            Changed?.Invoke();
            return true;
        }

        public void CancelReload()
        {
            reservedReloadAmount = 0;
            reloading = false;
            Changed?.Invoke();
        }

        private void OnDisable()
        {
            if (reloading) CancelReload();
        }

        private void FinishReload()
        {
            if (activeWeapon == null || definition == null || definition.AttackMode != WeaponAttackMode.Pistol)
            { CancelReload(); return; }
            int room = definition.MagazineCapacity - activeWeapon.CurrentMagazine;
            int amount = Mathf.Min(room, reservedReloadAmount);
            if (!AmmoService.TryConsume(inventory, definition.Ammunition, amount))
            { reservedReloadAmount = 0; reloading = false; Changed?.Invoke(); return; }
            int previousMagazine = activeWeapon.CurrentMagazine;
            activeWeapon.SetCurrentMagazine(previousMagazine + amount);
            reservedReloadAmount = 0;
            reloading = false;
            if (!PersistMagazine())
            {
                activeWeapon.SetCurrentMagazine(previousMagazine);
                inventory.TryAdd(definition.Ammunition, amount);
                ShowNotice("Không thể lưu băng đạn; thao tác nạp đã được hoàn tác.");
                Changed?.Invoke();
                return;
            }
            ShowNotice("Đã nạp đạn.");
            Changed?.Invoke();
        }

        private bool TryMelee(Vector2 origin, Vector2 direction)
        {
            Vector2 center = origin + direction * definition.AttackRange * 0.55f;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center,
                new Vector2(definition.AttackRange, 1.15f), Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            HashSet<CombatHealth> damaged = new HashSet<CombatHealth>();
            bool hitAny = false;
            foreach (Collider2D collider in hits)
            {
                CombatHealth target = collider != null ? collider.GetComponentInParent<CombatHealth>() : null;
                if (target == null || target == health || target.Faction != CombatFaction.Enemy || !damaged.Add(target)) continue;
                Vector2 delta = (Vector2)target.transform.position - origin;
                bool blocked = false;
                RaycastHit2D[] blockers = Physics2D.RaycastAll(origin, delta.normalized, delta.magnitude, meleeObstacles);
                foreach (RaycastHit2D blocker in blockers)
                {
                    if (blocker.collider == null || blocker.transform == transform || blocker.transform.IsChildOf(transform) ||
                        blocker.collider.GetComponentInParent<WorldItemPickup>() != null) continue;
                    if (blocker.collider.GetComponentInParent<CombatHealth>() == target) break;
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                if (target.TryTakeDamage(new DamageContext(definition.Damage, activeWeapon.Item.Tier, gameObject, target, DamageType.Melee)))
                    hitAny = true;
            }
            PlayerMeleeAttack slash = GetComponent<PlayerMeleeAttack>();
            if (slash != null) slash.ShowWeaponSlash(Mathf.Abs(direction.x) < 0.05f ? movement.FacingDirection : Mathf.Sign(direction.x), hitAny,
                definition.AttackRange);
            return hitAny;
        }

        private bool TryUnarmedAttack()
        {
            float facing = movement != null ? movement.FacingDirection : 1f;
            Vector2 origin = transform.position;
            Vector2 center = origin + Vector2.right * facing * 0.8f;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, new Vector2(1f, 1f), 0f);
            foreach (Collider2D collider in hits)
            {
                CombatHealth target = collider != null ? collider.GetComponentInParent<CombatHealth>() : null;
                if (target == null || target.Faction != CombatFaction.Enemy) continue;
                Vector2 delta = (Vector2)target.transform.position - origin;
                bool blocked = false;
                foreach (RaycastHit2D rayHit in Physics2D.RaycastAll(origin, delta.normalized, delta.magnitude, meleeObstacles))
                {
                    if (rayHit.collider == null || rayHit.transform == transform || rayHit.transform.IsChildOf(transform) ||
                        rayHit.collider.GetComponentInParent<WorldItemPickup>() != null) continue;
                    if (rayHit.collider.GetComponentInParent<CombatHealth>() == target) break;
                    blocked = true;
                    break;
                }
                if (!blocked) return target.TryTakeDamage(new DamageContext(unarmedDamage, ItemTier.I, gameObject, target, DamageType.Melee));
            }
            return false;
        }

        private void RefreshWeapon()
        {
            ItemStack equipped = loadout != null ? loadout.Weapon : null;
            if (ReferenceEquals(activeWeapon, equipped)) return;
            CancelReload();
            activeWeapon = equipped;
            definition = equipped != null && equipped.Item != null ? equipped.Item.WeaponDefinition : null;
            if (weaponVisual != null)
            {
                weaponVisual.sprite = definition != null ? definition.Icon : null;
                weaponVisual.enabled = definition != null && definition.Icon != null;
            }
            Changed?.Invoke();
        }

        private bool TryGetAim(Vector2 screenPosition, Vector2 origin, out Vector2 direction)
        {
            direction = Vector2.right * (movement != null ? movement.FacingDirection : 1f);
            Camera camera = Camera.main;
            if (camera == null || screenPosition.x < 0f || screenPosition.y < 0f ||
                screenPosition.x > Screen.width || screenPosition.y > Screen.height) return false;
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -camera.transform.position.z));
            Vector2 delta = (Vector2)world - origin;
            if (delta.sqrMagnitude < 0.0025f) return false;
            direction = delta.normalized;
            if (movement != null && Mathf.Abs(direction.x) > 0.05f) movement.SetFacingDirection(Mathf.Sign(direction.x));
            if (weaponVisual != null)
            {
                weaponVisual.transform.right = direction;
                weaponVisual.transform.localPosition = new Vector3(direction.x >= 0f ? 0.22f : -0.22f, 0.12f, 0f);
                weaponVisual.flipY = direction.x < 0f;
            }
            return true;
        }

        private Vector2 GetMuzzlePosition() => muzzle != null ? muzzle.position : (Vector2)transform.position + Vector2.up * 0.1f;

        private void SpawnProjectile(Vector2 origin, Vector2 direction, float speed, float gravity)
        {
            GameObject instance;
            if (definition.ProjectilePrefab != null) instance = Instantiate(definition.ProjectilePrefab, origin, Quaternion.identity);
            else
            {
                instance = new GameObject(definition.AttackMode == WeaponAttackMode.Bow ? "Arrow Projectile" : "Pistol Projectile");
                SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
                renderer.sprite = definition.Icon != null ? definition.Icon : fallbackProjectileSprite;
                renderer.color = definition.AttackMode == WeaponAttackMode.Bow ? new Color(0.75f, 0.55f, 0.25f) : new Color(1f, 0.85f, 0.3f);
                renderer.sortingOrder = 8;
                instance.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
                CircleCollider2D collider = instance.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                instance.AddComponent<Projectile2D>();
                instance.transform.localScale = definition.AttackMode == WeaponAttackMode.Bow
                    ? new Vector3(0.45f, 0.08f, 1f) : new Vector3(0.16f, 0.16f, 1f);
            }
            Projectile2D projectile = instance.GetComponent<Projectile2D>();
            if (projectile != null) projectile.Initialize(gameObject, activeWeapon.Item.Tier, definition.Damage,
                direction, speed, gravity, definition.AttackRange);
        }

        private bool PersistMagazine()
        {
            PlayerProfileStore profile = PlayerProfileStore.Instance;
            return profile == null || !profile.RaidInProgress || activeWeapon == null ||
                profile.UpdateEquippedWeaponMagazine(activeWeapon);
        }

        private bool RaidIsActive()
        {
            return raidManager == null || raidManager.CanInteract;
        }

        private static bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void ShowNotice(string message)
        {
            notice = message;
            noticeUntil = Time.time + 1.7f;
        }
    }
}
