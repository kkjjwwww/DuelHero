using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DuelHero.Battle;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelHero.EditorTools
{
    // Explicitly requested Editor-only integration test; never saves scene or run data.
    [InitializeOnLoad]
    public static class StunPlayModeValidation
    {
        private const string Active = "DuelHero.StunPlayTest.Active";
        private const string Stage = "DuelHero.StunPlayTest.Stage";
        private const string Request = "Temp/StunPlayTest.request";
        private const string Result = "Temp/StunPlayTest.result";
        private const string Folder = "Captures/Stun/PlayMode";
        private static double deadline, next;
        private static CardInstance head;
        private static int executionRound;
        private static bool wasBackground;
        private static float previousTimeScale;
        private static string capture;
        private static bool markerExecuted;
        private static object Field(object target,string name) => target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
        private static CardView PlayerSlot(int index) => ((CardView[])Field(UnityEngine.Object.FindFirstObjectByType<ActionQueueUI>(),"slotCards"))[index];
        private static EnemyActionSlotView EnemySlot(int index) => ((EnemyActionSlotView[])Field(UnityEngine.Object.FindFirstObjectByType<EnemyActionQueueUI>(),"slots"))[index];
        private static void TestStunHover()
        {
            var own=PlayerSlot(0);
            own.GetComponent<CardHoverInfo>().ShowInfo();
            var inspection=UnityEngine.Object.FindFirstObjectByType<CardInspectionPresenter>();
            Check(inspection!=null && inspection.IsVisible && inspection.Owner==own.transform &&
                inspection.View.GetComponentInChildren<StunCardCracks>(true)!=null,"player stun hover enlarges dedicated broken card");
            inspection.Hide();
            EnemySlot(0).OnPointerEnter(null);
            Check(inspection.IsVisible && inspection.View.GetComponentInChildren<StunCardCracks>(true)!=null,"enemy stun hover enlarges dedicated broken card");
            inspection.Hide();
        }
        private static void TestResultKeyword(PlayerDeck deck)
        {
            var view=PlayerSlot(2);
            var keyword=deck.Database.keywords.Single(k=>k.id=="stun");
            typeof(CardView).GetMethod("NotifyKeywordEnter",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,new object[]{keyword,new Vector2(960,540)});
            Check(UnityEngine.Object.FindObjectsByType<KeywordTooltip>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Any(t=>t.gameObject.activeInHierarchy),"player reserved-card keyword tooltip works while awaiting next turn");
            foreach(var tooltip in UnityEngine.Object.FindObjectsByType<KeywordTooltip>(FindObjectsInactive.Include,FindObjectsSortMode.None)) tooltip.Hide();
            var inspection=CardInspectionPresenter.GetOrCreate(view.GetComponentInParent<Canvas>().rootCanvas);
            // Switch prefab kinds in both directions; cached inspection must retain the correct look.
            EnemySlot(0).OnPointerEnter(null);
            Check(inspection.IsVisible && inspection.View.GetComponentInChildren<StunCardCracks>(true)!=null,"enemy stun inspection works in result phase");
            inspection.Hide();
            var enemy=EnemySlot(0);
            enemy.Bind(deck.Database.cards.Single(c=>c.id=="head_break_001"));
            enemy.Reveal();
            enemy.SetResultInspection(true,null);
            enemy.OnPointerEnter(null);
            Check(inspection.IsVisible && inspection.View.GetComponentInChildren<StunCardCracks>(true)==null,"normal inspection does not inherit stun cracks");
            typeof(CardView).GetMethod("NotifyKeywordEnter",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(inspection.View,new object[]{keyword,new Vector2(960,540)});
            Check(UnityEngine.Object.FindObjectsByType<KeywordTooltip>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Any(t=>t.gameObject.activeInHierarchy),"enemy inspection keyword tooltip works in result phase");
            inspection.Hide(); enemy.ShowStun();
        }
        static StunPlayModeValidation()
        {
            EditorApplication.update += Tick;
            if (File.Exists(Request) && !SessionState.GetBool(Active,false))
                EditorApplication.delayCall += Begin;
        }
        [MenuItem("Duel Hero/Debug/Validate Stun and Capture")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { File.WriteAllText(Result,"BLOCKED: Stop play mode before integration validation."); return; }
            if (File.Exists(Request)) File.Delete(Request);
            StunSetup.BuildPrefab();
            StunValidation.Run();
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Result,"RUNNING\n");
            SessionState.SetBool(Active,true); SessionState.SetInt(Stage,1);
            // Stage 1 resumes after the play-mode domain reload.
            EditorApplication.EnterPlaymode();
        }
        private static void Check(bool condition,string message)
        { if (!condition) throw new InvalidOperationException(message); File.AppendAllText(Result,"PASS: "+message+"\n"); }
        private static void Move(UnitStats unit,Vector2Int cell)
        {
            var movement=unit.GetComponent<GridMovement>();
            typeof(GridMovement).GetField("gridPosition",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(movement,cell);
            typeof(GridMovement).GetMethod("UpdateWorldPosition",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(movement,null);
        }
        private static void Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            capture=Path.GetFullPath(Folder+"/"+name+".png");
            ScreenCapture.CaptureScreenshot(capture);
            next=EditorApplication.timeSinceStartup+1;
        }
        private static bool CaptureReady() => EditorApplication.timeSinceStartup>=next && File.Exists(capture) && new FileInfo(capture).Length>0;
        private static void Tick()
        {
            if (!SessionState.GetBool(Active,false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlaying) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (deadline==0) deadline=EditorApplication.timeSinceStartup+180;
            try
            {
                if (EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Stun play-mode validation timed out at stage "+SessionState.GetInt(Stage,0));
                int stage=SessionState.GetInt(Stage,0);
                if (stage==1)
                {
                    wasBackground=Application.runInBackground; previousTimeScale=Time.timeScale;
                    Application.runInBackground=true;
                    EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
                    EditorSceneManager.LoadSceneInPlayMode("Assets/00.Scenes/BattleScene.unity",new LoadSceneParameters(LoadSceneMode.Single));
                    SessionState.SetInt(Stage,2); return;
                }
                var turn=UnityEngine.Object.FindFirstObjectByType<BattleTurnController>();
                var queue=UnityEngine.Object.FindFirstObjectByType<CardReservationQueue>();
                var deck=UnityEngine.Object.FindFirstObjectByType<PlayerDeck>();
                if (turn==null || queue?.UnitStats==null || !queue.UnitStats.IsInitialized || turn.PreparedEnemyCards.Count!=3 || deck==null) return;
                var player=queue.UnitStats;
                var enemy=UnityEngine.Object.FindObjectsByType<UnitStats>(FindObjectsSortMode.None).Single(u=>u!=player && u.IsInitialized);
                if (stage==2)
                {
                    turn.ActionResolved += action => { if (action.Kind == BattleActionKind.CardExecuted && action.CardId == StunCard.Definition.id) markerExecuted = true; };
                    int count=deck.Cards.Count;
                    // Same generic deck API used by the existing selected-card add button.
                    head=deck.AddCard("head_break_001");
                    Check(deck.Cards.Count==count+1 && ReferenceEquals(deck.Cards.Last(),head),"generic card add creates one independent Head Break copy");
                    for (int i=0;i<3;i++) Check(queue.TryReserve(deck.WaitAction),"reserve wait slot "+(i+1));
                    var second=queue.Reservations[1]; var third=queue.Reservations[2];
                    Check(turn.TryApplyStunForDebug(player,1),"debug applies player stun");
                    Check(StunCard.IsStun(queue.Reservations[0].Definition) && ReferenceEquals(second,queue.Reservations[1]) && ReferenceEquals(third,queue.Reservations[2]),"reserved first action canceled without shifting later slots");
                    Check(!queue.CancelAt(0) && !queue.TrySetDirection(0,Vector2Int.up),"blocked player slot rejects cancel and direction input");
                    Check(turn.TryApplyStunForDebug(enemy,1) && StunCard.IsStun(turn.PreparedEnemyCards[0]),"debug immediately replaces enemy first slot");
                    Check(turn.CanInspectSlots,"preparation permits slot inspection");
                    TestStunHover();
                    Capture("01-both-sides-stunned"); SessionState.SetInt(Stage,3); return;
                }
                if (stage==3 && CaptureReady())
                {
                    Check(turn.TryExecute(),"execute turn with blocked first slots");
                    int count=deck.Cards.Count; int stun=player.Stun;
                    Check(!turn.CanEditForDebug && !turn.TryApplyStunForDebug(player,1) && deck.Cards.Count==count && player.Stun==stun,"stun debug actions rejected during execution");
                    Check(!turn.CanInspectSlots,"execution blocks slot inspection");
                    Time.timeScale=5; SessionState.SetInt(Stage,4); return;
                }
                if (stage==4 && turn.ResultsAvailable)
                {
                    Check(player.Stun==0 && player.Energy==player.MaxEnergy,"blocked wait slot consumes one stun and no energy");
                    Check(!turn.CanEditForDebug && !turn.TryApplyStunForDebug(player,1),"debug edits rejected during result display");
                    Check(turn.TryAdvanceTurn(),"advance after first test turn"); SessionState.SetInt(Stage,5); return;
                }
                if (stage==5 && turn.CanEditForDebug && !queue.IsExecuting && queue.CurrentRound==2)
                {
                    // Hold the enemy still for a real third-slot Head Break hit.
                    Check(turn.TryApplyStunForDebug(enemy,3),"disable enemy movement for controlled hit test");
                    var origin=player.GetComponent<GridMovement>().GridPosition;
                    var grid=player.GetComponent<GridMovement>().Grid;
                    var cell=grid.CanEnter(origin+Vector2Int.right)?origin+Vector2Int.right:origin;
                    Move(enemy,cell);
                    Check(queue.TryReserve(deck.WaitAction) && queue.TryReserve(deck.WaitAction) && queue.TryReserve(head),"reserve Head Break in third slot");
                    executionRound=queue.CurrentRound;
                    var list=UnityEngine.Object.FindFirstObjectByType<DeckListUI>();
                    if (list.SelectedCategory!="공격") list.ToggleCategory("공격");
                    Time.timeScale=1; Capture("02-head-break-third-slot"); SessionState.SetInt(Stage,6); return;
                }
                if (stage==6 && CaptureReady())
                { Check(turn.TryExecute(),"execute real Head Break card"); Time.timeScale=5; SessionState.SetInt(Stage,7); return; }
                if (stage==7 && turn.ResultsAvailable)
                {
                    Check(enemy.Stun==1,"third-slot Head Break leaves one stun for next turn");
                    Check(player.Energy==player.MaxEnergy-20,"real Head Break costs exactly 20 energy");
                    Check(turn.CanInspectSlots && turn.IsInputLocked,"result phase permits information without unlocking actions");
                    TestResultKeyword(deck);
                    Check(!queue.CancelAt(2) && !queue.TrySetDirection(2,Vector2Int.left),"result inspection cannot edit reservations");
                    Check(turn.TryAdvanceTurn(),"advance to carryover turn");
                    Check(!turn.CanInspectSlots,"turn restoration blocks inspection"); SessionState.SetInt(Stage,8); return;
                }
                if (stage==8 && turn.CanEditForDebug && !queue.IsExecuting && queue.CurrentRound==3)
                {
                    Check(enemy.Stun==1 && StunCard.IsStun(turn.PreparedEnemyCards[0]),"enemy next-turn first slot blocked by real attack");
                    Check(head.AvailableFromRound==executionRound+3 && head.RemainingCooldown(queue.CurrentRound)==2,"Head Break has two-turn cooldown");
                    Check(turn.TryApplyStunForDebug(player,1) && turn.TryApplyStunForDebug(player,1) && player.Stun==2,"repeated debug stun stacks additively");
                    Check(queue.Reservations.Count==2 && queue.Reservations.All(c=>StunCard.IsStun(c.Definition)),"player first two slots blocked");
                    Check(queue.TryReserve(deck.WaitAction) && queue.Reservations[2].ReservedSlot==2,"player can reserve from third slot after stun two");
                    Time.timeScale=1; Capture("03-carryover-and-stun-two"); SessionState.SetInt(Stage,9); return;
                }
                if (stage==9 && CaptureReady())
                {
                    Check(!markerExecuted,"disabled slots never emit a card-executed action");
                    Check(turn.TryWinForDebug() && !turn.CanInspectSlots,"victory popup blocks slot inspection");
                    File.AppendAllText(Result,"PASS: gameplay screenshots captured\nALL_PASS\n");
                    Finish();
                }
            }
            catch (Exception error)
            {
                File.AppendAllText(Result,"FAIL: "+error+"\n"); Debug.LogException(error); Finish();
            }
        }
        private static void Finish()
        {
            Application.runInBackground=wasBackground; Time.timeScale=previousTimeScale;
            SessionState.SetBool(Active,false); SessionState.EraseInt(Stage);
            EditorApplication.ExitPlaymode();
        }
    }
}
