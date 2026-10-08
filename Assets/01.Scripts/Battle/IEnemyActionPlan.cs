using System.Collections.Generic;
using DuelHero.Cards;
namespace DuelHero.Battle
{
    public interface IEnemyActionPlan
    {
        IReadOnlyList<CardDefinition> CreatePlan(int round);
        void CompletePlan(int round, int executedSlots);
    }
}
