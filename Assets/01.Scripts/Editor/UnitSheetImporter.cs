using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuelHero.Cards;
using DuelHero.Data;
using UnityEditor;
using UnityEngine;

public static class UnitSheetImporter
{
    public const string DatabasePath = "Assets/Data/UnitDatabase.asset";
    public const string SourceFolder = "Assets/Data/UnitSheets";
    public const string EncounterProfilesPath = "Assets/Data/EnemyEncounterProfiles.asset";

    public static EnemyEncounterProfiles ParseEncounterProfiles(string folder, CardDatabase cards)
    {
        const string commonColumn = "fixedCommonCardIds";
        const string uniqueColumn = "uniqueCardId";
        const string bonusColumn = "bonusCandidateCardIds";
        var rows = SheetCsvTable.Read(Path.Combine(folder, "Enemies.csv"), "id", "id")
            .Where(row => new[] { commonColumn, uniqueColumn, bonusColumn }
                .Any(column => row.TryGetValue(column, out var value) && value.Length > 0));
        EnemyEncounterProfiles parsed = null;
        try
        {
            foreach (var row in rows)
            {
                string id = row["id"];
                string[] common = ReadCardIds(row.GetValueOrDefault(commonColumn));
                string[] unique = ReadCardIds(row.GetValueOrDefault(uniqueColumn));
                string[] bonus = ReadCardIds(row.GetValueOrDefault(bonusColumn));
                var all = common.Concat(unique).Concat(bonus).ToArray();
                if (parsed == null)
                {
                    var source = AssetDatabase.LoadAssetAtPath<EnemyEncounterProfiles>(EncounterProfilesPath);
                    if (source == null) throw new InvalidOperationException(EncounterProfilesPath + " 파일이 필요합니다.");
                    parsed = UnityEngine.Object.Instantiate(source);
                }
                var profile = parsed.FindByUnitId(id);
                if (profile == null) throw new FormatException($"Enemies.csv: {id}의 전투 프로필을 찾을 수 없습니다.");
                int requiredCommonCards = parsed.IsBoss(id) ? 3 : 2;
                if (common.Length != requiredCommonCards || unique.Length != 1 || bonus.Length == 0 ||
                    all.Distinct().Count() != all.Length ||
                    cards == null || all.Any(cardId => !cards.cards.Any(card => card.id == cardId)))
                    throw new FormatException($"Enemies.csv: {id}의 공용 카드 {requiredCommonCards}장, 고유 카드 1장, 추가 후보와 카드 ID를 확인해주세요.");

                profile.fixedCards = common.Concat(unique).ToArray();
                profile.bonusCandidates = bonus;
            }
            return parsed;
        }
        catch
        {
            if (parsed != null) UnityEngine.Object.DestroyImmediate(parsed);
            throw;
        }
    }

    private static string[] ReadCardIds(string value) =>
        (value ?? "").Split(';').Select(id => id.Trim()).Where(id => id.Length > 0).ToArray();

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
