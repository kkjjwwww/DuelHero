using System;
using System.Linq;
using DuelHero.Battle;
using DuelHero.Cards;
using DuelHero.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class CardRewardSetup
{
    internal const string DevelopmentDatabasePath = "Assets/Data/Development/RewardCardDatabase.asset";
    internal const string SettingsPath = "Assets/Data/CardRewardSettings.asset";

    [MenuItem("Tools/Duel Hero/Rewards/Set up development rewards")]
    public static void SetUp()
    {
        if (Application.isPlaying) throw new InvalidOperationException("플레이 모드를 종료해주세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("열린 씬의 변경을 먼저 저장해주세요.");
        var database = DevelopmentDatabase();
        var settings = AssetDatabase.LoadAssetAtPath<CardRewardSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<CardRewardSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        ConfigureScene("Assets/00.Scenes/MapScene.unity", scene =>
            SetReference(Find<RouteMapController>(scene), "cardDatabase", database));
        ConfigureScene("Assets/00.Scenes/BattleScene.unity", scene =>
        {
            SetReference(Find<PlayerDeck>(scene), "database", database);
            var result = Find<BattleResultUI>(scene);
            var serialized = new SerializedObject(result);
            var title = (Text)serialized.FindProperty("resultLabel").objectReferenceValue;
            var button = (Button)serialized.FindProperty("restartButton").objectReferenceValue;
            var panel = (RectTransform)title.transform.parent;
            var reward = result.GetComponent<CardRewardUI>();
            if (reward == null) reward = result.gameObject.AddComponent<CardRewardUI>();
            var content = panel.Find("RewardCards") as RectTransform;
            if (content == null)
            {
                content = new GameObject("RewardCards", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(panel, false);
                content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
                content.sizeDelta = new Vector2(820, 400);
                var message = new GameObject("Message", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                message.transform.SetParent(content, false);
                message.font = title.font;
                message.fontSize = 18;
                message.color = new Color(.94f, .89f, .77f);
                message.alignment = TextAnchor.MiddleCenter;
                message.raycastTarget = false;
                message.rectTransform.anchorMin = message.rectTransform.anchorMax = new Vector2(.5f, .5f);
                message.rectTransform.sizeDelta = new Vector2(800, 32);
                message.rectTransform.anchoredPosition = new Vector2(0, 182);
            }
            SetReference(reward, "resultPanel", panel);
            SetReference(reward, "resultTitle", title.rectTransform);
            SetReference(reward, "heading", panel.Find("Heading").gameObject);
            SetReference(reward, "actionButton", button.transform);
            SetReference(reward, "content", content);
            SetReference(reward, "message", content.GetComponentInChildren<Text>(true));
            SetReference(reward, "cardPrefab", AssetDatabase.LoadAssetAtPath<CardView>("Assets/02.Prefabs/CardView.prefab"));
            content.gameObject.SetActive(false);
            var controller = Find<BattleRunController>(scene);
            SetReference(controller, "rewardSettings", settings);
            SetReference(controller, "rewardView", reward);
        });
        AssetDatabase.SaveAssetIfDirty(settings);
        Debug.Log("카드 보상 연결 완료: 원본 카드 DB/시트는 유지하고 개발용 DB를 맵·전투에 연결했습니다.");
    }

    private static CardDatabase DevelopmentDatabase()
    {
        var existing = AssetDatabase.LoadAssetAtPath<CardDatabase>(DevelopmentDatabasePath);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder("Assets/Data/Development"))
            AssetDatabase.CreateFolder("Assets/Data", "Development");
        var source = AssetDatabase.LoadAssetAtPath<CardDatabase>(CardSheetImporter.DatabasePath);
        var database = ScriptableObject.CreateInstance<CardDatabase>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), database);
        // Clearly separate this snapshot from the sheet-import destination.
        database.sourceSpreadsheetId = string.Empty;
        database.importedAtUtc = string.Empty;
        string[] rarities = { "일반", "고급", "희귀", "전설" };
        string[] keys = { "common", "uncommon", "rare", "legendary" };
        var definitions = database.cards.ToList();
        for (int i = 0; i < rarities.Length; i++)
            foreach (string baseId in new[] { "slash_001", "thrust_001" })
            {
                var original = source.cards.Single(c => c.id == baseId);
                var copy = JsonUtility.FromJson<CardDefinition>(JsonUtility.ToJson(original));
                copy.id = "reward_test_" + keys[i] + "_" + baseId;
                copy.name = "테스트 " + original.name;
                copy.rarity = rarities[i];
                copy.isObtainable = true;
                copy.isStarterCard = false;
                foreach (var effect in copy.effects)
                {
                    effect.id = copy.id + "_" + effect.id;
                    effect.cardId = copy.id;
                }
                definitions.Add(copy);
            }
        database.cards = definitions.ToArray();
        AssetDatabase.CreateAsset(database, DevelopmentDatabasePath);
        return database;
    }

    private static T Find<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();

    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureScene(string path, Action<Scene> configure)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            configure(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
