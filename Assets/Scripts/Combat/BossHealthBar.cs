using UnityEngine;

namespace Looter.Combat
{
    public sealed class BossHealthBar : MonoBehaviour
    {
        [SerializeField] private BossController boss;
        [SerializeField, Min(0.1f)] private float hideDelayAfterDeath = 2f;
        private float deathTime;
        private GUIStyle titleStyle;
        private GUIStyle phaseStyle;

        private void Awake()
        {
            if (boss == null) boss = FindAnyObjectByType<BossController>();
        }

        private void OnGUI()
        {
            if (boss == null) boss = FindAnyObjectByType<BossController>();
            if (boss == null || !boss.IsHudVisible) return;
            if (boss.IsDead)
            {
                if (deathTime <= 0f) deathTime = Time.unscaledTime;
                if (Time.unscaledTime - deathTime >= hideDelayAfterDeath) return;
            }
            else deathTime = 0f;
            EnsureStyles();
            float width = Mathf.Min(520f, Screen.width * 0.58f);
            float x = (Screen.width - width) * 0.5f;
            GUI.Box(new Rect(x, 82f, width, 66f), GUIContent.none);
            titleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x + 8f, 83f, width - 16f, 23f), boss.DisplayName, titleStyle);
            float fraction = boss.MaximumHealth > 0 ? Mathf.Clamp01(boss.CurrentHealth / (float)boss.MaximumHealth) : 0f;
            Rect background = new Rect(x + 18f, 109f, width - 36f, 15f);
            GUI.color = new Color(0.15f, 0.06f, 0.08f, 1f);
            GUI.DrawTexture(background, Texture2D.whiteTexture);
            GUI.color = boss.CurrentPhase == 2 ? new Color(1f, 0.27f, 0.17f) : new Color(0.78f, 0.12f, 0.17f);
            GUI.DrawTexture(new Rect(background.x, background.y, background.width * fraction, background.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            phaseStyle.normal.textColor = new Color(1f, 0.85f, 0.5f);
            GUI.Label(new Rect(x, 124f, width, 19f), $"GIAI ĐOẠN {boss.CurrentPhase}   ·   {boss.CurrentHealth}/{boss.MaximumHealth}", phaseStyle);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            phaseStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
    }
}
