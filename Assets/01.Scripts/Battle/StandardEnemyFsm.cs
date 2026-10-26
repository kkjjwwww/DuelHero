using System.Linq;
using static DuelHero.Battle.EnemyPlanningRules;

namespace DuelHero.Battle
{
    public enum EnemyTacticState { Approach, Engage, Recover }

    public sealed class StandardEnemyFsm : IEnemyStateMachine<EnemyTacticState>
    {
        public EnemyTacticState Evaluate(EnemyDecisionContext context)
        {
            var state = context.State;
            var ready = context.Cards.Where(c => c.CanReserve(context.Round)).ToArray();
            var affordable = ready.Where(c => c.Definition.energyCost <= state.Energy).ToArray();
            if (NeedsEnergy(ready, state.Energy) && affordable.Any(c => Has(c, "energyRestore")))
                return EnemyTacticState.Recover;
            if (affordable.SelectMany(Actions).Any(action => Hits(action, state, context)))
                return EnemyTacticState.Engage;
            return EnemyTacticState.Approach;
        }
    }
}

