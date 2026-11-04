using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Data;

namespace DuelHero.Shop
{
    public static class ShopOfferSelector
    {
        public static ShopOffer[] Generate(IEnumerable<CardDefinition> definitions, ShopConfig config, uint seed, string nodeId)
        {
            if (config == null || config.cardOfferCount <= 0 || string.IsNullOrEmpty(nodeId)) throw new ArgumentException("상점 설정과 노드가 필요합니다.");
            var remaining = definitions.Where(c => c != null && c.isObtainable && !c.isBasicAction && !WaitCard.IsWait(c) &&
                !c.id.StartsWith("reward_test_", StringComparison.Ordinal) && config.WeightFor(c.rarity) > 0)
                .GroupBy(c => c.id).Select(g => g.First()).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            uint hash = unchecked(2166136261u ^ seed);
            foreach (char c in "shop:" + nodeId) hash = unchecked((hash ^ c) * 16777619u);
            var random = new Random(unchecked((int)hash));
            var offers = new List<ShopOffer>();
            while (offers.Count < config.cardOfferCount && remaining.Count > 0)
            {
                var rarities = new[] { "일반", "고급", "희귀", "전설" }.Where(r => remaining.Any(c => c.rarity == r)).ToArray();
                long total = rarities.Sum(r => (long)config.WeightFor(r));
                double roll = random.NextDouble() * total;
                string rarity = rarities.Last();
                foreach (var r in rarities) { roll -= config.WeightFor(r); if (roll < 0) { rarity = r; break; } }
                var candidates = remaining.Where(c => c.rarity == rarity).ToArray();
                var selected = candidates[random.Next(candidates.Length)];
                offers.Add(new ShopOffer(selected.id, config.PriceFor(selected.rarity)));
                remaining.Remove(selected);
            }
            return offers.ToArray();
        }
    }
}
