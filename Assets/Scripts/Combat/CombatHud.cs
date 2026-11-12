using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Looter.Raid;

namespace Looter.Combat
{
    public sealed class CombatHud : MonoBehaviour
    {
        [SerializeField] private CombatHealth playerHealth;
        [SerializeField] private RaidManager raidManager;
        [SerializeField] private DroneProtection droneProtection;
        private GUIStyle titleStyle;
        private GUIStyle detailStyle;
        private bool stylesReady;
        private string droneMessage;
        private float droneMessageUntil;

        private void Awake()
        {
            if (raidManager == null) raidManager = FindAnyObjectByType<RaidManager>();
        }

        public void Configure(CombatHealth health) => playerHealth = health;
        public void ConfigureRaid(RaidManager manager) => raidManager = manager;

        private void Update()
        {
            CacheDroneProtection();
            if (raidManager != null || playerHealth == null || playerHealth.IsAlive) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) RestartScene();
        }

        private void OnGUI()
        {
            if (playerHealth == null) return;
            EnsureStyles();
            DrawPlayerHealth();
            DrawDroneStatus();
            if (raidManager == null && !playerHealth.IsAlive) DrawDeathOverlay();
        }

        private void OnEnable()
        {
            if (playerHealth == null) playerHealth = GetComponent<CombatHealth>();
            CacheDroneProtection();
        }

        private void CacheDroneProtection()
        {
            if (droneProtection != null || playerHealth == null) return;
            droneProtection = playerHealth.GetComponent<DroneProtection>();
            if (droneProtection != null) droneProtection.DroneDisabled += OnDroneDisabled;
        }

        private void OnDisable()
        {
            if (droneProtection != null) droneProtection.DroneDisabled -= OnDroneDisabled;
            droneProtection = null;
        }

        private void OnDroneDisabled() { droneMessage = "Drone đã hết độ bền!"; droneMessageUntil = Time.time + 3f; }

        private void DrawDroneStatus()
        {
            if (droneProtection != null && droneProtection.EquippedDrone != null && droneProtection.EquippedDrone.Item != null)
            {
                var drone = droneProtection.EquippedDrone;
                const float x = 16f, y = 86f, width = 220f;
                GUI.Box(new Rect(x, y, width, 58f), GUIContent.none);
                GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 20f),
                    $"DRONE {drone.Item.TierLabel} — {drone.CurrentDurability}/{drone.EffectiveMaxDurability}", detailStyle);
                Rect bar = new Rect(x + 10f, y + 29f, width - 20f, 17f);
                GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                float ratio = drone.EffectiveMaxDurability > 0 ? drone.CurrentDurability / (float)drone.EffectiveMaxDurability : 0f;
                GUI.color = ratio <= 0f ? Color.gray : ratio < 0.25f ? Color.red : ratio < 0.6f ? Color.yellow : Color.green;
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (!string.IsNullOrEmpty(droneMessage) && Time.time <= droneMessageUntil)
                GUI.Box(new Rect(Screen.width * 0.5f - 150f, 82f, 300f, 32f), droneMessage);
        }

        private void DrawPlayerHealth()
        {
            const float x = 16f, y = 16f, width = 220f;
            GUI.Box(new Rect(x, y, width, 62f), GUIContent.none);
            GUI.Label(new Rect(x + 12f, y + 7f, width - 24f, 22f),
                $"Máu: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}", titleStyle);
            Rect bar = new Rect(x + 12f, y + 34f, width - 24f, 16f);
            GUI.color = new Color(0.18f, 0.08f, 0.08f, 1f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            float fraction = playerHealth.MaxHealth > 0 ? playerHealth.CurrentHealth / (float)playerHealth.MaxHealth : 0f;
            GUI.color = new Color(0.2f, 0.82f, 0.28f, 1f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawDeathOverlay()
        {
            Rect panel = new Rect(Screen.width * 0.5f - 145f, Screen.height * 0.5f - 75f, 290f, 150f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 8f, panel.y + 14f, panel.width - 16f, 40f), "Bạn đã chết", titleStyle);
            GUI.Label(new Rect(panel.x + 8f, panel.y + 58f, panel.width - 16f, 25f), "Nhấn R hoặc nút bên dưới để chơi lại", detailStyle);
            if (GUI.Button(new Rect(panel.x + 65f, panel.y + 95f, panel.width - 130f, 34f), "Chơi lại")) RestartScene();
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            stylesReady = true;
        }

        private static void RestartScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
