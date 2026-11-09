using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Data;
using DuelHero.Map;
using DuelHero.Run;
using DuelHero.Shop;
using UnityEditor;
using UnityEngine;

public static class EconomyValidation
{
    private static int checks;
    private static void Check(bool value, string rule) { if (!value) throw new Exception("Economy: " + rule); checks++; }
    public static uint ShopSeed()
    {
        for (uint seed = 0; seed < 10000; seed++)
        { var map = RouteMapGenerator.Generate(seed); if (map.GetNode(map.StartNodeId).NextNodeIds.Any(id => map.GetNode(id).Type == RouteNodeType.Shop)) return seed; }
        throw new Exception("테스트용 2단계 상점 시드 없음");
    }
    [MenuItem("Tools/Duel Hero/Economy/Validate gold and shop")]
    public static void Run()
    {
        checks = 0;
        var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(CardSheetImporter.DatabasePath);
        var shop = AssetDatabase.LoadAssetAtPath<ShopConfig>(EconomyConfigSheetImporter.AssetPath("ShopConfig"));
        var runConfig = AssetDatabase.LoadAssetAtPath<RunConfig>(EconomyConfigSheetImporter.AssetPath("RunConfig"));
        var reward = AssetDatabase.LoadAssetAtPath<BattleRewardConfig>(EconomyConfigSheetImporter.AssetPath("BattleRewardConfig"));
        Check(runConfig.startingGold == 30 && shop.cardOfferCount == 4, "latest sheet values");
        uint seed = ShopSeed();
        var run = new RunSession(seed, 100, 100, new[] { "slash_001" }, runConfig.startingGold);
        Check(run.Gold == 30 && run.TryBeginBattle(run.Map.StartNodeId), "start gold and battle");
        Check(run.TryCompleteBattle(97, run.CardIds, reward.normalBattleGold) && run.Gold == 60, "normal battle atomic gold");
        Check(!run.TryCompleteBattle(97, run.CardIds, reward.normalBattleGold) && run.Gold == 60, "duplicate completion no gold");
        string node = run.Map.GetNode(run.Progress.CurrentNodeId).NextNodeIds.First(id => run.Map.GetNode(id).Type == RouteNodeType.Shop);
        Check(run.TryBeginShop(node, db.cards, shop) && run.Progress.CurrentNodeId != node, "shop entry does not complete node");
        var offers = run.ShopOffers;
        Check(offers.Count == 4 && offers.Select(o => o.CardId).Distinct().Count() == 4, "four distinct offers");
        Check(!run.TryBeginBattle(node) && !run.TryBeginShop(node, db.cards, shop), "pending shop blocks another encounter");
        var generated = ShopOfferSelector.Generate(db.cards.Reverse(), shop, seed, node);
        Check(offers.Select(o => o.CardId).SequenceEqual(generated.Select(o => o.CardId)), "seed and database order stability");
        int index = Enumerable.Range(0, offers.Count).First(i => offers[i].Price <= run.Gold);
        var offer = offers[index]; int oldGold = run.Gold, count = run.CardIds.Count;
        Check(!run.TryBuyShopOffer("wrong-node", index) && run.Gold == oldGold, "wrong visit purchase rejected");
        Check(run.TryBuyShopOffer(node, index) && run.Gold == oldGold - offer.Price && run.CardIds.Count == count + 1 && offer.Sold, "atomic purchase");
        Check(!run.TryBuyShopOffer(node, index) && run.CardIds.Count == count + 1, "sold offer cannot buy twice");
        Check(!run.TryCancelShopEntry(node), "entry rollback cannot discard purchase");
        int expensive = Enumerable.Range(0, offers.Count).First(i => offers[i].Price > run.Gold);
        Check(!run.TryBuyShopOffer(node, expensive), "insufficient gold rejected");
        Check(ReferenceEquals(offers[index], run.ShopOffers[index]) && run.ShopOffers[index].Sold, "view reload preserves offers and stock");
        Check(run.TryCompleteShop(node) && run.Progress.CurrentNodeId == node && run.PendingShopNodeId == null, "exit completes shop");
        Check(!run.TryCompleteShop(node) && !run.TryBuyShopOffer(node, index), "exit retry cannot complete or buy again");
        while (!run.Progress.IsComplete)
        {
            string next = run.Map.GetNode(run.Progress.CurrentNodeId).NextNodeIds.First();
            var type = run.Map.GetNode(next).Type;
            if (type == RouteNodeType.Battle || type == RouteNodeType.Boss)
            {
                int before = run.Gold; Check(run.TryBeginBattle(next), "next battle entry");
                int gold = type == RouteNodeType.Boss ? reward.bossBattleGold : reward.normalBattleGold;
                Check(run.TryCompleteBattle(90, run.CardIds, gold) && run.Gold == before + gold, "battle/boss reward");
            }
            else Check(run.Progress.TrySelect(next), "utility progress");
        }
        Check(run.CardIds.Count(id => id == offer.CardId) == 1, "purchased card persists to end of run");
        Check(ShopOfferSelector.Generate(db.cards.Take(2).Select(c => new CardDefinition { id = c.id, rarity = "일반", isObtainable = true }), shop, seed, "small").Length == 2, "short pool");
        Check(ShopOfferSelector.Generate(Array.Empty<CardDefinition>(), shop, seed, "empty").Length == 0, "empty pool");
        var dummy = new CardDefinition { id = "reward_test_fake", rarity = "일반", isObtainable = true };
        Check(ShopOfferSelector.Generate(new[] { dummy, WaitCard.Definition }, shop, seed, "blocked").Length == 0, "dummy/wait exclusion");
        string randomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
        var single = UnityEngine.Object.Instantiate(shop);
        try
        {
            single.cardOfferCount = 1;
            var counts = new Dictionary<string, int> { ["일반"] = 0, ["고급"] = 0, ["희귀"] = 0, ["전설"] = 0 };
            var ids = db.cards.ToDictionary(c => c.id);
            var seen = new HashSet<string>();
            for (int i = 0; i < 10000; i++) { var item = ShopOfferSelector.Generate(db.cards, single, (uint)i, "shop-test")[0]; counts[ids[item.CardId].rarity]++; }
            Check(Math.Abs(counts["일반"] / 10000.0 - .62) < .025 && Math.Abs(counts["전설"] / 10000.0 - .03) < .015, "rarity sampling");
            for (int i = 0; i < 40; i++) seen.Add(ShopOfferSelector.Generate(db.cards, single, seed, "shop-" + i)[0].CardId);
            Check(seen.Count > 1, "different node ids use independent draw");
        }
        finally { UnityEngine.Object.DestroyImmediate(single); }
        Check(randomBefore == JsonUtility.ToJson(UnityEngine.Random.state), "global random state preserved");
        ValidateImportRollback();
        Debug.Log("ECONOMY_VALIDATION_PASS: " + checks + " checks; shop seed=" + seed);
    }
    private static void ValidateImportRollback()
    {
        string folder = Path.Combine("Temp", "EconomyValidation-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string before = File.ReadAllText(EconomyConfigSheetImporter.AssetPath("RunConfig"));
        try
        {
            foreach (var name in EconomyConfigSheetImporter.Names) File.Copy(Path.Combine(EconomyConfigSheetImporter.SourceFolder, name + ".csv"), Path.Combine(folder, name + ".csv"));
            var changed = GoogleSheetSyncClient.SplitConfigCsv(
                "group,key,value,description\nRunConfig,startingGold,30,start\nRunConfig,restHealAmount,47,rest\n",
                new[] { "RunConfig" });
            File.WriteAllText(Path.Combine(folder, "RunConfig.csv"), changed["RunConfig.csv"]);
            var parsedRun = (RunConfig)EconomyConfigSheetImporter.Parse(folder, "RunConfig");
            try { Check(parsedRun.restHealAmount == 47, "rest heal follows sheet value"); }
            finally { UnityEngine.Object.DestroyImmediate(parsedRun); }
            foreach (var bad in new[] { "key,value,description\nstartingGold,-1,bad\n", "key,value,description\nunknown,30,bad\n", "key,value,description\nstartingGold,30,bad\nstartingGold,40,bad\n" })
            {
                File.WriteAllText(Path.Combine(folder, "RunConfig.csv"), bad + "restHealAmount,30,rest\n"); bool rejected = false;
                try { GameSheetImporter.ImportFolder(folder, CardSheetImporter.DefaultSpreadsheetId, SheetSyncScope.Economy); }
                catch (FormatException) { rejected = true; }
                Check(rejected && before == File.ReadAllText(EconomyConfigSheetImporter.AssetPath("RunConfig")), "invalid config retains old asset");
            }
        }
        finally { Directory.Delete(folder, true); }
    }
}
