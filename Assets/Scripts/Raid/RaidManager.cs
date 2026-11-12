using System;
using System.Collections.Generic;
using Looter.Combat;
using Looter.Foundation;
using Looter.Inventory;
using Looter.Items;
using Looter.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Looter.Raid
{
    public enum RaidState { Preparing, Starting, InProgress, Extracted, Failed }

    public sealed class RaidManager : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float raidDurationSeconds = 1200f;
        [SerializeField, Min(0.05f)] private float spawnVerticalOffset = 0.08f;

        [SerializeField] private GameObject player;
        [SerializeField] private DroneSystemSettings droneSettings;
        private CombatHealth playerHealth;
        private Rigidbody2D playerBody;
        private CapsuleCollider2D playerCapsule;
        private PlayerDeathHandler playerDeathHandler;
        private PlayerController2D playerMovement;
        private PlayerMeleeAttack playerAttack;
        private WeaponController weaponController;
        private InventoryModel inventory;
        private InventoryPresenter inventoryPresenter;
        private WorldInteractionController worldInteraction;
        private EquipmentLoadout equipmentLoadout;
        private DroneProtection droneProtection;
        [SerializeField] private CameraFollow2D cameraFollow;
        private RaidSpawnPoint[] spawnPoints = Array.Empty<RaidSpawnPoint>();
        private ExtractionZone[] extractionZones = Array.Empty<ExtractionZone>();
        private EnemyAI[] enemies = Array.Empty<EnemyAI>();
        private BossController[] bosses = Array.Empty<BossController>();
        private RaidState state = RaidState.Preparing;
        private RaidResult lastResult;
        private float raidStartTime;
        private float remainingSeconds;
        private string statusMessage;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle timerStyle;
        private Vector2 resultScroll;

        public RaidState State => state;
        public DroneSystemSettings DroneSettings => droneSettings;
        public RaidResult LastResult => lastResult;
        public bool CanInteract => state == RaidState.InProgress && remainingSeconds > 0f;
        public float RemainingSeconds => remainingSeconds;
        public float RaidDurationSeconds { get => raidDurationSeconds; set => raidDurationSeconds = Mathf.Max(1f, value); }

        public bool TryRepairDroneWithKit(InventoryModel targetInventory, DroneProtection protection, out string reason)
        {
            reason = null;
            if (!CanInteract) { reason = "Không thể sửa drone sau khi raid kết thúc."; return false; }
            if (targetInventory == null || protection == null || protection.EquippedDrone == null)
            { reason = "Chưa trang bị drone hợp lệ."; return false; }
            ItemStack drone = protection.EquippedDrone;
            List<ItemStack> previousBackpack = targetInventory.Snapshot(InventoryStorage.Backpack);
            int oldCurrent = drone.CurrentDurability;
            int oldMaximum = drone.EffectiveMaxDurability;
            if (!targetInventory.TryRepairDroneWithKit(drone, droneSettings, out reason, false)) return false;

            PlayerProfileStore profile = PlayerProfileStore.Instance;
            if (profile != null && profile.RaidInProgress && !profile.UpdateEquippedDroneDurability(drone))
            {
                drone.SetDroneDurability(oldCurrent, oldMaximum);
                targetInventory.ReplaceContents(previousBackpack, targetInventory.Snapshot(InventoryStorage.Safe),
                    targetInventory.BackpackCapacity);
                reason = string.IsNullOrEmpty(profile.StatusMessage) ? "Không thể lưu giao dịch sửa drone." : profile.StatusMessage;
                return false;
            }

            reason = $"Đã phục hồi độ bền drone; độ bền tối đa giảm theo mức hư hỏng ({oldMaximum - drone.EffectiveMaxDurability}).";
            return true;
        }

        public void Configure(GameObject raidPlayer, CameraFollow2D followCamera)
        {
            player = raidPlayer;
            cameraFollow = followCamera;
            CachePlayerReferences();
        }

        private void Awake()
        {
            if (player == null)
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null) player = taggedPlayer;
            }
            if (cameraFollow == null && Camera.main != null)
                cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            CachePlayerReferences();
        }

        private void OnEnable()
        {
            if (playerHealth == null && player != null) playerHealth = player.GetComponent<CombatHealth>();
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
            if (droneProtection != null) droneProtection.DurabilityChanged += OnDroneDurabilityChanged;
        }

        private void Start()
        {
            PlayerProfileStore profile = PlayerProfileStore.Instance;
            if (profile != null && profile.RaidInProgress)
            {
                if (inventory == null || !profile.LoadRaidInventory(inventory, equipmentLoadout))
                {
                    state = RaidState.Preparing;
                    statusMessage = "Không thể khôi phục dữ liệu raid từ save. Tiến trình chưa bị ghi đè.";
                    return;
                }
            }
            BeginRaid(false);
        }

        private void Update()
        {
            if (state != RaidState.InProgress) return;
            remainingSeconds = Mathf.Max(0f, raidDurationSeconds - (Time.time - raidStartTime));
            if (remainingSeconds <= 0f) Finish(RaidOutcome.Failed, RaidFailureReason.TimeExpired);
            else if (playerHealth == null || !playerHealth.IsAlive) Finish(RaidOutcome.Failed, RaidFailureReason.PlayerDied);
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (droneProtection != null) droneProtection.DurabilityChanged -= OnDroneDurabilityChanged;
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        public bool TryExtract(ExtractionZone zone)
        {
            if (state != RaidState.InProgress || zone == null || player == null || playerHealth == null) return false;
            float elapsed = Time.time - raidStartTime;
            if (elapsed >= raidDurationSeconds)
            {
                remainingSeconds = 0f;
                Finish(RaidOutcome.Failed, RaidFailureReason.TimeExpired);
                return false;
            }
            if (!playerHealth.IsAlive || !zone.IsPlayerInside(player.transform)) return false;

            Finish(RaidOutcome.Extracted, RaidFailureReason.None);
            return true;
        }

        private void BeginRaid(bool resetWorld)
        {
            Time.timeScale = 1f;
            CachePlayerReferences();
            if (player == null || playerHealth == null || playerBody == null)
            {
                state = RaidState.Preparing;
                statusMessage = "Không tìm thấy người chơi hoặc Rigidbody2D.";
                return;
            }

            state = RaidState.Starting;
            if (resetWorld) ResetWorldForNextRaid();
            EnemySpawnDirector spawnDirector = FindAnyObjectByType<EnemySpawnDirector>();
            if (spawnDirector != null) spawnDirector.BeginRaid(resetWorld);
            RefreshSceneObjects();
            ResetEnemies(resetWorld);
            if (!TryChooseSpawn(out Vector2 spawnPosition))
            {
                state = RaidState.Preparing;
                statusMessage = "Không tìm thấy điểm xuất phát trống. Kiểm tra SpawnPoint trong Scene.";
                return;
            }

            if (playerDeathHandler != null) playerDeathHandler.PrepareForNewRaid();
            else
            {
                playerHealth.ResetForNewRaid();
                if (playerMovement != null) playerMovement.enabled = true;
                if (playerAttack != null) playerAttack.enabled = true;
                if (weaponController != null) weaponController.enabled = true;
                playerBody.simulated = true;
            }

            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = spawnPosition;
            player.transform.position = spawnPosition;
            Physics2D.SyncTransforms();
            if (cameraFollow != null) cameraFollow.SnapToTarget();
            if (droneProtection != null)
                droneProtection.Configure(equipmentLoadout, playerMovement,
                    equipmentLoadout != null ? equipmentLoadout.Drone : null, droneSettings);
            SetGameplayEnabled(true);

            lastResult = null;
            statusMessage = null;
            raidStartTime = Time.time;
            remainingSeconds = raidDurationSeconds;
            state = RaidState.InProgress;
        }

        private void Finish(RaidOutcome outcome, RaidFailureReason reason)
        {
            if (state != RaidState.InProgress) return;
            float elapsed = Mathf.Clamp(Time.time - raidStartTime, 0f, raidDurationSeconds);
            List<ItemStack> backpack = inventory != null
                ? inventory.Snapshot(InventoryStorage.Backpack)
                : new List<ItemStack>();
            List<ItemStack> safe = inventory != null
                ? inventory.Snapshot(InventoryStorage.Safe)
                : new List<ItemStack>();

            if (outcome == RaidOutcome.Extracted)
            {
                lastResult = new RaidResult(outcome, reason, elapsed, backpack, safe, null);
                state = RaidState.Extracted;
            }
            else
            {
                lastResult = new RaidResult(outcome, reason, elapsed, null, safe, backpack);
                if (inventory != null) inventory.ClearStorage(InventoryStorage.Backpack);
                state = RaidState.Failed;
            }

            SetGameplayEnabled(false);
            Time.timeScale = 0f;

            PlayerProfileStore profile = PlayerProfileStore.Instance;
            if (profile != null && profile.RaidInProgress)
            {
                if (profile.CompleteRaid(outcome, inventory))
                {
                    Time.timeScale = 1f;
                    if (Application.CanStreamedLevelBeLoaded("BaseHub")) SceneManager.LoadScene("BaseHub");
                    else statusMessage = "Raid đã lưu xong. Scene BaseHub chưa có trong Build Settings.";
                }
                else statusMessage = profile.StatusMessage;
            }
        }

        private void OnPlayerDied(CombatHealth _)
        {
            if (state == RaidState.InProgress) Finish(RaidOutcome.Failed, RaidFailureReason.PlayerDied);
        }

        private void SetGameplayEnabled(bool enabled)
        {
            if (!enabled) Projectile2D.CancelFactionProjectiles(CombatFaction.Enemy);
            if (playerMovement != null) playerMovement.enabled = enabled;
            if (playerAttack != null) playerAttack.enabled = enabled;
            if (weaponController != null) weaponController.enabled = enabled;
            if (worldInteraction != null) worldInteraction.enabled = enabled;
            if (inventoryPresenter != null) inventoryPresenter.SetGameplayInputEnabled(enabled);
            foreach (EnemyAI enemy in enemies)
                if (enemy != null && enabled && enemy.GetComponent<CombatHealth>().IsAlive) enemy.enabled = true;
                else if (enemy != null) enemy.enabled = false;
            foreach (BossController boss in bosses)
            {
                if (boss == null) continue;
                if (!enabled) boss.StopForRaidEnd();
                boss.enabled = enabled && !boss.IsDead;
            }
        }

        private void ResetEnemies(bool resetWorld)
        {
            foreach (EnemyAI enemy in enemies)
            {
                if (enemy == null) continue;
                if (resetWorld)
                {
                    EnemyInventory loadout = enemy.GetComponent<EnemyInventory>();
                    if (loadout != null) loadout.ResetForNewRaid();
                    EnemyLootDrop lootDrop = enemy.GetComponent<EnemyLootDrop>();
                    if (lootDrop != null) lootDrop.ResetForNewRaid();
                    enemy.ResetForNewRaid();
                }
                enemy.enabled = true;
            }
            foreach (BossController boss in bosses)
                if (boss != null && resetWorld) { boss.ResetForNewRaid(); boss.enabled = true; }
        }

        private void ResetWorldForNextRaid()
        {
            foreach (WorldItemPickup pickup in FindObjectsByType<WorldItemPickup>())
                if (pickup != null) Destroy(pickup.gameObject);

            foreach (LootContainer container in FindObjectsByType<LootContainer>())
            {
                if (container == null) continue;
                if (container.IsCorpse) Destroy(container.gameObject);
                else container.ResetForNewRaid();
            }

            foreach (WorldLootSpawnPoint point in FindObjectsByType<WorldLootSpawnPoint>())
                if (point != null) point.SpawnLoot();
            Physics2D.SyncTransforms();
        }

        private void RefreshSceneObjects()
        {
            spawnPoints = FindObjectsByType<RaidSpawnPoint>();
            extractionZones = FindObjectsByType<ExtractionZone>();
            enemies = FindObjectsByType<EnemyAI>();
            bosses = FindObjectsByType<BossController>();
        }

        private bool TryChooseSpawn(out Vector2 position)
        {
            position = default;
            if (spawnPoints == null || spawnPoints.Length == 0 || playerCapsule == null) return false;
            List<RaidSpawnPoint> candidates = new List<RaidSpawnPoint>(spawnPoints);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swap = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[swap]) = (candidates[swap], candidates[i]);
            }

            foreach (RaidSpawnPoint point in candidates)
            {
                if (point == null) continue;
                Vector2 candidate = point.transform.position + Vector3.up * spawnVerticalOffset;
                if (!IsSpawnClear(candidate)) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        private bool IsSpawnClear(Vector2 playerPosition)
        {
            Vector2 centerOffset = playerCapsule.transform.TransformPoint(playerCapsule.offset) - player.transform.position;
            Vector2 center = playerPosition + centerOffset;
            Vector2 size = playerCapsule.bounds.size * 0.92f;
            Collider2D[] overlaps = Physics2D.OverlapCapsuleAll(center, size,
                playerCapsule.direction, playerCapsule.transform.eulerAngles.z, ~0);
            foreach (Collider2D overlap in overlaps)
            {
                if (overlap == null || overlap.transform == player.transform || overlap.transform.IsChildOf(player.transform)) continue;
                if (overlap.isTrigger || overlap.GetComponentInParent<WorldItemPickup>() != null) continue;
                return false;
            }

            foreach (ExtractionZone zone in extractionZones)
                if (zone != null && Vector2.Distance(playerPosition, zone.transform.position) < zone.InteractionRadius + 0.8f)
                    return false;
            return HasGroundSupport(playerPosition + Vector2.up * 0.15f);
        }

        private bool HasGroundSupport(Vector2 origin)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 1.5f, ~0);
            float nearestDistance = float.PositiveInfinity;
            bool foundSupport = false;
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.normal.y < 0.5f) continue;
                if (hit.transform == player.transform || hit.transform.IsChildOf(player.transform)) continue;
                if (hit.collider.GetComponentInParent<EnemyAI>() != null ||
                    hit.collider.GetComponentInParent<WorldItemPickup>() != null ||
                    hit.collider.GetComponentInParent<LootContainer>() != null) continue;
                if (hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                foundSupport = true;
            }
            return foundSupport;
        }

        private void CachePlayerReferences()
        {
            if (player == null) return;
            playerHealth = player.GetComponent<CombatHealth>();
            playerBody = player.GetComponent<Rigidbody2D>();
            playerCapsule = player.GetComponent<CapsuleCollider2D>();
            playerDeathHandler = player.GetComponent<PlayerDeathHandler>();
            playerMovement = player.GetComponent<PlayerController2D>();
            equipmentLoadout = player.GetComponent<EquipmentLoadout>();
            if (equipmentLoadout == null) equipmentLoadout = player.AddComponent<EquipmentLoadout>();
            playerAttack = player.GetComponent<PlayerMeleeAttack>();
            weaponController = player.GetComponent<WeaponController>();
            if (weaponController == null && Application.isPlaying) weaponController = player.AddComponent<WeaponController>();
            if (playerAttack != null) playerAttack.enabled = false;
            if (weaponController != null && player.GetComponent<WeaponHud>() == null) player.AddComponent<WeaponHud>();
            inventory = player.GetComponent<InventoryModel>();
            droneProtection = player.GetComponent<DroneProtection>();
            if (droneProtection == null && Application.isPlaying) droneProtection = player.AddComponent<DroneProtection>();
            inventoryPresenter = player.GetComponent<InventoryPresenter>();
            worldInteraction = player.GetComponent<WorldInteractionController>();
        }

        private void OnDroneDurabilityChanged(ItemStack drone)
        {
            PlayerProfileStore profile = PlayerProfileStore.Instance;
            if (profile != null && profile.RaidInProgress) profile.UpdateEquippedDroneDurability(drone);
        }

        private void OnValidate() => raidDurationSeconds = Mathf.Max(1f, raidDurationSeconds);

        private void OnGUI()
        {
            EnsureStyles();
            if (state == RaidState.InProgress)
            {
                int seconds = Mathf.CeilToInt(remainingSeconds);
                timerStyle.normal.textColor = seconds <= 60 ? new Color(1f, 0.35f, 0.25f) : Color.white;
                GUI.Box(new Rect(Screen.width - 190f, 16f, 172f, 60f), GUIContent.none);
                GUI.Label(new Rect(Screen.width - 180f, 21f, 152f, 20f), "Thời gian trận", bodyStyle);
                GUI.Label(new Rect(Screen.width - 180f, 40f, 152f, 30f), FormatTime(seconds), timerStyle);
            }
            else if (state == RaidState.Preparing)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 230f, Screen.height * 0.5f - 85f, 460f, 170f), GUIContent.none);
                GUI.Label(new Rect(Screen.width * 0.5f - 210f, Screen.height * 0.5f - 65f, 420f, 50f),
                    string.IsNullOrEmpty(statusMessage) ? "Chuẩn bị trận đấu" : statusMessage, titleStyle);
                if (GUI.Button(new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.5f + 15f, 200f, 42f), "Bắt đầu trận"))
                    BeginRaid(false);
            }
            else if (state == RaidState.Extracted || state == RaidState.Failed)
            {
                DrawResultScreen();
            }
        }

        private void DrawResultScreen()
        {
            float width = Mathf.Min(620f, Screen.width - 24f);
            float height = Mathf.Min(560f, Screen.height - 24f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, GUIContent.none);
            string title = lastResult.Outcome == RaidOutcome.Extracted ? "RÚT QUÂN THÀNH CÔNG" : "RÚT QUÂN THẤT BẠI";
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, width - 36f, 36f), title, titleStyle);
            string summary = $"Thời gian: {FormatTime(Mathf.FloorToInt(lastResult.ElapsedSeconds))}";
            if (lastResult.Outcome == RaidOutcome.Failed)
                summary += lastResult.FailureReason == RaidFailureReason.PlayerDied ? "  ·  Nguyên nhân: tử vong" : "  ·  Nguyên nhân: hết giờ";
            GUI.Label(new Rect(panel.x + 18f, panel.y + 52f, width - 36f, 25f), summary, bodyStyle);

            Rect view = new Rect(panel.x + 18f, panel.y + 82f, width - 36f, height - 150f);
            float contentHeight = 330f;
            resultScroll = GUI.BeginScrollView(view, resultScroll, new Rect(0f, 0f, view.width - 18f, contentHeight));
            float y = 0f;
            if (lastResult.Outcome == RaidOutcome.Extracted)
            {
                y = DrawResultItems("Loot đã rút", lastResult.ExtractedItems, 0f, view.width - 40f);
                y = DrawResultItems("Ô an toàn được giữ", lastResult.SecuredItems, y + 8f, view.width - 40f);
                GUI.Label(new Rect(4f, y + 10f, view.width - 40f, 26f),
                    $"Tổng số vật phẩm: {lastResult.TotalItemCount}  ·  Tổng giá trị: {lastResult.TotalLootValue}", bodyStyle);
            }
            else
            {
                y = DrawResultItems("Vật phẩm bị mất", lastResult.LostItems, 0f, view.width - 40f);
                y = DrawResultItems("Ô an toàn được giữ", lastResult.SecuredItems, y + 8f, view.width - 40f);
                GUI.Label(new Rect(4f, y + 10f, view.width - 40f, 26f),
                    $"Tổng giá trị vật phẩm bị mất: {lastResult.TotalLostValue}", bodyStyle);
            }
            GUI.EndScrollView();
            PlayerProfileStore profile = PlayerProfileStore.Instance;
            if (profile != null && profile.RaidInProgress)
            {
                if (GUI.Button(new Rect(panel.x + width * 0.5f - 125f, panel.y + height - 52f, 250f, 36f), "Thử lưu và về căn cứ"))
                {
                    if (profile.CompleteRaid(lastResult.Outcome, inventory) && Application.CanStreamedLevelBeLoaded("BaseHub"))
                    {
                        Time.timeScale = 1f;
                        SceneManager.LoadScene("BaseHub");
                    }
                    else statusMessage = profile.StatusMessage;
                }
            }
            else if (GUI.Button(new Rect(panel.x + width * 0.5f - 110f, panel.y + height - 52f, 220f, 36f), "Bắt đầu trận mới"))
                BeginRaid(true);
        }

        private float DrawResultItems(string heading, IReadOnlyList<ItemStack> stacks, float y, float width)
        {
            GUI.Label(new Rect(4f, y, width, 24f), heading, bodyStyle);
            y += 26f;
            if (stacks == null || stacks.Count == 0)
            {
                GUI.Label(new Rect(8f, y, width - 8f, 22f), "Không có vật phẩm.", bodyStyle);
                return y + 24f;
            }
            foreach (ItemStack stack in stacks)
            {
                string name = stack != null && stack.Item != null ? stack.Item.DisplayName : "Vật phẩm không hợp lệ";
                int quantity = stack != null ? stack.Quantity : 0;
                int value = stack != null && stack.Item != null ? stack.Item.SellValue * quantity : 0;
                GUI.Label(new Rect(8f, y, width - 8f, 22f), $"{name} ×{quantity}  ·  Giá trị {value}", bodyStyle);
                y += 22f;
            }
            return y;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            timerStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 23, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private static string FormatTime(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }
}
