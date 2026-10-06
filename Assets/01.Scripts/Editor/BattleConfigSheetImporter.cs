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
        foreach (var row in rows)
        {
            if (row["key"] != "energyRecoveryPerTurn") throw new FormatException("지원하지 않는 BattleConfig 키: " + row["key"]);
            found = true;
            configured = row["value"].Length > 0;
            if (configured) recovery = SheetCsvTable.NonNegative(row["value"], "energyRecoveryPerTurn");
        }
        if (!found) throw new FormatException("BattleConfig: energyRecoveryPerTurn 행이 없습니다.");
        var config = ScriptableObject.CreateInstance<BattleConfig>();
        config.hasEnergyRecoveryPerTurn = configured; config.energyRecoveryPerTurn = recovery; return config;
    }
}
