using DG.Tweening;
using DuelHero.Battle;
using DuelHero.Units;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Run
{
    public sealed class RestHealthBar : MonoBehaviour
    {
        [SerializeField] private Image healthFill;
        [SerializeField] private Image frameFlash;
        [SerializeField] private Text healthNumber;
        private Color restFillColor;

        private void Awake() => restFillColor = healthFill.color;

        internal void SetHealth(int health, int maximum)
        {
            healthFill.color = restFillColor;
            frameFlash.color = Color.clear;
            RenderHealth(health, maximum);
        }

        internal Sequence AnimateRecovery(int before, int after, int maximum)
        {
            SetHealth(before, maximum);
            var motion = DOTween.Sequence().SetUpdate(true);
            if (after > before)
            {
                HealthBarFeedback.AppendFrameFlash(motion, frameFlash, HealthBarFeedback.HealingTint);
                HealthBarFeedback.InsertHealingTint(motion, healthFill, restFillColor);
                motion.Insert(0f, DOVirtual.Float(before, after, HealthBarFeedback.HealFillDuration,
                    value => RenderHealth(value, maximum)).SetEase(Ease.OutQuad));
            }
            return motion.AppendInterval(.25f);
        }

        private void RenderHealth(float health, int maximum)
        {
            EnergyCostPreviewUI.SegmentView.SetRange(healthFill, 0f, health, maximum);
            healthNumber.text = Mathf.RoundToInt(health).ToString();
        }
    }
}
