using System;
using System.IO;
using DuelHero.Data;
using UnityEngine;

public static class BattleConfigSheetImporter
{
    public const string DatabasePath = "Assets/Data/BattleConfig.asset";
    public const string SourceFolder = "Assets/Data/BattleConfigSheets";
    public static BattleConfig ParseFolder(string folder)
    {
        var rows = SheetCsvTable.Read(Path.Combine(folder, "BattleConfig.csv"), "key", "key", "value", "description");
        bool configured = false; int recovery = 0; bool found = false;
        int healthIncrease = 0;
        int energyIncrease = 0;
        foreach (var row in rows)
        {
            string key = row["key"];
            string value = row["value"];
            switch (key)
            {
                case "energyRecoveryPerTurn":
                    found = true;
                    configured = value.Length > 0;
                    if (configured) recovery = SheetCsvTable.NonNegative(value, key);
                    break;
                case "enemyHealthIncreasePercentPerStage":
                    healthIncrease = SheetCsvTable.NonNegative(value, key);
                    break;
                case "enemyEnergyIncreasePercentPerStage":
                    energyIncrease = SheetCsvTable.NonNegative(value, key);
                    break;
                default:
                    throw new FormatException("지원하지 않는 BattleConfig 키: " + key);
            }
        }
        if (!found) throw new FormatException("BattleConfig: energyRecoveryPerTurn 행이 없습니다.");
        var config = ScriptableObject.CreateInstance<BattleConfig>();
        config.hasEnergyRecoveryPerTurn = configured;
        config.energyRecoveryPerTurn = recovery;
        config.enemyHealthIncreasePercentPerStage = healthIncrease;
        config.enemyEnergyIncreasePercentPerStage = energyIncrease;
        return config;
    }
}
