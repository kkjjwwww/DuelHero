using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DuelHero.Battle;
using DuelHero.Cards;
using UnityEditor;
using UnityEngine;

public static class CardMechanicValidation
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("Card mechanic validation: " + message); checks++; }
    private static CardInstance Reserve(StunValidation.Fixture f, CardDefinition definition)
    {
        var card = new CardInstance(definition);
        ((List<CardInstance>)typeof(PlayerDeck).GetField("cards",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f.Deck)).Add(card);
        Check(f.Queue.TryReserve(card), "reserve " + definition.id);
        return card;
    }
    private static void Fill(StunValidation.Fixture f) { while (f.Queue.Reservations.Count < 3) Check(f.Queue.TryReserve(f.Deck.WaitAction),"fill wait"); }
    private static CardDefinition Both(CardDefinition riposte, int damage)
    {
        var card = JsonUtility.FromJson<CardDefinition>(JsonUtility.ToJson(riposte));
        card.effects = new[] {
            new CardEffectDefinition { id="base",effectType="damage",targetType="적",value=damage,resolutionOrder=1,
                rangeOffsets=riposte.effects.Single(e=>e.IsCounter).rangeOffsets },
            riposte.effects.Single(e=>e.IsCounter) };
        return card;
    }
    [MenuItem("Tools/Duel Hero/Cards/Validate bleed and counter")]
    public static void Run()
    {
        checks=0;
        var db=AssetDatabase.LoadAssetAtPath<CardDatabase>(CardSheetImporter.DatabasePath);
        Check(db.cards.Length==14 && db.cards.Count(c=>c.isObtainable)==5,"14 real cards, five obtainable cards");
        foreach (var id in new[]{"bleed_attack_001","riposte_stance_001","tear_wound_001"})
        {
            var card=db.cards.Single(c=>c.id==id);
            Check(CardEffectExecutor.SupportsDefinition(card) && !card.ResolvedDescription().Contains("{effect"),"supported resolved card "+id);
            var rotated=CardDirectionRules.ForExecution(card,Vector2Int.up);
            Check(rotated.effects.Select(e=>e.triggerTiming).SequenceEqual(card.effects.Select(e=>e.triggerTiming)) &&
                rotated.effects.Select(e=>e.statusMultiplier).SequenceEqual(card.effects.Select(e=>e.statusMultiplier)),"direction preserves new fields");
        }
        Check(db.keywords.Single(k=>k.id=="bleed").TextColor=="#FF5A5A" && new KeywordDefinition().TextColor=="#FFBA47","keyword colors and legacy fallback");
        using(var f=new StunValidation.Fixture())
        {
            Reserve(f,f.Card("bleed_attack_001"));Fill(f);
            f.Turn.SlotCompleted += slot=> { if(slot==0) Check(f.Enemy.Health==93 && f.Enemy.Bleed==2,"same-slot bleed tick after attack"); };
            f.Execute(new[]{WaitCard.Definition,WaitCard.Definition,WaitCard.Definition});
            Check(f.Enemy.Health==90 && f.Enemy.Bleed==0 && f.Player.Energy==85,"bleed 3/2/1 ticks and cost");
        }
        using(var f=new StunValidation.Fixture())
        {
            f.Enemy.ApplyGuard(100,2);
            var card=f.Card("bleed_attack_001");
            f.Resolver.ApplyDamageBatch(f.Resolver.CalculateDamageBatch(new[]{new CombatEffectResolver.AttackRequest("p",card,card.effects[0])},1));
            Check(f.Enemy.Health==100 && f.Enemy.Bleed==3,"zero HP damage still applies bleed");
            f.Enemy.ApplyBleed(2); Check(f.Enemy.Bleed==5,"bleed stacks add");
            f.Resolver.EndSlot(1);
            Check(f.Enemy.Health==95 && f.Enemy.GuardReduction==96 && f.Enemy.Bleed==4,"bleed ignores and preserves remaining guard");
            card=CardDirectionRules.ForExecution(f.Db.cards.Single(c=>c.id=="bleed_attack_001"),Vector2Int.up);
            f.Resolver.ApplyDamageBatch(f.Resolver.CalculateDamageBatch(new[]{new CombatEffectResolver.AttackRequest("p",card,card.effects[0])},1));
            Check(f.Enemy.Bleed==4,"miss does not apply bleed");
        }
        using(var f=new StunValidation.Fixture())
        {
            f.Enemy.ApplyBleed(3); var card=f.Card("tear_wound_001");
            f.Resolver.ApplyDamageBatch(f.Resolver.CalculateDamageBatch(new[]{new CombatEffectResolver.AttackRequest("p",card,card.effects[0])},1));
            Check(f.Enemy.Health==85 && f.Enemy.Bleed==3,"6 plus three times bleed, no consumption");
        }
        using(var f=new StunValidation.Fixture())
        {
            Reserve(f,f.Card("riposte_stance_001"));Fill(f);int counters=0;
            f.Turn.ActionResolved += r=>{if(r.Kind==BattleActionKind.CounterTriggered)counters++;};
            f.Execute(new[]{WaitCard.Definition,WaitCard.Definition,WaitCard.Definition});
            Check(counters==0 && f.Enemy.Health==100,"no incoming attack skips counter stage");
        }
        using(var f=new StunValidation.Fixture())
        {
            Reserve(f,f.Card("riposte_stance_001"));Fill(f);int counters=0;
            f.Turn.ActionResolved += r=>{if(r.Kind==BattleActionKind.CounterTriggered)counters++;};
            f.Execute(new[]{f.Card("slash_001",true),WaitCard.Definition,WaitCard.Definition});
            Check(counters==1 && f.Player.Health==100 && f.Enemy.Health==92,"zero-damage direct hit triggers one counter");
        }
        using(var f=new StunValidation.Fixture())
        {
            var card=JsonUtility.FromJson<CardDefinition>(JsonUtility.ToJson(f.Card("slash_001",true)));
            card.effects=Enumerable.Range(0,3).Select(i=>new CardEffectDefinition{effectType="damage",targetType="적",value=2,rangeOffsets=card.effects[0].rangeOffsets}).ToArray();
            Reserve(f,f.Card("riposte_stance_001"));Fill(f);int counters=0;
            f.Turn.ActionResolved += r=>{if(r.Kind==BattleActionKind.CounterTriggered)counters++;};
            f.Execute(new[]{card,WaitCard.Definition,WaitCard.Definition});
            Check(counters==1 && f.Enemy.Health==92,"multiple incoming hits trigger only once");
        }
        using(var f=new StunValidation.Fixture())
        {
            Reserve(f,Both(f.Card("riposte_stance_001"),1));Fill(f);
            f.Player.SetHealthForDebug(9); f.Enemy.SetHealthForDebug(9);
            var order=new List<string>();f.Turn.ActionResolved += r=>{if(r.Kind==BattleActionKind.CounterTriggered)order.Add(r.ActorId);};
            f.Execute(new[]{Both(f.Card("riposte_stance_001",true),1),WaitCard.Definition,WaitCard.Definition});
            Check(order.SequenceEqual(new[]{"p","e"}) && f.Player.Health==0 && f.Enemy.Health==0,"player-first presentation and simultaneous lethal counters without chaining");
        }
        using(var f=new StunValidation.Fixture())
        {
            Reserve(f,Both(f.Card("riposte_stance_001"),20));Fill(f);f.Enemy.SetHealthForDebug(10);int enemyCounters=0;
            f.Turn.ActionResolved += r=>{if(r.Kind==BattleActionKind.CounterTriggered && r.ActorId=="e")enemyCounters++;};
            f.Execute(new[]{Both(f.Card("riposte_stance_001",true),1),WaitCard.Definition,WaitCard.Definition});
            Check(enemyCounters==0,"units killed by base action cannot counter");
        }
        using(var f=new StunValidation.Fixture())
        {
            Reserve(f,f.Card("riposte_stance_001"));Fill(f);int enemyCounters=0;
            f.Turn.ActionResolved += r=>{if(r.Kind==BattleActionKind.CounterTriggered && r.ActorId=="e")enemyCounters++;};
            f.Execute(new[]{Both(f.Card("riposte_stance_001",true),1),WaitCard.Definition,WaitCard.Definition});
            Check(enemyCounters==0,"counter damage does not trigger enemy counter");
        }
        using(var f=new StunValidation.Fixture())
        {
            Reserve(f,f.Card("riposte_stance_001"));Fill(f);f.Player.ApplyBleed(3);int counters=0;
            f.Turn.ActionResolved += r=>{if(r.Kind==BattleActionKind.CounterTriggered)counters++;};
            f.Execute(new[]{WaitCard.Definition,WaitCard.Definition,WaitCard.Definition});
            Check(counters==0 && f.Player.Health==94,"bleed does not trigger counter");
        }
        using(var f=new StunValidation.Fixture())
        {
            var card=CardDirectionRules.ForExecution(f.Db.cards.Single(c=>c.id=="riposte_stance_001"),Vector2Int.up);
            Reserve(f,card);Fill(f);
            f.Execute(new[]{f.Card("slash_001",true),WaitCard.Definition,WaitCard.Definition});
            Check(f.Enemy.Health==100,"counter uses card range, not guaranteed attacker targeting");
        }
        using(var f=new StunValidation.Fixture())
        {
            f.Queue.TryReserve(f.Deck.WaitAction); f.Queue.TryReserve(f.Deck.WaitAction); Reserve(f,f.Card("bleed_attack_001"));
            f.Execute(new[]{WaitCard.Definition,WaitCard.Definition,WaitCard.Definition});
            Check(f.Enemy.Bleed==2,"third-slot bleed remains after immediate tick");
            f.Queue.CompleteExecution(); Check(f.Enemy.Bleed==2,"bleed carries to next turn");
        }
        var map=new GridMap(new[]{Vector2Int.zero,Vector2Int.right},Array.Empty<Vector2Int>());
        var initial=new CardPreviewState(Vector2Int.zero,100,100,map);
        var stance=db.cards.Single(c=>c.id=="riposte_stance_001");
        var preview=CardPreviewCalculator.Calculate(initial,new[]{new CardPreviewRequest(stance,Vector2Int.right)})[0];
        Check(preview.GuardPreviewGain==10 && preview.AttackCells.Count==2 && preview.EnergyAfter==80,"preview shows guard and counter range without unconditional effect");
        var instance=new CardInstance(stance);
        var context=new EnemyDecisionContext(new[]{instance},new EnemyPlanningState(Vector2Int.zero,100,100,map),Vector2Int.right,1,
            new EnemyUnitSnapshot(100,100,100,100,Array.Empty<DuelHero.Units.GuardEffectSnapshot>()),
            new EnemyUnitSnapshot(100,100,100,100,Array.Empty<DuelHero.Units.GuardEffectSnapshot>()));
        var plan=new[]{new EnemyPlannedAction(instance,Vector2Int.right),new EnemyPlannedAction(new CardInstance(WaitCard.Definition),Vector2Int.right),new EnemyPlannedAction(new CardInstance(WaitCard.Definition),Vector2Int.right)};
        Check(EnemyPlanningState.SimulatePlan(context,plan).TotalEffectiveDamage==0,"AI does not assume counter activation");
        Debug.Log("CARD_MECHANIC_VALIDATION_PASS: "+checks+" checks");
    }
}
