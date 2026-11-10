using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Units
{
    internal static class HealthBarFeedback
    {
        internal static readonly Color HealingTint = new(.35f, 1f, .48f);
        internal const float HealFillDuration = .3f;

        internal static void AppendFrameFlash(Sequence motion, Image frame, Color tint)
        {
            frame.color = new Color(tint.r, tint.g, tint.b, 0f);
            motion.Append(frame.DOFade(.85f, .08f));
            motion.Append(frame.DOFade(0f, .12f));
        }

        internal static void InsertHealingTint(Sequence motion, Image fill, Color restColor)
        {
            fill.color = HealingTint;
            motion.Insert(0f, fill.DOColor(restColor, .2f));
        }
    }
}
