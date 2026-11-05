using System;
using System.Linq;
using DuelHero.Battle;
using DuelHero.Cards;
using DuelHero.Data;
using DuelHero.Map;
using DuelHero.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class EconomySetup
{
    public const string ShopScenePath = "Assets/00.Scenes/ShopScene.unity";
    public static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/BusanDandiM.ttf");
    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();
    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private static Text Label(string name, Transform parent, string text, Vector2 size, Vector2 position, int fontSize)
    {
        var rect = Rect(name, parent, size, position); var label = rect.gameObject.AddComponent<Text>();
        label.font = Font; label.fontSize = fontSize; label.text = text; label.alignment = TextAnchor.MiddleCenter; label.color = new Color(.96f, .87f, .65f); label.raycastTarget = false; return label;
    }
    private static void Edit(string path, Action<Scene> action)
    {
        var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("저장하지 않은 씬을 먼저 확인해주세요: " + path);
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { action(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    [MenuItem("Tools/Duel Hero/Economy/Set up gold and shop")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이를 중지해주세요.");
        GameSheetImporter.ImportFolder(EconomyConfigSheetImporter.SourceFolder, CardSheetImporter.DefaultSpreadsheetId, SheetSyncScope.Economy);
        Edit("Assets/00.Scenes/MapScene.unity", scene => {
            var controller = Find<RouteMapController>(scene);
            Set(controller, "runConfig", AssetDatabase.LoadAssetAtPath<RunConfig>(EconomyConfigSheetImporter.AssetPath("RunConfig")));
            Set(controller, "shopConfig", AssetDatabase.LoadAssetAtPath<ShopConfig>(EconomyConfigSheetImporter.AssetPath("ShopConfig")));
            var canvas = Find<Canvas>(scene);
            var existing = canvas.transform.Find("GoldLabel");
            var label = existing != null ? existing.GetComponent<Text>() : Label("GoldLabel", canvas.transform, "골드  30", new Vector2(250, 50), new Vector2(700, 460), 26);
            Set(controller, "goldLabel", label);
        });
        Edit("Assets/00.Scenes/BattleScene.unity", scene => {
            Set(Find<BattleRunController>(scene), "goldRewardConfig", AssetDatabase.LoadAssetAtPath<BattleRewardConfig>(EconomyConfigSheetImporter.AssetPath("BattleRewardConfig")));
            var view = Find<BattleResultUI>(scene); var so = new SerializedObject(view);
            var panel = (CanvasGroup)so.FindProperty("panel").objectReferenceValue;
            var existing = panel.transform.Find("GoldRewardLabel");
            var label = existing != null ? existing.GetComponent<Text>() : Label("GoldRewardLabel", panel.transform, "", new Vector2(650, 36), new Vector2(0, -177), 23);
            label.gameObject.SetActive(false); Set(view, "goldRewardLabel", label);
        });
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ShopScenePath) == null) BuildShop();
        ConfigureShopHover();
        var scenes = EditorBuildSettings.scenes.ToList();
        var shop = scenes.FirstOrDefault(s => s.path == ShopScenePath);
        if (shop == null) scenes.Add(new EditorBuildSettingsScene(ShopScenePath, true)); else shop.enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("ECONOMY_SETUP_PASS");
    }
    // Updates only the shop hover reference; leaves economy data and other scenes untouched.
    public static void ConfigureShopHover()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이를 중지해주세요.");
        Edit(ShopScenePath, scene => {
            var controller = Find<ShopController>(scene);
            var existing = controller.transform.Find("CardHoverTemplate");
            ButtonHoverScale template;
            if (existing != null) template = existing.GetComponent<ButtonHoverScale>();
            else
            {
                var go = new GameObject("CardHoverTemplate"); go.transform.SetParent(controller.transform, false);
                go.SetActive(false); template = go.AddComponent<ButtonHoverScale>();
            }
            Set(controller, "cardHoverTemplate", template);
        });
    }
    private static void BuildShop()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .03f, .055f); camera.transform.position = new Vector3(0, 0, -10);
            var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional;
            var canvas = new GameObject("ShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var backdrop = Rect("CounterBackdrop", canvas.transform, new Vector2(1500, 610), new Vector2(0, -35)).gameObject.AddComponent<Image>();
            backdrop.color = new Color(.075f, .065f, .095f, 1); backdrop.raycastTarget = false;
            Label("Title", canvas.transform, "떠돌이 상인의 가게", new Vector2(950, 85), new Vector2(0, 380), 46);
            var gold = Label("GoldLabel", canvas.transform, "골드  0", new Vector2(400, 50), new Vector2(640, 440), 28);
            var message = Label("Message", canvas.transform, "", new Vector2(1400, 55), new Vector2(0, 280), 24);
            var content = Rect("Offers", canvas.transform, new Vector2(1480, 440), new Vector2(0, -20));
            var layout = content.gameObject.AddComponent<GridLayoutGroup>(); layout.cellSize = new Vector2(300, 420); layout.spacing = new Vector2(40, 35);
            layout.childAlignment = TextAnchor.MiddleCenter; layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 4;
            // Leave enough room for larger future offer counts without placing controls over the cards.
            var exit = Rect("Exit", canvas.transform, new Vector2(300, 64), new Vector2(0, -430)); exit.gameObject.AddComponent<Image>().color = new Color(.25f, .17f, .11f);
            var button = exit.gameObject.AddComponent<Button>(); Label("Label", exit, "맵으로 돌아가기", new Vector2(290, 60), Vector2.zero, 26);
            var controller = canvas.gameObject.AddComponent<ShopController>();
            Set(controller, "database", AssetDatabase.LoadAssetAtPath<CardDatabase>(CardSheetImporter.DatabasePath));
            Set(controller, "cardPrefab", AssetDatabase.LoadAssetAtPath<CardView>("Assets/02.Prefabs/CardView.prefab"));
            Set(controller, "content", content); Set(controller, "goldLabel", gold); Set(controller, "message", message); Set(controller, "exitButton", button);
            EditorSceneManager.SaveScene(scene, ShopScenePath);
        }
        finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
    }
}
