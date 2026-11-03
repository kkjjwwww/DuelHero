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

        internal EnemyActionScore Evaluate(EnemyDecisionContext context, EnemyPlanningState state,
            EnemyPlannedAction[] selected, int count, float attackBurden,
            EnemyPlanningState before, bool escape, EnemyActionScore prefix, EnemyRoundStyle style)
        {
            float opportunity = 0;
            if (state.TargetHealth > 0)
            {
                if (count < 3 && context.HasAttackOpportunity(state.Position, state.Energy, selected, count))
                    opportunity = settings.opportunityWeight * style.Attack;
                if (!escape) opportunity += settings.approachWeight * style.Maneuver * context.AttackApproach(state.Position);
            }
            int risk = state.TargetHealth > 0 ? context.RiskAt(state.Position) : 0;
            int blocked = Math.Min(risk, state.PreviewGuard);
            int exposed = risk - blocked;
            // Only added protection counts as a useful guard action; existing shields do not
            // turn a blocked move or an empty recovery into a useful action.
            int guardGain = blocked - Math.Min(risk, before.GuardAmount);
            bool ineffective = state.Position == before.Position && state.Energy == before.Energy &&
                state.TotalEffectiveDamage == before.TotalEffectiveDamage && guardGain <= 0;
            int riskBefore = Math.Max(0, context.RiskAt(before.Position) - before.GuardAmount);
            return new EnemyActionScore(settings.damageWeight * style.Attack * state.TotalEffectiveDamage,
                opportunity, settings.remainingEnergyWeight * style.Preparation * state.Energy, attackBurden,
                prefix.RiskPenalty + settings.riskWeight * style.Preparation * exposed,
                prefix.Escape + (escape ? settings.escapeWeight * style.Maneuver * Math.Max(0, riskBefore - exposed) : 0),
                prefix.IneffectivePenalty + (ineffective ? settings.ineffectiveActionPenalty : 0), risk, blocked);
        }
    }
}
