using System;

namespace DuelHero.Battle
{
    public sealed class EnemyPlanEvaluator
    {
        private readonly UtilityAiSettings settings;
        public EnemyPlanEvaluator(UtilityAiSettings settings) => this.settings = settings;

        // A cost of 20 at 20 energy carries more burden than the same cost at 100.
        public float AttackCostPenalty(int cost, int energyBefore) =>
            settings.attackCostWeight * cost * ((float)cost / Math.Max(1, energyBefore));

        internal float Evaluate(EnemyDecisionContext context, EnemyPlanningState state,
            EnemyPlannedAction[] selected, int count, float attackBurden, bool checkAttackOpportunity = true)
        {
            float opportunity = 0;
            if (state.TargetHealth > 0)
            {
                if (checkAttackOpportunity && context.HasAttackOpportunity(state.Position, state.Energy, selected, count)) opportunity = 1;
                if (context.DistancesToTarget.TryGetValue(context.State.Position, out int initial) &&
                    context.DistancesToTarget.TryGetValue(state.Position, out int current))
                    opportunity += .5f * Math.Min(3, Math.Max(0, initial - current));
            }
            return settings.damageWeight * state.TotalEffectiveDamage + settings.opportunityWeight * opportunity +
                settings.remainingEnergyWeight * state.Energy - attackBurden;
        }
    }
}
