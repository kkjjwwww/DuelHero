using System;

namespace DuelHero.Battle
{
    // One small shared ruleset for stage 2. Enemy personality profiles are a later step.
    [Serializable]
    public sealed class UtilityAiSettings
    {
        public int retainedPlans = 4;
        public int actionsPerPlan = 6;
        public float damageWeight = 1f;
        public float opportunityWeight = 3f;
        public float remainingEnergyWeight = .03f;
        public float attackCostWeight = .2f;

        internal void Validate()
        {
            if (retainedPlans < 1 || retainedPlans > 16 || actionsPerPlan < 3 || actionsPerPlan > 16)
                throw new ArgumentOutOfRangeException(nameof(retainedPlans), "유지 계획은 1~16개, 행동 후보는 3~16개로 설정해주세요.");
            if (!Valid(damageWeight) || !Valid(opportunityWeight) || !Valid(remainingEnergyWeight) || !Valid(attackCostWeight))
                throw new ArgumentOutOfRangeException(nameof(damageWeight), "유틸리티 가중치는 유한한 0 이상의 값이어야 합니다.");
        }
        private static bool Valid(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
