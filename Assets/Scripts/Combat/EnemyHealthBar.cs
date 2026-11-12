using UnityEngine;

namespace Looter.Combat
{
    [RequireComponent(typeof(CombatHealth))]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float heightOffset = 0.9f;
        private CombatHealth health;
        private Camera sceneCamera;
        private GUIStyle labelStyle;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            sceneCamera = Camera.main;
        }

        private void OnGUI()
        {
            if (health == null || !health.IsAlive) return;
            if (sceneCamera == null) sceneCamera = Camera.main;
            if (sceneCamera == null) return;
            Vector3 screen = sceneCamera.WorldToScreenPoint(transform.position + Vector3.up * heightOffset);
            if (screen.z <= 0f) return;
            float x = screen.x - 28f, y = Screen.height - screen.y;
            Rect background = new Rect(x, y, 56f, 7f);
            GUI.color = new Color(0.12f, 0.08f, 0.08f, 0.9f);
            GUI.DrawTexture(background, Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.2f, 0.18f, 1f);
            float fraction = health.CurrentHealth / (float)health.MaxHealth;
            GUI.DrawTexture(new Rect(x, y, background.width * fraction, background.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            EnemyAI enemy = GetComponent<EnemyAI>();
            string name = enemy != null && enemy.Definition != null ? enemy.Definition.DisplayName : "Kẻ địch";
            if (enemy != null && enemy.IsElite) name = "★ " + name;
            GUI.Label(new Rect(x - 28f, y - 20f, 112f, 17f), $"{name}  {health.CurrentHealth}/{health.MaxHealth}", labelStyle);
        }
    }
}
