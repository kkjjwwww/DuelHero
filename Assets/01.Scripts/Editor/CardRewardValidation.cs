using System;
using System.Linq;
using DuelHero.Cards;
using UnityEditor;
using UnityEngine;

internal static class CardRewardValidation
{
    [MenuItem("Tools/Duel Hero/Rewards/Validate card rewards")]
    public static void Run()
    {
        var settings = ScriptableObject.CreateInstance<CardRewardSettings>();
        try
        {
            string[] rarities = { "일반", "고급", "희귀", "전설" };
            var cards = rarities.SelectMany(r => Enumerable.Range(0, 2).Select(i => Card(r + i, r))).ToArray();
            var empty = Array.Empty<string>();
            string beforeRandom = JsonUtility.ToJson(UnityEngine.Random.state);
            var first = CardRewardSelector.Generate(cards, empty, settings, 42, "battle-1");
            Require(first.Select(c => c.id).SequenceEqual(
                CardRewardSelector.Generate(cards.Reverse(), empty, settings, 42, "battle-1").Select(c => c.id)),
                "동일 시드 또는 DB 순서 독립성");
            Require(first.Length == 3 && first.Select(c => c.id).Distinct().Count() == 3, "후보 3종 중복 제거");
            Require(beforeRandom == JsonUtility.ToJson(UnityEngine.Random.state), "전역 난수 상태 보존");
            Require(CardRewardSelector.Generate(Array.Empty<CardDefinition>(), empty, settings, 1, "x").Length == 0,
                "빈 보상 풀");
            Require(CardRewardSelector.Generate(cards.Take(2), empty, settings, 1, "x").Length == 2,
                "3종 미만의 보상 풀");
            var excluded = new[] { Card("blocked", "일반"), Card("basic", "일반"), Card(WaitCard.Id, "일반") };
            excluded[0].isObtainable = false;
            excluded[1].isBasicAction = true;
            Require(CardRewardSelector.Generate(excluded, empty, settings, 1, "x").Length == 0,
                "획득 불가·기본 행동·대기 제외");
            Require(CardRewardSelector.Generate(new[] { cards[0], cards[0] }, empty, settings, 1, "x").Length == 1,
                "같은 ID 중복 정의 제외");
            Require(CardRewardSelector.Generate(cards.Where(c => c.rarity == "전설"), empty, settings, 1, "x")
                .All(c => c.rarity == "전설"), "빈 등급의 가중치 재분배");

            const int samples = 20000;
            int[] counts = new int[4];
            for (uint seed = 0; seed < samples; seed++)
            {
                var reward = CardRewardSelector.Generate(cards, empty, settings, seed, "rarity");
                counts[Array.IndexOf(rarities, reward[0].rarity)]++;
            }
            double[] expected = { .62, .25, .10, .03 };
            for (int i = 0; i < counts.Length; i++)
                Require(Math.Abs(counts[i] / (double)samples - expected[i]) < .015, "희귀도 분포 " + rarities[i]);

            var pair = new[] { Card("owned", "일반"), Card("new", "일반") };
            int ownedCount = 0;
            for (uint seed = 0; seed < samples; seed++)
            {
                var one = CardRewardSelector.Generate(pair, new[] { "owned" }, settings, seed, "ownership");
                var many = CardRewardSelector.Generate(pair, new[] { "owned", "owned", "owned" }, settings, seed, "ownership");
                Require(one.Select(c => c.id).SequenceEqual(many.Select(c => c.id)), "보유 사본 수에 독립적인 감점");
                if (one[0].id == "owned") ownedCount++;
            }
            Require(Math.Abs(ownedCount / (double)samples - .2) < .015, "소유 카드 가중치 1/4");
            var allOwned = CardRewardSelector.Generate(pair, new[] { "owned", "new" }, settings, 1, "x");
            Require(allOwned.Length == 2, "모두 소유한 보상 풀");
            settings.commonWeight = settings.uncommonWeight = settings.rareWeight = settings.legendaryWeight = 0;
            Require(CardRewardSelector.Generate(cards, empty, settings, 1, "x").Length == 0, "추첨 비활성 설정");
            Debug.Log("카드 보상 검증 통과: 재현성·중복·제외·후보 부족·희귀도 20,000시드·소유 감점 20,000시드. " +
                "등급 건수=" + string.Join(",", counts) + "; 소유 카드=" + ownedCount);
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }

    private static CardDefinition Card(string id, string rarity) => new()
    {
        id = id, rarity = rarity, isObtainable = true,
        effects = Array.Empty<CardEffectDefinition>(), keywordIds = Array.Empty<string>()
    };

    private static void Require(bool condition, string rule)
    {
        if (!condition) throw new InvalidOperationException("카드 보상 검증 실패: " + rule);
    }
}
