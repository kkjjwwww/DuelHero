using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using DuelHero.Battle;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEditor;
using UnityEngine;

public static class ChargeSlashValidation
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int checks;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("Charge: " + message); checks++; }
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static GridMap Map(params Vector2Int[] obstacles) => new GridMap(
        Enumerable.Range(0,25).Select(i => new Vector2Int(i%5,i/5)), obstacles);
    private static void Drain(IEnumerator routine)
    {
        try { while (routine.MoveNext()) if (routine.Current is IEnumerator nested) Drain(nested); }
        finally { (routine as IDisposable)?.Dispose(); }
    }
    private static void Execute(StunValidation.Fixture f, CardDefinition player, CardDefinition enemy)
    {
        var instance = f.Deck.AddCard(player.id);
        Check(f.Queue.TryReserve(instance), "reserve");
        while(f.Queue.Reservations.Count<3) Check(f.Queue.TryReserve(f.Deck.WaitAction), "wait reserve");
        Check(f.Queue.BeginExecution(), "begin");
        var enemies = new[]{enemy,WaitCard.Definition,WaitCard.Definition};
        Set(f.Turn,"preparedEnemyCards",enemies);
        var routine=(IEnumerator)typeof(BattleTurnController).GetMethod("ExecuteSlots",Private).Invoke(f.Turn,new object[]{new[]{player,WaitCard.Definition,WaitCard.Definition},enemies});
        Drain(routine);
        Check(f.Player.Energy==75, "cost paid once");
        f.Queue.CompleteExecution();
        Check(instance.RemainingCooldown(2)==2, "cooldown applied once");
    }
    [MenuItem("Tools/Duel Hero/Cards/Validate charge slash")]
    public static string Run()
    {
        checks=0;
        var db=AssetDatabase.LoadAssetAtPath<CardDatabase>(CardSheetImporter.DatabasePath);
        var charge=db.cards.Single(c=>c.id=="charge_slash_001");
        Check(charge.energyCost==25&&charge.cooldownTurns==2&&charge.rarity=="일반"&&charge.isObtainable&&!charge.isStarterCard,"card values");
        Check(CardEffectExecutor.SupportsDefinition(charge),"supported");
        var start=new Vector2Int(2,2);
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down})
        foreach(int blockedAt in new[]{0,1,2})
        {
            var map=blockedAt==0?Map():Map(start+dir*blockedAt);
            var preview=CardPreviewCalculator.Calculate(new CardPreviewState(start,100,100,map),new[]{new CardPreviewRequest(charge,dir)})[0];
            int steps=blockedAt==0?2:blockedAt-1;
            Check(preview.EndPosition==start+dir*steps&&preview.MovementPath.Count==steps+1,"preview movement");
            Check(new HashSet<Vector2Int>(preview.AttackCells).SetEquals(preview.MovementPath),"preview exact path");
            using(var f=new StunValidation.Fixture())
            {
                var pm=f.Player.GetComponent<GridMovement>();Set(pm.Grid,"map",map);Set(pm,"gridPosition",start);
                var card=CardDirectionRules.ForExecution(charge,dir);var path=new List<Vector2Int>();
                f.Resolver.GetComponent<CardEffectExecutor>().ExecutePreparation("p",card,1,movementPath:path);
                Check(path.SequenceEqual(preview.MovementPath)&&pm.GridPosition==preview.EndPosition,"execution matches preview");
                var attack=new CombatEffectResolver.AttackRequest("p",card,card.effects[1],path);
                Set(f.Enemy.GetComponent<GridMovement>(),"gridPosition",start);
                f.Resolver.ApplyDamageBatch(f.Resolver.CalculateDamageBatch(new[]{attack},1));
                Check(f.Enemy.Health==85,"origin hit once");
            }
        }
        var boundary=CardPreviewCalculator.Calculate(new CardPreviewState(new Vector2Int(4,2),100,100,Map()),new[]{new CardPreviewRequest(charge,Vector2Int.right)})[0];
        Check(boundary.AttackCells.Count==1,"boundary retains origin only");
        using(var f=new StunValidation.Fixture())
        {
            var pm=f.Player.GetComponent<GridMovement>();Set(pm.Grid,"map",Map());
            var path=new List<Vector2Int>();f.Resolver.GetComponent<CardEffectExecutor>().ExecutePreparation("p",charge,1,movementPath:path);
            var target=f.Enemy.GetComponent<GridMovement>();Set(target,"gridPosition",new Vector2Int(1,1));
            var attack=new CombatEffectResolver.AttackRequest("p",charge,charge.effects[1],path);
            f.Resolver.ApplyDamageBatch(f.Resolver.CalculateDamageBatch(new[]{attack},1));Check(f.Enemy.Health==100,"target leaves path before damage");
            Set(target,"gridPosition",Vector2Int.right);path.Add(Vector2Int.right);
            f.Resolver.ApplyDamageBatch(f.Resolver.CalculateDamageBatch(new[]{attack},1));Check(f.Enemy.Health==85,"target enters path, duplicate path cell does not duplicate hit");
        }
        using(var f=new StunValidation.Fixture())
        {
            Set(f.Enemy.GetComponent<GridMovement>(),"gridPosition",Vector2Int.right*2);
            Execute(f,charge,CardDirectionRules.ForExecution(charge,Vector2Int.left));
            Check(f.Player.Health==85&&f.Enemy.Health==85,"both dash damage simultaneous after movement");
        }
        using(var f=new StunValidation.Fixture())
        {
            Set(f.Enemy.GetComponent<GridMovement>(),"gridPosition",Vector2Int.right*2);
            Execute(f,charge,f.Card("slash_001",true));
            Check(f.Player.Health==90&&f.Enemy.Health==85,"dash with ordinary attack");
        }
        using(var f=new StunValidation.Fixture())
        {
            Set(f.Enemy.GetComponent<GridMovement>(),"gridPosition",Vector2Int.right*2);
            Execute(f,charge,f.Card("riposte_stance_001",true));
            Check(f.Enemy.Health==95&&f.Player.Health==92,"shield and counter preserved");
        }
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down})
        {
            var owned=new CardInstance(charge);var wait=new CardInstance(WaitCard.Definition);var target=start+dir;
            var context=new EnemyDecisionContext(new[]{owned},new EnemyPlanningState(start,100,100,Map()),target,1,
                new EnemyUnitSnapshot(100,100,100,100,Array.Empty<GuardEffectSnapshot>()),new EnemyUnitSnapshot(100,100,100,100,Array.Empty<GuardEffectSnapshot>()));
            var plan=new[]{new EnemyPlannedAction(owned,dir),new EnemyPlannedAction(wait,dir),new EnemyPlannedAction(wait,dir)};
            var sim=EnemyPlanningState.SimulatePlan(context,plan);
            Check(sim.TotalEffectiveDamage==15&&sim.Slots[0].PositionAfter==start+dir*2,"AI path damage and destination");
            var selected=new UtilityPlanSelector(new UtilityAiSettings()).Select(context);
            Check(selected.Any(a=>a.Card.Definition.id==charge.id),"AI recognizes path attack opportunity");
        }
        using(var f=new StunValidation.Fixture())
        {
            var instance=f.Deck.AddCard(charge.id);f.Queue.TryReserve(instance);
            while(f.Queue.Reservations.Count<3)f.Queue.TryReserve(f.Deck.WaitAction);
            f.Player.ApplyStun(1);f.Queue.BeginExecution();
            var enemies=new[]{WaitCard.Definition,WaitCard.Definition,WaitCard.Definition};Set(f.Turn,"preparedEnemyCards",enemies);
            Drain((IEnumerator)typeof(BattleTurnController).GetMethod("ExecuteSlots",Private).Invoke(f.Turn,new object[]{f.Queue.Reservations.Select(c=>c.Definition).ToArray(),enemies}));
            Check(f.Player.Energy==100&&f.Player.GetComponent<GridMovement>().GridPosition==Vector2Int.zero&&f.Enemy.Health==100,"stun cancels dash and its damage/cost");
        }
        using(var f=new StunValidation.Fixture())
        {
            var extra=new GameObject("SecondEnemy",typeof(UnitStats),typeof(GridMovement));extra.transform.SetParent(f.Root.transform);
            var stats=extra.GetComponent<UnitStats>();stats.Initialize(100,100,100,100);Set(extra.GetComponent<GridMovement>(),"gridPosition",Vector2Int.right);
            var field=typeof(CombatEffectResolver).GetField("units",Private);var old=(Array)field.GetValue(f.Resolver);var type=old.GetType().GetElementType();var units=Array.CreateInstance(type,old.Length+1);Array.Copy(old,units,old.Length);
            var binding=Activator.CreateInstance(type);type.GetField("id").SetValue(binding,"e2");type.GetField("side").SetValue(binding,UnitDataKind.Enemy);type.GetField("movement").SetValue(binding,extra.GetComponent<GridMovement>());type.GetField("stats").SetValue(binding,stats);units.SetValue(binding,old.Length);field.SetValue(f.Resolver,units);
            var path=new List<Vector2Int>();f.Resolver.GetComponent<CardEffectExecutor>().ExecutePreparation("p",charge,1,movementPath:path);
            f.Resolver.ApplyDamageBatch(f.Resolver.CalculateDamageBatch(new[]{new CombatEffectResolver.AttackRequest("p",charge,charge.effects[1],path)},1));
            Check(stats.Health==85&&f.Enemy.Health==85,"multiple enemies sharing path cell");
        }
        Animation();
        return "PASS "+checks+" charge slash checks";
    }
    private static void Animation()
    {
        var root=new GameObject("ChargeAnimationCheck");root.SetActive(false);
        try
        {
            var motion=new GameObject("Motion");motion.transform.SetParent(root.transform,false);motion.transform.localPosition=new Vector3(.2f,.5f,0);
            var renderer=motion.AddComponent<SpriteRenderer>();var sprites=root.AddComponent<UnitSpritePresenter>();
            Set(sprites,"targetRenderer",renderer);
            foreach(var pair in new[]{("idle","idle"),("attackBefore","attack_before"),("attackAfter","attack_after")})
                Set(sprites,pair.Item1,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/PlayerHooded/"+pair.Item2+".png"));
            var attack=root.AddComponent<UnitAttackPresentation>();Set(attack,"sprites",sprites);Set(attack,"motionRoot",motion.transform);
            root.SetActive(true);var rest=motion.transform.localPosition;
            var routine=attack.PlayDash(Vector3.right*2);Check(routine.MoveNext(),"dash starts");
            Check(sprites.CurrentPose==UnitPose.AttackBefore&&Mathf.Abs(motion.transform.localPosition.x-(rest.x-2))<.001f,"rewind and attack-before pose");
            var tween=(Sequence)typeof(UnitAttackPresentation).GetField("motion",Private).GetValue(attack);tween.SetUpdate(UpdateType.Manual);
            DOTween.ManualUpdate(.1f,.1f);
            Check(Mathf.Abs(motion.transform.localPosition.x-(rest.x-1))<.01f&&Mathf.Abs(motion.transform.localPosition.y-rest.y)<.001f,"halfway linear movement with no jump");
            DOTween.ManualUpdate(.11f,.11f);Check(!routine.MoveNext(),"dash completes");
            Check(sprites.CurrentPose==UnitPose.AttackAfter&&attack.IsPlaying&&motion.transform.localPosition==rest,"attack-after pose held at destination");
            attack.Cancel();Check(sprites.CurrentPose==UnitPose.Idle&&!attack.IsPlaying,"damage-completion reset");
            routine=attack.PlayDash(Vector3.right*2);routine.MoveNext();attack.Cancel();routine.MoveNext();
            DOTween.ManualUpdate(1,1);Check(motion.transform.localPosition==rest&&sprites.CurrentPose==UnitPose.Idle,"cancel restores motion and no late tween writes");
            routine=attack.PlayDash(Vector3.right*2);routine.MoveNext();root.SetActive(false);routine.MoveNext();
            Check(motion.transform.localPosition==rest&&!attack.IsPlaying,"disable restores dash");
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }

}
