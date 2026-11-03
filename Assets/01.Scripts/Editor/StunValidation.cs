using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DuelHero.Battle;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEditor;
using UnityEngine;

public static class StunValidation
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("Stun validation: " + message); }
    internal sealed class Fixture : IDisposable
    {
        public readonly GameObject Root = new GameObject("StunValidation");
        public UnitStats Player, Enemy;
        public CardReservationQueue Queue;
        public PlayerDeck Deck;
        public CombatEffectResolver Resolver;
        public BattleTurnController Turn;
        public CardDatabase Db;
        public Fixture()
        {
            Db = AssetDatabase.LoadAssetAtPath<CardDatabase>(CardSheetImporter.DatabasePath);
            GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(Root.transform); return go; }
            var grid = Child("Grid").AddComponent<BattleGrid>();
            Set(grid, "map", new GridMap(new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2 }, Array.Empty<Vector2Int>()));
            Set(grid, "initialized", true);
            var board = Child("Board").AddComponent<BoardOccupancy>(); Set(board, "grid", grid);
            var p = Child("Player"); var e = Child("Enemy");
            Player = p.AddComponent<UnitStats>(); Enemy = e.AddComponent<UnitStats>();
            Player.Initialize(100, 100, 100, 100); Enemy.Initialize(100, 100, 100, 100);
            var pm = p.AddComponent<GridMovement>(); var em = e.AddComponent<GridMovement>();
            board.RegisterUnit(pm); board.RegisterUnit(em); Set(em, "gridPosition", Vector2Int.right);
            Deck = p.AddComponent<PlayerDeck>(); Set(Deck, "database", Db); Deck.Initialize();
            Queue = p.AddComponent<CardReservationQueue>(); Set(Queue, "unitStats", Player); Set(Queue, "playerDeck", Deck);
            typeof(CardReservationQueue).GetMethod("OnEnable", Private).Invoke(Queue, null);
            Resolver = Child("Resolver").AddComponent<CombatEffectResolver>();
            var binding = typeof(CombatEffectResolver).GetNestedType("UnitBinding", BindingFlags.NonPublic);
            var units = Array.CreateInstance(binding, 2);
            for (int i = 0; i < 2; i++)
            {
                var value = Activator.CreateInstance(binding);
                binding.GetField("id").SetValue(value, i == 0 ? "p" : "e");
                binding.GetField("side").SetValue(value, i == 0 ? UnitDataKind.Player : UnitDataKind.Enemy);
                binding.GetField("movement").SetValue(value, i == 0 ? pm : em);
                binding.GetField("stats").SetValue(value, i == 0 ? Player : Enemy);
                units.SetValue(value, i);
            }
            Set(Resolver, "units", units);
            var executor = Resolver.gameObject.AddComponent<CardEffectExecutor>(); Set(executor, "combatEffects", Resolver);
            Turn = Child("Turn").AddComponent<BattleTurnController>();
            Set(Turn, "playerQueue", Queue); Set(Turn, "playerId", "p"); Set(Turn, "enemyId", "e");
            Set(Turn, "effects", executor); Set(Turn, "combatEffects", Resolver); Set(Turn, "executionRound", 1);
        }
        public CardDefinition Card(string id, bool enemy = false) => CardDirectionRules.ForExecution(Db.cards.Single(c => c.id == id), enemy ? Vector2Int.left : Vector2Int.right);
        public void Execute(CardDefinition[] enemy)
        {
            Check(Queue.BeginExecution(), "begin execution");
            var player = Queue.Reservations.Select(c => c.Definition).ToArray();
            Set(Turn, "preparedEnemyCards", enemy);
            var routine = (IEnumerator)typeof(BattleTurnController).GetMethod("ExecuteSlots", Private).Invoke(Turn, new object[] { player, enemy });
            while (routine.MoveNext()) { }
        }
        public void Dispose() { Turn.enabled = false; UnityEngine.Object.DestroyImmediate(Root); }
    }
    [MenuItem("Tools/Duel Hero/Cards/Validate stun")]
    public static void Run()
    {
        using (var f = new Fixture())
        {
            var card = f.Card("head_break_001");
            Check(card.cooldownTurns == 2 && card.energyCost == 20 && CardEffectExecutor.SupportsDefinition(card), "card and direction snapshot");
            f.Enemy.ApplyGuard(100, 1);
            f.Resolver.ApplyDamageBatch(f.Resolver.CalculateDamageBatch(new[] { new CombatEffectResolver.AttackRequest("p", card, card.effects[0]) }, 1));
            Check(f.Enemy.Health == 100 && f.Enemy.Stun == 1, "zero HP damage still stuns");
            f.Enemy.ApplyStun(2); Check(f.Enemy.Stun == 3, "additive stacks");
            Check(f.Enemy.ConsumeStunSlot() && f.Enemy.Stun == 2, "one stack per disabled slot");
            var miss = CardDirectionRules.ForExecution(f.Db.cards.Single(c => c.id == "head_break_001"), Vector2Int.up);
            f.Resolver.ApplyDamageBatch(f.Resolver.CalculateDamageBatch(new[] { new CombatEffectResolver.AttackRequest("p", miss, miss.effects[0]) }, 1));
            Check(f.Enemy.Stun == 2, "miss must not stun");
        }
        using (var f = new Fixture())
        {
            var owned = Enumerable.Range(0, 3).Select(_ => f.Deck.AddCard("slash_001")).ToArray();
            foreach (var c in owned) Check(f.Queue.TryReserve(c), "reserve attack");
            f.Execute(new[] { f.Card("head_break_001", true), WaitCard.Definition, WaitCard.Definition });
            Check(f.Player.Health == 97 && f.Enemy.Health == 80, "current slot executes; next slot canceled; third stays in place");
            Check(f.Player.Energy == 80 && StunCard.IsStun(f.Queue.Reservations[1].Definition), "canceled action costs zero and has marker");
            f.Queue.CompleteExecution();
            Check(owned[1].AvailableFromRound == 1 && owned[1].ReservedSlot == -1 && owned[0].AvailableFromRound == 3, "only executed cards cool down");
        }
        using (var f = new Fixture())
        {
            for (int i = 0; i < 3; i++) f.Queue.TryReserve(f.Deck.WaitAction);
            f.Execute(new[] { WaitCard.Definition, WaitCard.Definition, f.Card("head_break_001", true) });
            Check(f.Player.Stun == 1, "third-slot stun carries over"); f.Queue.CompleteExecution();
            Check(f.Queue.Reservations.Count == 1 && StunCard.IsStun(f.Queue.Reservations[0].Definition), "next turn starts with blocked first slot");
            Check(!f.Queue.CancelAt(0) && !f.Queue.TrySetDirection(0, Vector2Int.up), "marker cannot be changed");
            var card = f.Deck.AddCard("slash_001"); Check(f.Queue.TryReserve(card) && card.ReservedSlot == 1, "reserve from second slot");
            f.Queue.TryReserve(f.Deck.WaitAction);
            var preview = CardPreviewCalculator.CalculateQueue(new CardPreviewState(Vector2Int.zero, 100, 100,
                new GridMap(new[] { Vector2Int.zero, Vector2Int.right }, Array.Empty<Vector2Int>())), f.Queue.Reservations);
            Check(preview[0].EnergyCost == 0 && preview[0].AttackCells.Count == 0 && preview[1].EnergyAfter == 90, "preview skips stun marker");
        }
        using (var f = new Fixture())
        {
            f.Queue.TryReserve(f.Deck.AddCard("head_break_001"));
            f.Queue.TryReserve(f.Deck.AddCard("slash_001")); f.Queue.TryReserve(f.Deck.AddCard("slash_001"));
            f.Execute(new[] { f.Card("head_break_001",true),f.Card("slash_001",true),WaitCard.Definition });
            Check(f.Player.Health == 97 && f.Enemy.Health == 87 && f.Player.Energy == 70, "simultaneous stun preserves both current attacks and cancels both next attacks");
        }
        using (var f = new Fixture())
        {
            var owned = Enumerable.Range(0,3).Select(_ => f.Deck.AddCard("slash_001")).ToArray();
            foreach (var c in owned) f.Queue.TryReserve(c);
            var stun2 = JsonUtility.FromJson<CardDefinition>(JsonUtility.ToJson(f.Card("head_break_001",true)));
            stun2.effects[1].value = 2;
            f.Execute(new[] { stun2,WaitCard.Definition,WaitCard.Definition });
            Check(f.Enemy.Health == 90 && f.Player.Energy == 90 && f.Player.Stun == 0, "stun two cancels the next two actions");
            f.Queue.CompleteExecution(); Check(owned[1].AvailableFromRound == 1 && owned[2].AvailableFromRound == 1, "both canceled copies remain available");
        }
        using (var f = new Fixture())
        {
            f.Player.ApplyStun(4); Check(f.Queue.Reservations.Count == 3, "all three slots blocked");
            f.Execute(new[] { WaitCard.Definition, WaitCard.Definition, WaitCard.Definition });
            Check(f.Player.Stun == 1 && f.Player.Energy == 100, "all-stun turn consumes three slots without cost");
            f.Queue.CompleteExecution(); Check(f.Queue.Reservations.Count == 1, "remaining fourth stack carries");
        }
        using (var f = new Fixture())
        {
            var map = new GridMap(new[] { Vector2Int.zero, Vector2Int.right }, Array.Empty<Vector2Int>());
            var actor = new EnemyUnitSnapshot(100,100,100,100,Array.Empty<GuardEffectSnapshot>(),2);
            var context = new EnemyDecisionContext(f.Deck.Cards, new EnemyPlanningState(Vector2Int.zero,100,100,map),Vector2Int.right,1,
                actor,new EnemyUnitSnapshot(100,100,100,100,Array.Empty<GuardEffectSnapshot>()));
            var plan = new UtilityPlanSelector(new UtilityAiSettings()).Select(context);
            Check(StunCard.IsStun(plan[0].Card.Definition) && StunCard.IsStun(plan[1].Card.Definition), "AI skips disabled slots");
            EnemyPlanningState.SimulatePlan(context, plan);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<CardView>(StunSetup.PrefabPath);
        Check(prefab != null && prefab.GetComponentInChildren<StunCardCracks>(true) != null, "dedicated broken prefab");
        var badge = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "EnergyBadge");
        Check(!badge.gameObject.activeSelf, "cost badge hidden");
        using (var f = new Fixture())
        {
            var canvasObject = new GameObject("QueueCanvas",typeof(Canvas)); canvasObject.transform.SetParent(f.Root.transform);
            var uiObject = new GameObject("QueueUI"); uiObject.transform.SetParent(canvasObject.transform);
            var ui = uiObject.AddComponent<ActionQueueUI>();
            var images = new UnityEngine.UI.Image[3];
            for (int i=0;i<3;i++)
            {
                var slot = new GameObject("Slot"+i,typeof(RectTransform),typeof(UnityEngine.UI.Image));
                slot.transform.SetParent(uiObject.transform);
                ((RectTransform)slot.transform).sizeDelta = new Vector2(132,184.38f);
                images[i]=slot.GetComponent<UnityEngine.UI.Image>();
            }
            Set(ui,"slotImages",images); Set(ui,"reservationQueue",f.Queue); Set(ui,"cardDatabase",f.Db);
            Set(ui,"cardPrefab",AssetDatabase.LoadAssetAtPath<CardView>("Assets/02.Prefabs/CardView.prefab"));
            typeof(ActionQueueUI).GetMethod("OnEnable",Private).Invoke(ui,null);
            f.Player.ApplyStun(1);
            var shown = images[0].GetComponentInChildren<CardView>(true);
            Check(shown != null && shown.name == "StunnedCard" && !shown.Button.interactable &&
                shown.GetComponentInChildren<StunCardCracks>(true) != null,"live queue instantiates dedicated disabled prefab");
        }
        RenderPreview(prefab);
        Debug.Log("STUN_VALIDATION_PASS: zero damage, miss, stacking, same-slot execution, cancellation, cost/cooldown, carryover, all-slot stun, preview, AI, prefab");
    }
    private static void RenderPreview(CardView prefab)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("StunPreview");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var texture = new RenderTexture(504,704,24);
        var pixels = new Texture2D(504,704,TextureFormat.RGBA32,false);
        var previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("Camera",typeof(Camera)); cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.GetComponent<Camera>(); camera.transform.localPosition = new Vector3(0,0,-10);
            camera.orthographic = true; camera.orthographicSize = 100; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.09f,.09f,.11f,1); camera.targetTexture = texture;
            camera.scene = scene;
            var canvasObject = new GameObject("Canvas",typeof(Canvas)); canvasObject.transform.SetParent(root.transform);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            var card = UnityEngine.Object.Instantiate(prefab,canvas.transform,false);
            card.transform.localPosition = Vector3.zero; card.transform.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,504,704),0,0); pixels.Apply();
            System.IO.Directory.CreateDirectory("Captures/Stun");
            System.IO.File.WriteAllBytes("Captures/Stun/stun-card.png",pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; texture.Release();
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(texture);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
