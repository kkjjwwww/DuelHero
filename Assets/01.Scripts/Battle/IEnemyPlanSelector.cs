namespace DuelHero.Battle
{
    public interface IEnemyPlanSelector<TState> where TState : struct, System.Enum
    {
        EnemyPlannedAction[] Select(EnemyDecisionContext context, TState state);
    }
}

