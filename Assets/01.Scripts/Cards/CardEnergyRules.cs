using System.Collections.Generic;
using System.Linq;
namespace DuelHero.Cards
{
    public static class CardEnergyRules
    {
        // Simulation only: never modifies UnitStats or card instances.
        public static bool CanExecute(IReadOnlyList<CardInstance> cards, int currentEnergy, int maxEnergy, out string reason)
        {
            reason = null;
            if (currentEnergy < 0 || maxEnergy < currentEnergy) { reason = "에너지 상태가 올바르지 않습니다."; return false; }
            int energy = currentEnergy;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i].Definition;
                if (card.energyCost < 0) { reason = "카드 비용이 올바르지 않습니다."; return false; }
                if (energy < card.energyCost)
                {
                    reason = $"슬롯 {i + 1}: 에너지 부족 ({energy}/{card.energyCost})";
                    return false;
                }
                energy -= card.energyCost;
                if (card.effects == null) continue;
                foreach (var effect in card.effects.OrderBy(e => e.resolutionOrder))
                {
                    if (effect.effectType != "energyRestore") continue;
                    if (effect.value < 0 || effect.targetType != "자신") { reason = "지원하지 않는 에너지 회복 효과입니다."; return false; }
                    energy += System.Math.Min(effect.value, maxEnergy - energy);
                }
            }
            return true;
        }
    }
}
