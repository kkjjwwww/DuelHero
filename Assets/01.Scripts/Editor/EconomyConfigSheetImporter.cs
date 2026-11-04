using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DuelHero.Data;
using UnityEngine;

public static class EconomyConfigSheetImporter
{
    public const string SourceFolder = "Assets/Data/EconomySheets";
    public static readonly string[] Names = { "RunConfig", "BattleRewardConfig", "ShopConfig" };
    public static string AssetPath(string name) => "Assets/Data/" + name + ".asset";
    public static ScriptableObject Parse(string folder, string name)
    {
        Type type = name switch { "RunConfig" => typeof(RunConfig), "BattleRewardConfig" => typeof(BattleRewardConfig),
            "ShopConfig" => typeof(ShopConfig), _ => throw new ArgumentException(name) };
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => f.FieldType == typeof(int)).ToArray();
        var rows = SheetCsvTable.Read(Path.Combine(folder, name + ".csv"), "key", "key", "value", "description");
        if (rows.Count != fields.Length || rows.Any(r => !fields.Any(f => f.Name == r["key"])))
            throw new FormatException(name + ": 설정 키 누락 또는 지원하지 않는 키");
        var config = ScriptableObject.CreateInstance(type);
        try
        {
            foreach (var field in fields) field.SetValue(config, SheetCsvTable.NonNegative(rows.Single(r => r["key"] == field.Name)["value"], name + "." + field.Name));
            if (config is ShopConfig shop && (shop.cardOfferCount < 1 ||
                (long)shop.commonWeight + shop.uncommonWeight + shop.rareWeight + shop.legendaryWeight == 0))
                throw new FormatException("ShopConfig: 상품 수와 희귀도 총 가중치는 양수여야 합니다.");
            return config;
        }
        catch { UnityEngine.Object.DestroyImmediate(config); throw; }
    }
}
