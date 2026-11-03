using System;

namespace DuelHero.Battle
{
    public enum EnemyDecisionStyle { Attack, Maneuver, Preparation }

    // Value snapshot: settings are never changed while evaluating a round.
    public readonly struct EnemyRoundStyle
    {
        public EnemyDecisionStyle Kind { get; }
        public float Attack { get; }
        public float Maneuver { get; }
        public float Preparation { get; }
        internal EnemyRoundStyle(EnemyDecisionStyle kind, UtilityAiSettings settings)
        {
            Kind = kind;
            Attack = kind == EnemyDecisionStyle.Attack ? settings.attackStyleMultiplier : 1f;
            Maneuver = kind == EnemyDecisionStyle.Maneuver ? settings.maneuverStyleMultiplier : 1f;
            Preparation = kind == EnemyDecisionStyle.Preparation ? settings.preparationStyleMultiplier : 1f;
        }
    }

    [Serializable]
    public sealed class UtilityAiSettings
    {
        public float damageWeight = 1f;
        public float opportunityWeight = 3f;
        public float remainingEnergyWeight = .03f;
        public float attackCostWeight = .2f;
        public float approachWeight = 1.5f;
        public float riskWeight = .1f;
        public float ineffectiveActionPenalty = 1f;
        public float unproductiveMovePenalty = 1f;
        public float revisitMovePenalty = 2f;
        // Zero selects the best deterministically; positive values soften the score gap.
        public float softmaxTemperature = 8f;
        public float attackStyleMultiplier = 1.2f;
        public float maneuverStyleMultiplier = 1.3f;
        public float preparationStyleMultiplier = 1.3f;

        internal void Validate()
        {
            if (!Valid(softmaxTemperature))
                throw new ArgumentOutOfRangeException(nameof(softmaxTemperature), "Softmax 온도는 유한한 0 이상의 값이어야 합니다.");
            if (!Valid(damageWeight) || !Valid(opportunityWeight) || !Valid(remainingEnergyWeight) || !Valid(attackCostWeight) ||
                !Valid(approachWeight) || !Valid(riskWeight) || !Valid(ineffectiveActionPenalty) ||
                !Valid(attackStyleMultiplier) || !Valid(maneuverStyleMultiplier) || !Valid(preparationStyleMultiplier) ||
                !Valid(unproductiveMovePenalty) || !Valid(revisitMovePenalty))
                throw new ArgumentOutOfRangeException(nameof(damageWeight), "유틸리티 가중치는 유한한 0 이상의 값이어야 합니다.");
        }
        private static bool Valid(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
