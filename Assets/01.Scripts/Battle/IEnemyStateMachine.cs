namespace DuelHero.Battle
{
    public interface IEnemyStateMachine<TState> where TState : struct, System.Enum
    {
        TState Evaluate(EnemyDecisionContext context);
    }
}

