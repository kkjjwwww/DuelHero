using System;
using System.IO;
using System.Linq;
using DuelHero.Data;
using UnityEngine;

public static class UnitSheetImporter
{
    public const string DatabasePath = "Assets/Data/UnitDatabase.asset";
    public const string SourceFolder = "Assets/Data/UnitSheets";
    public static UnitDatabase ParseFolder(string folder)
    {
        var players = Parse(Path.Combine(folder, "Players.csv"));
        var enemies = Parse(Path.Combine(folder, "Enemies.csv"));
        var database = ScriptableObject.CreateInstance<UnitDatabase>();
        database.players = players; database.enemies = enemies; return database;
    }
    private static UnitDefinition[] Parse(string path)
    {
        return SheetCsvTable.Read(path, "id", "id", "name", "startingHealth", "maxHealth", "startingEnergy", "maxEnergy").Select(row =>
        {
            var unit = new UnitDefinition { id = row["id"], name = row["name"],
                startingHealth = SheetCsvTable.NonNegative(row["startingHealth"], path + "/startingHealth"),
                maxHealth = SheetCsvTable.NonNegative(row["maxHealth"], path + "/maxHealth"),
                startingEnergy = SheetCsvTable.NonNegative(row["startingEnergy"], path + "/startingEnergy"),
                maxEnergy = SheetCsvTable.NonNegative(row["maxEnergy"], path + "/maxEnergy") };
            if (unit.name.Length == 0 || unit.maxHealth == 0 || unit.startingHealth > unit.maxHealth || unit.startingEnergy > unit.maxEnergy)
                throw new FormatException(path + ": " + unit.id + "의 이름·최대 체력·시작값/최대값을 확인해주세요.");
            return unit;
        }).ToArray();
    }
}
