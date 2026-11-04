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
            EnemyPlanningState before, EnemyActionScore prefix, EnemyRoundStyle style,
            bool revisited, int cardDamage)
        {
            float opportunity = 0;
            bool canAttackNext = count < 3 && state.TargetHealth > 0 &&
                context.HasAttackOpportunity(state.Position, state.Energy, selected, count);
            if (state.TargetHealth > 0)
            {
                if (canAttackNext)
                    opportunity = settings.opportunityWeight * style.Attack;
                opportunity += settings.approachWeight * style.Maneuver * context.AttackApproach(state.Position);
            }
            int risk = state.TargetHealth > 0 ? context.RiskAt(state.Position) : 0;
            int blocked = Math.Min(risk, state.PreviewGuard);
            int exposed = risk - blocked;
            // Only added protection counts as a useful guard action; existing shields do not
            // turn a blocked move or an empty recovery into a useful action.
            int guardGain = blocked - Math.Min(risk, before.GuardAmount);
            int riskBefore = Math.Max(0, context.RiskAt(before.Position) - before.GuardAmount);
            bool moved = state.Position != before.Position;
            var prepared = context.GetPreparedAction(selected[count - 1]);
            // One benefit check for empty actions and movement. Returning toward a firing
            // position is not new progress if this round already visited that position.
            bool useful = cardDamage > 0 || state.Energy > before.Energy || guardGain > 0 || exposed < riskBefore ||
                prepared.AppliedBleed(state.Position, context.Target, state.MovementPath) > 0;
            if (moved && !revisited && !useful)
                useful = context.ImprovesAttackPosition(before.Position, state.Position) ||
                    (canAttackNext && !context.HasAttackOpportunity(before.Position, before.Energy, selected, count));
            bool ineffective = !moved && !useful;
            bool unproductiveMove = moved && !useful;
            float movePenalty = unproductiveMove ? (revisited
                ? Math.Max(settings.unproductiveMovePenalty, settings.revisitMovePenalty) : settings.unproductiveMovePenalty) : 0;
            return new EnemyActionScore(settings.damageWeight * style.Attack * state.TotalEffectiveDamage,
                opportunity, settings.remainingEnergyWeight * style.Preparation * state.Energy, attackBurden,
                prefix.RiskPenalty + settings.riskWeight * style.Preparation * exposed,
                prefix.IneffectivePenalty + (ineffective ? settings.ineffectiveActionPenalty : 0), risk, blocked,
                prefix.MovementPenalty + movePenalty, moved && revisited, unproductiveMove);
        }
    }
}
