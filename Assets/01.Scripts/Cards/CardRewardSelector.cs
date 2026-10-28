using System;
using System.Collections.Generic;
using System.Linq;

namespace DuelHero.Cards
{
    // Two-stage sampling: rarity first, then an owned-card discount within that rarity.
    public static class CardRewardSelector
    {
        private static readonly string[] Rarities = { "일반", "고급", "희귀", "전설" };

        public static CardDefinition[] Generate(IEnumerable<CardDefinition> definitions,
            IEnumerable<string> ownedIds, CardRewardSettings settings, uint runSeed, string nodeId)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var owned = new HashSet<string>(ownedIds);
            var remaining = definitions.Where(c => c != null && c.isObtainable && !c.isBasicAction &&
                    !WaitCard.IsWait(c) && settings.WeightFor(c.rarity) > 0)
                .GroupBy(c => c.id).Select(g => g.First()).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            var selected = new List<CardDefinition>(3);
            var random = new Random(unchecked((int)CreateSeed(runSeed, nodeId)));
            double multiplier = Math.Max(0, Math.Min(1, settings.ownedCardMultiplier));
            while (selected.Count < 3 && remaining.Count > 0)
            {
                var rarities = Rarities.Where(r => remaining.Any(c => c.rarity == r &&
                    (!owned.Contains(c.id) || multiplier > 0))).ToArray();
                if (rarities.Length == 0) break;
                string rarity = Draw(rarities, r => settings.WeightFor(r), random);
                var candidates = remaining.Where(c => c.rarity == rarity).ToArray();
                var card = Draw(candidates, c => owned.Contains(c.id) ? multiplier : 1, random);
                selected.Add(card);
                remaining.Remove(card);
            }
            return selected.ToArray();
        }

        private static T Draw<T>(IReadOnlyList<T> values, Func<T, double> weight, Random random)
        {
            double total = 0;
            for (int i = 0; i < values.Count; i++) total += weight(values[i]);
            double roll = random.NextDouble() * total;
            for (int i = 0; i < values.Count; i++)
            {
                roll -= weight(values[i]);
                if (roll < 0) return values[i];
            }
            return values[values.Count - 1];
        }

        private static uint CreateSeed(uint runSeed, string nodeId)
        {
            uint hash = unchecked(2166136261u ^ runSeed);
            foreach (char value in "card-reward:" + nodeId)
                hash = unchecked((hash ^ value) * 16777619u);
            return hash;
        }
    }
}
