using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEngine;
namespace DuelHero.Battle
{
    // Inspector-authored plan, replaceable by AI through IEnemyActionPlan.
    public sealed class TemporaryEnemyPlan : MonoBehaviour, IEnemyActionPlan
    {
        [SerializeField] private CardDatabase database;
        [SerializeField] private UnitStats stats;
        [SerializeField] private string[] cardIds = { "move_left_001", "slash_001", "breathing_001" };
        private readonly Dictionary<string, CardInstance> instances = new();
        private CardInstance[] activePlan;
        public IReadOnlyList<CardDefinition> CreatePlan(int round)
        {
            if (database == null || stats == null || !stats.IsInitialized)
                throw new InvalidOperationException("적 계획의 카드 데이터와 유닛 상태를 연결해주세요.");
            activePlan = new CardInstance[3];
            var selected = new HashSet<CardInstance>();
            var result = new CardDefinition[3];
            int energy = stats.Energy;
            for (int slot = 0; slot < 3; slot++)
            {
                string id = slot < cardIds.Length ? cardIds[slot] : null;
                if (string.IsNullOrEmpty(id)) continue;
                if (!instances.TryGetValue(id, out var instance))
                {
                    var definition = database.cards.FirstOrDefault(c => c.id == id);
                    if (definition == null) throw new InvalidOperationException("적 계획의 카드 ID 오류: " + id);
                    instances.Add(id, instance = new CardInstance(definition));
                }
                if (!instance.CanReserve(round) || selected.Contains(instance) || energy < instance.Definition.energyCost) continue;
                selected.Add(instance);
                activePlan[slot] = instance; result[slot] = instance.Definition;
                energy -= instance.Definition.energyCost;
                foreach (var effect in instance.Definition.effects.OrderBy(e => e.resolutionOrder))
                    if (effect.effectType == "energyRestore" && effect.targetType == "자신")
                        energy += Math.Min(effect.value, stats.MaxEnergy - energy);
            }
            return result;
        }
        public void CompletePlan(int round, int executedSlots)
        {
            if (activePlan == null) return;
            for (int i = 0; i < Math.Min(executedSlots, activePlan.Length); i++) activePlan[i]?.MarkUsed(round);
            activePlan = null;
        }
    }
}
