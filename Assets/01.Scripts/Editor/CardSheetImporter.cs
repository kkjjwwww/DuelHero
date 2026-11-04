using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DuelHero.Cards;
using DuelHero.Battle;
using UnityEditor;
using UnityEngine;

public static class CardSheetImporter
{
    public const string SourceFolder = "Assets/Data/CardSheets";
    public const string DatabasePath = "Assets/Data/CardDatabase.asset";
    public const string DefaultSpreadsheetId = "1pspfXZtUhTmSc9ZTL1iTzQDeBgvfbb_wqwCNs83EFQE";

    [MenuItem("Tools/Duel Hero/Cards/Reimport saved sheet data")]
    public static void ReimportSaved()
    {
        try { Debug.Log(ImportFolder(SourceFolder)); }
        catch (Exception error) { Debug.LogError("Card import rejected. Previous database retained.\n" + error.Message); }
    }

    [MenuItem("Tools/Duel Hero/Cards/Import downloaded CSV folder")]
    public static void ImportDownloaded()
    {
        string folder = EditorUtility.OpenFolderPanel("Folder containing Cards.csv, Effects.csv and Keywords.csv", "", "");
        if (string.IsNullOrEmpty(folder)) return;
        try
        {
            string result = ImportFolder(folder);
            AssetDatabase.Refresh();
            Debug.Log(result);
        }
        catch (Exception error) { Debug.LogError("Card import failed: " + error.Message); }
    }

    // Fully parse and validate before touching the existing Unity asset.
    public static string ImportFolder(string folder, string spreadsheetId = DefaultSpreadsheetId, bool refreshViews = true)
    {
        CardDatabase parsed = ParseFolder(folder);
        CardDatabase target = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
        bool created = target == null;
        string previousDatabase = created ? null : JsonUtility.ToJson(target);
        var previousCsv = new Dictionary<string, byte[]>();
        var incomingCsv = new Dictionary<string, byte[]>();
        try
        {
            foreach (string file in new[] { "Cards.csv", "Effects.csv", "Keywords.csv", "Statuses.csv" }.Where(f => f != "Statuses.csv" || File.Exists(Path.Combine(folder, f))))
            {
                string destination = Path.Combine(SourceFolder, file);
                previousCsv[destination] = File.Exists(destination) ? File.ReadAllBytes(destination) : null;
                incomingCsv[destination] = File.ReadAllBytes(Path.Combine(folder, file));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            Directory.CreateDirectory(SourceFolder);
            if (created)
            {
                target = ScriptableObject.CreateInstance<CardDatabase>();
                AssetDatabase.CreateAsset(target, DatabasePath);
            }
            Undo.RecordObject(target, "Import card sheet data");
            foreach (var file in incomingCsv) File.WriteAllBytes(file.Key, file.Value);
            target.cards = parsed.cards;
            target.keywords = parsed.keywords;
            target.statuses = parsed.statuses;
            target.sourceSpreadsheetId = spreadsheetId;
            target.importedAtUtc = DateTime.UtcNow.ToString("o");
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }
        catch
        {
            foreach (var file in previousCsv)
            {
                if (file.Value != null) File.WriteAllBytes(file.Key, file.Value);
                else if (File.Exists(file.Key)) File.Delete(file.Key);
            }
            if (created && target != null) AssetDatabase.DeleteAsset(DatabasePath);
            else if (target != null && previousDatabase != null)
            {
                JsonUtility.FromJsonOverwrite(previousDatabase, target);
                EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target);
            }
            throw;
        }
        finally { UnityEngine.Object.DestroyImmediate(parsed); }
        if (refreshViews) RefreshDeckViews();
        return $"Imported {target.cards.Length} cards, {target.cards.Sum(c => c.effects.Length)} effects, {target.keywords.Length} keywords.";
    }

    public static void RefreshDeckViews()
    {
        foreach (var view in UnityEngine.Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!view.gameObject.scene.IsValid()) continue;
            try
            {
                view.Rebuild();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            catch (Exception error) { Debug.LogWarning("Card data saved, but deck preview refresh failed: " + error.Message); }
        }
    }

    public static CardDatabase ParseFolder(string folder)
    {
        var cardRows = Table(Path.Combine(folder, "Cards.csv"), "id", "name", "description", "actionType", "energyCost", "keywordIds", "rarity", "isObtainable", "isBasicAction", "isStarterCard");
        var effectRows = Table(Path.Combine(folder, "Effects.csv"), "id", "cardId", "resolutionOrder", "effectType", "value", "durationSlots", "rangeOffsets", "targetType");
        var keywordRows = Table(Path.Combine(folder, "Keywords.csv"), "id", "name", "description", "resolutionRule", "triggerTiming", "parameters");
        var statuses = File.Exists(Path.Combine(folder, "Statuses.csv"))
            ? Table(Path.Combine(folder, "Statuses.csv"), "id", "name", "category", "keywordId", "stackRule", "countUnit")
                .Select(r => new StatusDefinition { id = Required(r, "id"), name = Required(r, "name"), category = Required(r, "category"),
                    keywordId = Required(r, "keywordId"), stackRule = Required(r, "stackRule"), countUnit = Required(r, "countUnit") }).ToArray()
            : Array.Empty<StatusDefinition>();
        var cards = cardRows.Select(r => new CardDefinition
        {
            id = Required(r, "id"), name = Required(r, "name"), description = Required(r, "description"),
            actionType = Required(r, "actionType"), rarity = Required(r, "rarity"), energyCost = Number(r, "energyCost"),
            cooldownTurns = r.TryGetValue("cooldownTurns", out var cooldown) && !string.IsNullOrWhiteSpace(cooldown)
                ? Number(r, "cooldownTurns") : 1,
            keywordIds = r["keywordIds"].Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray(),
            isObtainable = Boolean(r, "isObtainable"), isBasicAction = Boolean(r, "isBasicAction"), isStarterCard = Boolean(r, "isStarterCard")
        }).ToArray();
        var keywords = keywordRows.Select(r => new KeywordDefinition
        {
            id = Required(r, "id"), name = Required(r, "name"), description = Required(r, "description"),
            resolutionRule = Required(r, "resolutionRule"), triggerTiming = Required(r, "triggerTiming"), parameters = r["parameters"],
            textColor = r.TryGetValue("textColor", out var color) ? color : string.Empty
        }).ToArray();
        var effects = effectRows.Select(r => new CardEffectDefinition
        {
            id = Required(r, "id"), cardId = Required(r, "cardId"), effectType = Required(r, "effectType"),
            resolutionOrder = Number(r, "resolutionOrder"), value = Number(r, "value"), durationSlots = Number(r, "durationSlots"),
            statusId = r.TryGetValue("statusId", out var status) ? status : string.Empty,
            triggerEffectId = r.TryGetValue("triggerEffectId", out var trigger) ? trigger : string.Empty,
            triggerTiming = r.TryGetValue("triggerTiming", out var timing) ? timing : string.Empty,
            movementStyle = r.TryGetValue("movementStyle", out var style) ? style : string.Empty,
            rangeBasis = r.TryGetValue("rangeBasis", out var basis) ? basis : string.Empty,
            statusMultiplier = r.TryGetValue("statusMultiplier", out var multiplier) && !string.IsNullOrWhiteSpace(multiplier) ? Number(r, "statusMultiplier") : 0,
            rangeOffsets = Offsets(r["rangeOffsets"]), targetType = Required(r, "targetType")
        }).ToArray();
        if (cards.Length == 0) throw new FormatException("No card rows with an id.");
        Unique(cards.Select(c => c.id), "card id"); Unique(effects.Select(e => e.id), "effect id"); Unique(keywords.Select(k => k.id), "keyword id");
        var cardIds = new HashSet<string>(cards.Select(c => c.id));
        var keywordIds = new HashSet<string>(keywords.Select(k => k.id));
        foreach (var keyword in keywords)
            if (!string.IsNullOrWhiteSpace(keyword.textColor) && !Regex.IsMatch(keyword.textColor, @"^#[0-9a-fA-F]{6}$"))
                throw new FormatException($"{keyword.id}: textColor must be #RRGGBB");
        Unique(statuses.Select(s => s.id), "status id");
        foreach (var status in statuses)
            if ((status.id != "stun" && status.id != "bleed") || status.category != "디버프" || status.stackRule != "add" ||
                status.countUnit != (status.id == "bleed" ? "stack" : "actionSlot") || !keywordIds.Contains(status.keywordId))
                throw new FormatException($"{status.id}: unsupported status definition or missing keyword");
        foreach (var effect in effects)
        {
            if (!cardIds.Contains(effect.cardId)) throw new FormatException($"{effect.id}: missing cardId {effect.cardId}");
            if (!new[] { "move", "guard", "damage", "energyRestore", "healthRestore", "applyStatus" }.Contains(effect.effectType))
                throw new FormatException($"{effect.id}: unsupported effectType {effect.effectType}");
            if (effect.resolutionOrder < 1) throw new FormatException($"{effect.id}: resolutionOrder must be positive");
            if (!string.IsNullOrEmpty(effect.triggerTiming) && effect.triggerTiming != "action" && !effect.IsCounter)
                throw new FormatException($"{effect.id}: unsupported triggerTiming");
            if (effect.IsCounter && (effect.effectType == "move" || effect.effectType == "applyStatus" ||
                !cards.Single(c => c.id == effect.cardId).keywordIds.Contains("counter")))
                throw new FormatException($"{effect.id}: unsupported counter effect or missing keyword");
            if (effect.statusMultiplier > 0 && (effect.effectType != "damage" || effect.statusId != "bleed" || !statuses.Any(s => s.id == "bleed")))
                throw new FormatException($"{effect.id}: unsupported status scaling");
            if (effect.effectType == "move" && (effect.rangeOffsets.Length != 1 ||
                Math.Abs((long)effect.rangeOffsets[0].x) + Math.Abs((long)effect.rangeOffsets[0].y) != 1))
                throw new FormatException($"{effect.id}: move requires one cardinal unit offset");
            if (!new[] { "자신", "적" }.Contains(effect.targetType)) throw new FormatException($"{effect.id}: invalid targetType");
            if (effect.effectType == "applyStatus" && (effect.value <= 0 || effect.durationSlots != 0 || effect.targetType != "적" ||
                !statuses.Any(s => s.id == effect.statusId) || !effects.Any(hit => hit.cardId == effect.cardId && hit.id == effect.triggerEffectId &&
                    hit.effectType == "damage" && hit.resolutionOrder < effect.resolutionOrder && hit.IsCounter == effect.IsCounter)))
                throw new FormatException($"{effect.id}: invalid status or hit-effect reference");
        }
        foreach (var card in cards)
        {
            if (!new[] { "공격", "이동", "방어", "회복" }.Contains(card.actionType)) throw new FormatException($"{card.id}: invalid actionType");
            foreach (string keyword in card.keywordIds)
                if (!keywordIds.Contains(keyword)) throw new FormatException($"{card.id}: missing keyword {keyword}");
            card.effects = effects.Where(e => e.cardId == card.id).OrderBy(e => e.resolutionOrder).ToArray();
            if (!card.effects.All(e => CardEffectExecutor.SupportsMovementMetadata(card, e)))
                throw new FormatException($"{card.id}: invalid movementStyle or rangeBasis");
            if (card.effects.Length == 0) throw new FormatException($"{card.id}: no effects");
            Unique(card.effects.Select(e => e.resolutionOrder.ToString()), card.id + " effect order");
            if (Regex.IsMatch(card.ResolvedDescription(), @"\{[^}]+\}")) throw new FormatException($"{card.id}: unresolved description placeholder");
        }
        var db = ScriptableObject.CreateInstance<CardDatabase>(); db.cards = cards; db.keywords = keywords; db.statuses = statuses;
        return db;
    }

    private static string Required(Dictionary<string, string> row, string key)
    {
        if (string.IsNullOrWhiteSpace(row[key])) throw new FormatException($"{row["id"]}: missing {key}");
        return row[key];
    }
    private static int Number(Dictionary<string, string> row, string key)
    {
        if (!int.TryParse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0)
            throw new FormatException($"{row["id"]}: invalid nonnegative integer {key}={row[key]}");
        return value;
    }
    private static bool Boolean(Dictionary<string, string> row, string key)
    {
        if (!bool.TryParse(row[key], out bool value)) throw new FormatException($"{row["id"]}: invalid boolean {key}");
        return value;
    }
    private static void Unique(IEnumerable<string> ids, string label)
    {
        var seen = new HashSet<string>();
        foreach (string id in ids) if (!seen.Add(id)) throw new FormatException($"Duplicate {label}: {id}");
    }
    private static Vector2Int[] Offsets(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<Vector2Int>();
        return value.Split(';').Select(part =>
        {
            var match = Regex.Match(part.Trim(), @"^\((-?\d+)\s*,\s*(-?\d+)\)$");
            if (!match.Success) throw new FormatException("Invalid rangeOffsets: " + value);
            return new Vector2Int(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
        }).ToArray();
    }

    private static List<Dictionary<string, string>> Table(string path, params string[] requiredHeaders)
    {
        var rows = ReadCsv(File.ReadAllText(path, Encoding.UTF8));
        if (rows.Count == 0) throw new FormatException(path + ": empty CSV");
        var headers = rows[0].Select(s => s.Trim().TrimStart('\uFEFF')).ToArray();
        Unique(headers, "header");
        foreach (string header in requiredHeaders) if (!headers.Contains(header)) throw new FormatException(path + ": missing column " + header);
        var result = new List<Dictionary<string, string>>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = new Dictionary<string, string>();
            for (int j = 0; j < headers.Length; j++) row[headers[j]] = j < rows[i].Count ? rows[i][j].Trim() : "";
            if (string.IsNullOrWhiteSpace(row["id"])) continue;
            result.Add(row);
        }
        return result;
    }

    // Quoted commas, escaped quotes and multiline descriptions are supported.
    public static List<List<string>> ReadCsv(string text)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (!quoted && c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (!quoted && (c == '\n' || c == '\r'))
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); rows.Add(row); row = new List<string>(); field.Clear();
            }
            else field.Append(c);
        }
        if (quoted) throw new FormatException("Unterminated CSV quote");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
