using System;

namespace DuelHero.Battle
{
    // One small shared ruleset for stage 2. Enemy personality profiles are a later step.
    [Serializable]
    public sealed class UtilityAiSettings
    {
        public float damageWeight = 1f;
        public float opportunityWeight = 3f;
        public float remainingEnergyWeight = .03f;
        public float attackCostWeight = .2f;
        public int randomCandidateCount = 3;
        public float randomScoreTolerance = .1f;

        internal void Validate()
        {
            if (randomCandidateCount < 1 || randomCandidateCount > 3 || !Valid(randomScoreTolerance) || randomScoreTolerance > 1)
                throw new ArgumentOutOfRangeException(nameof(randomCandidateCount), "무작위 후보는 1~3개, 허용 점수 차이는 0~1로 설정해주세요.");
            if (!Valid(damageWeight) || !Valid(opportunityWeight) || !Valid(remainingEnergyWeight) || !Valid(attackCostWeight))
                throw new ArgumentOutOfRangeException(nameof(damageWeight), "유틸리티 가중치는 유한한 0 이상의 값이어야 합니다.");
        }
        private static bool Valid(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
