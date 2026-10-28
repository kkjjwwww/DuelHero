using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DuelHero.Battle;
using DuelHero.Cards;
using DuelHero.Units;
using DuelHero.Run;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
#if UNITY_6000_0_OR_NEWER
using Unity.Profiling;
#endif

internal static class EnemyPlanSimulationValidation
{
    private static readonly BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

    [MenuItem("Tools/Duel Hero/AI/Validate plan simulation")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("플레이 모드를 종료한 뒤 검증해주세요.");
        int comparisons = 0;
        foreach (var direction in Directions)
        {
            Compare(new[] { Move("move", 1), Attack("attack", 10, 7), Wait() }, direction, 20, 100, targetDistance: 2);
            Compare(new[] { Restore(15), Attack("attack", 10, 7), Wait() }, direction, 0, 100);
            Compare(new[] { Dash(1, 10), Attack("follow", 10, 7), Wait() }, direction, 30, 100, targetDistance: 2);
            comparisons += 3;
        }
        Compare(new[] { Attack("a", 1, 8), Attack("b", 1, 8), Attack("c", 1, 8) },
            Vector2Int.right, 20, 10);
        Compare(new[] { Attack("multi", 1, 8, 8), Wait(), Wait() }, Vector2Int.right, 20, 10);
        Compare(new[] { Attack("multiShield", 1, 8, 8), Wait(), Wait() },
            Vector2Int.right, 20, 10, new[] { new GuardEffectSnapshot(10, 1) });
        Compare(new[] { Attack("a", 1, 8), Attack("b", 1, 8), Wait() },
            Vector2Int.right, 20, 30, new[] { new GuardEffectSnapshot(10, 2), new GuardEffectSnapshot(3, 1) });
        Compare(new[] { Wait(), Attack("a", 1, 8), Wait() },
            Vector2Int.right, 20, 30, new[] { new GuardEffectSnapshot(10, 1) });
        Compare(new[] { Guard(), Wait(), Wait() }, Vector2Int.right, 20, 30);
        Compare(new[] { Restore(20), Wait(), Wait() }, Vector2Int.right, 100, 30);
        var ranged = Attack("range", 1, 7);
        ranged.Definition.effects[0].rangeOffsets = new[] { Vector2Int.right * 2 };
        Compare(new[] { Dash(3, 7), ranged, Wait() }, Vector2Int.right, 30, 100,
            blocked: new Vector2Int(3, 2), targetDistance: 2);
        comparisons += 8;
        var lateMove = Dash(1, 10);
        lateMove.Definition.effects[0].resolutionOrder = 20;
        lateMove.Definition.effects[1].resolutionOrder = 0;
        Compare(new[] { lateMove, Wait(), Wait() }, Vector2Int.right, 30, 100, targetDistance: 2);
        Compare(new[] { Attack("miss", 10, 7), Restore(20), Wait() }, Vector2Int.right, 30, 100, targetDistance: 2);
        comparisons += 2;
        ValidatePlanRules();
        ValidateProvider();
        Debug.Log($"적 계획 시뮬레이션 검증 완료: 실행기 대조 {comparisons}개, 입력 불변·계획 제한·캐시 검사 통과.");
    }

    private static void Compare(CardInstance[] cards, Vector2Int direction, int energy, int health,
        GuardEffectSnapshot[] shields = null, Vector2Int? blocked = null, int targetDistance = 1, bool utility = false)
    {
        var root = new GameObject("EnemyPlanSimulationValidation");
        try
        {
            var grid = BuildGrid(root, blocked);
            var board = root.AddComponent<BoardOccupancy>();
            Set(board, "grid", grid);
            var actor = Unit(root, "actor", new Vector2Int(2, 2), 100, energy, board);
            var target = Unit(root, "target", new Vector2Int(2, 2) + direction * targetDistance, health, 50, board);
            foreach (var shield in shields ?? Array.Empty<GuardEffectSnapshot>())
                target.stats.ApplyGuard(shield.Amount, shield.RemainingSlots);
            actor.stats.ApplyGuard(2, 2);
            var resolver = root.AddComponent<CombatEffectResolver>();
            Bind(resolver, actor.movement, actor.stats, target.movement, target.stats);
            var executor = root.AddComponent<CardEffectExecutor>();
            Set(executor, "combatEffects", resolver);
            var damageEvents = new List<BattleActionResult>();
            resolver.ActionResolved += result => { if (result.Kind == BattleActionKind.Damage) damageEvents.Add(result); };
            var context = Context(cards, grid.Map, actor.movement.GridPosition, target.movement.GridPosition,
                actor.stats, target.stats);
            var plan = utility ? new UtilityPlanSelector(new UtilityAiSettings { randomCandidateCount = 1 }).Select(context) :
                cards.Select(c => new EnemyPlannedAction(c, direction)).ToArray();
            int[] available = cards.Select(c => c.AvailableFromRound).ToArray();
            var definitionsBefore = DefinitionSignature(cards);
            var result = EnemyPlanningState.SimulatePlan(context, plan);
            var repeat = EnemyPlanningState.SimulatePlan(context, plan);
            var stateType = typeof(EnemyPlanningState);
            var full = new EnemyPlanningState(context.State.Position, energy, 100, context.State.Map);
            stateType.GetMethod("Reset", Fields).Invoke(full, new object[] { context });
            var preview = new EnemyPlanningState(context.State.Position, energy, 100, context.State.Map);
            for (int slot = 0; slot < result.ExecutedSlots; slot++)
            {
                stateType.GetMethod("CopyValuesFrom", Fields).Invoke(preview, new object[] { full });
                int raw = (int)stateType.GetMethod("PreviewAction", Fields).Invoke(preview,
                    new object[] { context, context.GetPreparedAction(plan[slot]), full });
                var row = (EnemyActionSimulation)stateType.GetMethod("Step", Fields).Invoke(full,
                    new object[] { context, plan[slot], slot });
                Require(preview.Position == full.Position && preview.Energy == full.Energy &&
                    (int)stateType.GetProperty("TargetHealth", Fields).GetValue(preview) == row.TargetHealthAfter &&
                    raw == row.RawHitDamage, "가벼운 후보 예측과 전체 실행 예측이 다릅니다.");
            }
            Require(result.TotalEffectiveDamage == repeat.TotalEffectiveDamage &&
                actor.stats.Energy == energy && target.stats.Health == health && actor.movement.GridPosition == new Vector2Int(2, 2) &&
                actor.stats.GuardReduction == 2 && target.stats.GuardReduction == (shields ?? Array.Empty<GuardEffectSnapshot>()).Sum(s => s.Amount),
                "시뮬레이션이 원본 유닛을 변경했습니다.");
            Require(context.State.Energy == energy && context.State.Position == new Vector2Int(2, 2) &&
                cards.Select(c => c.AvailableFromRound).SequenceEqual(available) && cards.All(c => c.ReservedSlot == -1),
                "시뮬레이션이 입력 또는 카드 상태를 변경했습니다.");
            Require(definitionsBefore == DefinitionSignature(cards), "공유 카드 효과·좌표가 변경되었습니다.");
            int totalDamage = 0, executed = 0;
            for (int slot = 0; slot < 3 && target.stats.Health > 0; slot++)
            {
                var row = result.Slots[slot];
                damageEvents.Clear();
                int beforeHealth = target.stats.Health, beforeEnergy = actor.stats.Energy;
                var beforePosition = actor.movement.GridPosition;
                int beforeGuard = actor.stats.GuardReduction, beforeTargetGuard = target.stats.GuardReduction;
                var execution = context.GetPreparedAction(plan[slot]).ExecutionCard;
                Require(actor.stats.TrySpendEnergy(execution.energyCost), "실행 비용 부족");
                int afterCost = actor.stats.Energy;
                executor.ExecutePreparation("actor", execution, 1);
                int duringGuard = actor.stats.GuardReduction;
                int rawExpected = execution.effects.Where(e => e.effectType == "damage" &&
                    AttackRangeCalculator.Calculate(actor.movement.GridPosition, e.rangeOffsets).Contains(target.movement.GridPosition))
                    .Sum(e => e.value);
                var requests = execution.effects.OrderBy(e => e.resolutionOrder).Where(e => e.effectType == "damage")
                    .Select(e => new CombatEffectResolver.AttackRequest("actor", execution.id, e));
                resolver.ApplyDamageBatch(resolver.CalculateDamageBatch(requests, 1));
                resolver.EndSlot(1);
                int dealt = beforeHealth - target.stats.Health;
                Require(row.PositionBefore == beforePosition && row.PositionAfter == actor.movement.GridPosition &&
                    row.EnergyBefore == beforeEnergy && row.EnergyAfterCost == afterCost && row.EnergyAfter == actor.stats.Energy &&
                    row.TargetHealthBefore == beforeHealth && row.TargetHealthAfter == target.stats.Health &&
                    row.EffectiveDamage == dealt && row.RawHitDamage == rawExpected &&
                    row.AbsorbedDamage == damageEvents.Sum(e => e.ReducedDamage) &&
                    row.GuardBefore == beforeGuard && row.GuardDuringSlot == duringGuard &&
                    row.GuardAfterSlot == actor.stats.GuardReduction && row.TargetGuardBefore == beforeTargetGuard &&
                    row.TargetGuardAfterSlot == target.stats.GuardReduction, "슬롯 결과가 실제 실행기와 다릅니다.");
                totalDamage += dealt; executed++;
            }
            Require(result.ExecutedSlots == executed && result.TotalEffectiveDamage == totalDamage &&
                result.TargetHealthAfter == target.stats.Health && result.EnergyAfter == actor.stats.Energy &&
                result.PositionAfter == actor.movement.GridPosition, "누적 결과가 실제 실행기와 다릅니다.");
            Require(ReferenceEquals(context.GetPreparedAction(plan[0]), context.GetPreparedAction(plan[0])),
                "카드·방향 캐시를 재사용하지 않습니다.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void ValidatePlanRules()
    {
        var map = Map();
        var first = Attack("first", 10, 7);
        var cards = new[] { first, Move("move", 1), Restore(20) };
        var context = Context(cards, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 20, 100);
        var plan = cards.Select(c => new EnemyPlannedAction(c, Vector2Int.right)).ToArray();
        Reject(() => EnemyPlanningState.SimulatePlan(context, new[] { plan[0], plan[0], plan[2] }));
        Reject(() => EnemyPlanningState.SimulatePlan(context, new[] { plan[0] }));
        Reject(() => EnemyPlanningState.SimulatePlan(context,
            new[] { new EnemyPlannedAction(Attack("foreign", 1, 1), Vector2Int.right), plan[1], plan[2] }));
        first.MarkUsed(1);
        Reject(() => EnemyPlanningState.SimulatePlan(context, plan));
        var costly = new[] { Attack("cost", 30, 1), Wait(), Wait() };
        Reject(() => EnemyPlanningState.SimulatePlan(Context(costly, map, new Vector2Int(2, 2),
            new Vector2Int(3, 2), 20, 100), costly.Select(c => new EnemyPlannedAction(c, Vector2Int.right)).ToArray()));
        var waits = new[] { Wait(), Wait(), Wait() };
        Require(EnemyPlanningState.SimulatePlan(Context(waits, map, new Vector2Int(2, 2),
            new Vector2Int(3, 2), 0, 100), waits.Select(c => new EnemyPlannedAction(c, Vector2Int.right)).ToArray()).ExecutedSlots == 3,
            "반복 대기를 실행하지 못했습니다.");
        var dead = Context(waits, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 0, 0);
        Require(EnemyPlanningState.SimulatePlan(dead, waits.Select(c => new EnemyPlannedAction(c, Vector2Int.right)).ToArray()).ExecutedSlots == 0,
            "사망한 대상에게 행동을 실행했습니다.");
    }

    private static void ValidateProvider()
    {
        bool recording = UtilityPlanSelector.CaptureScores;
        var savedRun = RunSessionStore.Current;
        UtilityPlanSelector.CaptureScores = true;
        var root = new GameObject("EnemyProviderValidation");
        var database = ScriptableObject.CreateInstance<CardDatabase>();
        try
        {
            var grid = BuildGrid(root, null);
            var board = root.AddComponent<BoardOccupancy>();
            Set(board, "grid", grid);
            var actor = Unit(root, "actor", new Vector2Int(2, 2), 100, 30, board);
            var target = Unit(root, "target", new Vector2Int(4, 2), 100, 100, board);
            var resolver = root.AddComponent<CombatEffectResolver>();
            Bind(resolver, actor.movement, actor.stats, target.movement, target.stats);
            var executor = root.AddComponent<CardEffectExecutor>();
            Set(executor, "combatEffects", resolver);
            database.cards = new[] { Move("move", 1).Definition, Attack("attack", 10, 7).Definition, Restore(20).Definition };
            var run = RunSessionStore.StartNew(1234, 100, 100, database.cards.Select(c => c.id));
            Require(run.TryBeginBattle(run.Map.StartNodeId), "검증용 전투 노드 진입 실패");
            var provider = root.AddComponent<EnemyActionPlanProvider>();
            Set(provider, "database", database); Set(provider, "stats", actor.stats);
            Set(provider, "movement", actor.movement); Set(provider, "target", target.movement);
            Set(provider, "effects", executor); Set(provider, "actorId", "actor");
            Set(provider, "cardIds", database.cards.Select(c => c.id).ToArray());
            int notifications = 0;
            provider.CardsChanged += () => notifications++;
            var plan = provider.CreatePlan(1);
            var simulation = provider.LastSimulation;
            var trace = provider.LastDecisionTrace;
            Require(trace != null && trace.Round == 1 && trace.Slots.Count == 3, "Provider의 점수 기록 연결 오류");
            uint decisionSeed = provider.LastDecisionSeed;
            var seedMethod = typeof(UtilityPlanSelector).Assembly.GetType("DuelHero.Battle.EnemyDecisionRandom")
                .GetMethod("CreateSeed", BindingFlags.NonPublic | BindingFlags.Static);
            Require(decisionSeed == (uint)seedMethod.Invoke(null, new object[] { run.Map.Seed, run.PendingBattleNodeId, "actor", 1 }) &&
                trace.DecisionSeed == decisionSeed, "런·노드·적·라운드 시드 전달 오류");
            Set(target.movement, "gridPosition", new Vector2Int(0, 0));
            Require(ReferenceEquals(plan, provider.CreatePlan(1)) && ReferenceEquals(simulation, provider.LastSimulation) &&
                ReferenceEquals(trace, provider.LastDecisionTrace) && provider.LastDecisionSeed == decisionSeed,
                "공개한 계획을 같은 라운드에 다시 생성했습니다.");
            Set(target.movement, "gridPosition", new Vector2Int(4, 2));
            Require(plan.Count == 3 && simulation.ExecutedSlots == 3 && simulation.TotalEffectiveDamage == 7 &&
                provider.Cards.All(c => c.AvailableFromRound == 1) && actor.stats.Energy == 30 && target.stats.Health == 100,
                "Provider의 계획·공개 상태 연결 오류");
            for (int slot = 0; slot < plan.Count; slot++)
            {
                var card = plan[slot];
                Require(actor.stats.TrySpendEnergy(card.energyCost), "Provider 실행 비용 오류");
                executor.ExecutePreparation("actor", card, 1);
                resolver.ApplyDamageBatch(resolver.CalculateDamageBatch(card.effects.OrderBy(e => e.resolutionOrder)
                    .Where(e => e.effectType == "damage")
                    .Select(e => new CombatEffectResolver.AttackRequest("actor", card.id, e)), 1));
                resolver.EndSlot(1);
                Require(simulation.Slots[slot].EnergyAfter == actor.stats.Energy &&
                    simulation.Slots[slot].PositionAfter == actor.movement.GridPosition &&
                    simulation.Slots[slot].TargetHealthAfter == target.stats.Health, "Provider 실행 결과 불일치");
            }
            provider.CompletePlan(1, 3);
            Require(provider.Cards.All(c => c.RemainingCooldown(2) == 1) && notifications == 1,
                "기존 사용·쿨다운·알림 계약 변경");
            provider.CreatePlan(2);
            Require(provider.LastDecisionSeed != decisionSeed, "다음 라운드의 판단 시드가 갱신되지 않았습니다.");
            RunSessionStore.Clear();
            provider.CreatePlan(3);
            uint standalone = (uint)typeof(EnemyActionPlanProvider).GetField("standaloneSeed", Fields).GetValue(provider);
            Require(provider.LastDecisionSeed == (uint)seedMethod.Invoke(null, new object[] { standalone, null, "actor", 3 }),
                "단독 전투 시드 생성 오류");
            provider.CreatePlan(4);
            Require((uint)typeof(EnemyActionPlanProvider).GetField("standaloneSeed", Fields).GetValue(provider) == standalone &&
                provider.LastDecisionSeed == (uint)seedMethod.Invoke(null, new object[] { standalone, null, "actor", 4 }),
                "단독 전투의 기본 시드를 라운드마다 다시 생성했습니다.");
            Debug.Log("Provider 연결 검증 통과: 계획 → 시뮬레이션 → 기존 실행 → 사용·쿨다운.");
        }
        finally
        {
            UtilityPlanSelector.CaptureScores = recording;
            typeof(RunSessionStore).GetProperty("Current").SetValue(null, savedRun);
            Object.DestroyImmediate(root); Object.DestroyImmediate(database);
        }
    }

    [MenuItem("Tools/Duel Hero/AI/Validate utility AI")]
    public static void RunUtility()
    {
        Run();
        var settings = new UtilityAiSettings { randomCandidateCount = 1 };
        var selector = new UtilityPlanSelector(settings);
        var evaluator = new EnemyPlanEvaluator(settings);
        Require(evaluator.AttackCostPenalty(20, 20) > evaluator.AttackCostPenalty(20, 100) &&
            evaluator.AttackCostPenalty(30, 50) > evaluator.AttackCostPenalty(10, 50), "저에너지·고비용 감점 오류");
        var energyChoices = new[] { Attack("cheap", 1, 10), Attack("expensive", 20, 12) };
        var low = Context(energyChoices, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 20, 100);
        var high = Context(energyChoices, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 100, 100);
        Require(selector.Select(low)[0].Card == energyChoices[0] && selector.Select(high)[0].Card == energyChoices[1],
            "사용 직전 에너지가 실제 공격 선택에 반영되지 않았습니다.");

        var combo = new[] { Attack("cheap", 10, 4), Attack("strong", 30, 40), Restore(20) };
        var context = Context(combo, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 10, 100);
        var result = EnemyPlanningState.SimulatePlan(context, selector.Select(context));
        Require(result.TotalEffectiveDamage == 40,
            "즉시 약공격보다 회복→강공격 조합을 선택하지 못했습니다.");
        Compare(combo, Vector2Int.right, 10, 100, utility: true);

        var crowded = Enumerable.Range(0, 8).Select(i => Attack("weak" + i, 0, 1))
            .Concat(new[] { Restore(40), Attack("strong", 50, 80) }).ToArray();
        context = Context(crowded, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 10, 100);
        Require(EnemyPlanningState.SimulatePlan(context, selector.Select(context)).TotalEffectiveDamage >= 80,
            "공격 후보가 많을 때 회복 준비 경로가 탈락했습니다.");

        var weakRanged = Enumerable.Range(0, 8).Select(i => Attack("ranged" + i, 0, 3)).ToArray();
        foreach (var card in weakRanged) card.Definition.effects[0].rangeOffsets =
            new[] { Vector2Int.right, Vector2Int.right * 2, Vector2Int.right * 3 };
        var approach = weakRanged.Concat(new[] { Move("move1", 1), Move("move2", 1), Attack("strong", 10, 40) }).ToArray();
        context = Context(approach, Map(), new Vector2Int(1, 2), new Vector2Int(4, 2), 30, 100);
        var immediate = selector.Select(context);
        Require(weakRanged.Contains(immediate[0].Card), "현재 슬롯의 최고 점수 대신 미래 조합을 선택했습니다.");
        Compare(approach, Vector2Int.right, 30, 100, targetDistance: 3, utility: true);

        var ties = new[] { Attack("first", 5, 5), Attack("second", 5, 5) };
        context = Context(ties, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 5, 100);
        Require(selector.Select(context)[0].Card == ties[0], "동점에서 기존 카드 순서를 유지하지 않았습니다.");
        var restore = Restore(10);
        context = Context(new[] { restore }, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 0, 100);
        Require(selector.Select(context)[0].Direction == Vector2Int.right, "동점 방향 순서가 변경되었습니다.");

        var copies = new[] { Attack("copy", 5, 5), Attack("copy", 5, 5) };
        var cooldown = Attack("cooldown", 0, 100); cooldown.MarkUsed(1);
        var reserved = Attack("reserved", 0, 100); reserved.TryReserve(0, 1);
        var limited = copies.Concat(new[] { cooldown, reserved, Attack("costly", 100, 100) }).ToArray();
        context = Context(limited, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 10, 100);
        var plan = selector.Select(context);
        Require(plan.Take(2).All(a => copies.Contains(a.Card)) && plan[0].Card != plan[1].Card &&
            WaitCard.IsWait(plan[2].Card.Definition) && cooldown.AvailableFromRound == 3 && reserved.ReservedSlot == 0,
            "카드 복사본·중복·쿨다운·예약·대기 제한 오류");
        Require(plan.Select(a => a.Card.Definition.id + a.Direction).SequenceEqual(
            selector.Select(context).Select(a => a.Card.Definition.id + a.Direction)), "동일 입력에서 선택이 바뀌었습니다.");
        context = Context(Array.Empty<CardInstance>(), Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 0, 100);
        Require(selector.Select(context).All(a => WaitCard.IsWait(a.Card.Definition)), "모든 선택지가 막혔을 때 반복 대기 오류");

        int cases = 0;
        foreach (var direction in Directions)
        {
            Compare(new[] { Move("move", 1), Attack("attack", 10, 7), Restore(20) }, direction,
                20, 100, targetDistance: 2, utility: true);
            Compare(new[] { Move("move", 3), Attack("attack", 10, 7), Guard() }, direction,
                20, 100, blocked: new Vector2Int(2, 2) + direction, targetDistance: 2, utility: true);
            cases += 2;
        }
        foreach (int x in new[] { 0, 2, 5 }) foreach (int y in new[] { 0, 2, 4 })
        foreach (int energy in new[] { 0, 10, 100 })
        {
            var cards = new[] { Move("m1", 1), Move("m2", 1), Restore(20), Guard(), Attack("a", 10, 20) };
            context = Context(cards, Map(), new Vector2Int(x, y), new Vector2Int(3, 2), energy, 100);
            var first = selector.Select(context);
            var metrics = selector.LastSearch;
            result = EnemyPlanningState.SimulatePlan(context, first);
            Require(result.EnergyAfter >= 0 && metrics.AppliedActions == result.ExecutedSlots &&
                metrics.AppliedActions <= 3 && metrics.ActionPreviews <= cards.Length * 4 * 3,
                "슬롯별 후보 확인 상한 또는 경계 행동 오류");
            cases++;
        }
        ValidateDecisionScores();
        ValidateRandomSelection();
        ProfileUtility();
        Debug.Log($"슬롯별 유틸리티 AI 검증 통과: 이동·회복 후 공격, 저에너지 감점, 동점 순서·카드 제한·반복 대기, 경계·실행 대조 {cases}개.");
    }

    private static void ProfileUtility()
    {
        bool recording = UtilityPlanSelector.CaptureScores;
        UtilityPlanSelector.CaptureScores = false;
        try { ProfileUtilityCore(); }
        finally { UtilityPlanSelector.CaptureScores = recording; }
    }

    private static void ValidateDecisionScores()
    {
        bool recording = UtilityPlanSelector.CaptureScores;
        try
        {
            var cards = new[] { Move("move", 1), Attack("attack", 10, 7), Restore(20) };
            var context = Context(cards, Map(), new Vector2Int(2, 2), new Vector2Int(4, 2), 30, 100);
            var selector = new UtilityPlanSelector(new UtilityAiSettings());
            UtilityPlanSelector.CaptureScores = false;
            var plain = selector.Select(context);
            Require(selector.LastTrace == null, "기록을 껐는데 후보 기록을 생성했습니다.");
            UtilityPlanSelector.CaptureScores = true;
            var captured = selector.Select(context);
            var trace = selector.LastTrace;
            Require(trace != null && trace.Round == context.Round && trace.Slots.Count == 3, "점수 기록 누락");
            int candidateCount = 0;
            for (int i = 0; i < trace.Slots.Count; i++)
            {
                Require(plain[i].Card == captured[i].Card && plain[i].Direction == captured[i].Direction,
                    "디버그 기록이 AI 선택을 변경했습니다.");
                var slot = trace.Slots[i];
                var chosen = slot.Candidates[slot.SelectedIndex];
                Require(cards[chosen.CardIndex] == captured[i].Card && chosen.Direction == captured[i].Direction &&
                    slot.EligibleIndices.Contains(slot.SelectedIndex) && chosen.Score.Total >= slot.MinimumScore &&
                    slot.BestScore == slot.Candidates.Max(c => c.Score.Total), "표시한 선택·점수가 실제 선택과 다릅니다.");
                Require(Math.Abs(chosen.Score.Total - (chosen.Score.Damage + chosen.Score.Opportunity +
                    chosen.Score.Energy - chosen.Score.CostPenalty)) < .0001f, "점수 구성 합계 오류");
                candidateCount += slot.Candidates.Count;
            }
            Require(candidateCount == selector.LastSearch.ActionPreviews, "평가한 후보 중 기록이 누락되었습니다.");
            string name = trace.Slots[0].Candidates[0].CardName;
            cards[0].Definition.name = "changed after decision";
            Require(trace.Slots[0].Candidates[0].CardName == name, "원본 카드 변경이 지난 기록을 바꿨습니다.");
            var blocked = Context(Array.Empty<CardInstance>(), Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 0, 100);
            selector.Select(blocked);
            Require(selector.LastTrace.Slots.All(s => s.SelectedIndex == -1 && s.Candidates.Count == 0 &&
                !string.IsNullOrEmpty(s.Note)), "대기 사유 또는 미평가 점수 표시 오류");
            UtilityPlanSelector.CaptureScores = false;
            selector.Select(context);
            Require(selector.LastTrace == null && trace.Slots.Count == 3, "다음 판단이 이전 점수 스냅샷을 변경했습니다.");
            Debug.Log("AI 점수 디버그 검증 통과: 기록 켜기·끄기, 전체 후보·추첨 자격·선택 일치, 점수 구성, 스냅샷·대기 사유.");
        }
        finally { UtilityPlanSelector.CaptureScores = recording; }
    }

    private static void ValidateRandomSelection()
    {
        bool recording = UtilityPlanSelector.CaptureScores;
        try
        {
#if UNITY_6000_0_OR_NEWER
            var globalRandom = UnityEngine.Random.state;
#endif
            var map = Map();
            var cards = Enumerable.Range(0, 5).Select(i => Attack("equal" + i, 0, 20)).ToArray();
            foreach (var card in cards) card.Definition.effects[0].rangeOffsets = Directions;
            var selector = new UtilityPlanSelector(new UtilityAiSettings());
            var observed = new HashSet<int>();
            for (uint seed = 0; seed < 64; seed++)
            {
                var context = Context(cards, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 100, 100, seed);
                UtilityPlanSelector.CaptureScores = true;
                var plan = selector.Select(context);
                var trace = selector.LastTrace;
                Require(trace.DecisionSeed == seed, "판단 시드 기록 오류");
                foreach (var slot in trace.Slots)
                {
                    Require(slot.EligibleIndices.Count == 3 && slot.EligibleIndices.Contains(slot.SelectedIndex), "상위 3개 후보 추첨 오류");
                    Require(slot.EligibleIndices.Select(i => slot.Candidates[i].CardIndex).Distinct().Count() == 3 &&
                        slot.EligibleIndices.All(i => slot.Candidates[i].Direction == Vector2Int.right &&
                            slot.Candidates[i].Score.Total >= slot.MinimumScore), "동일 결과 방향 중복 또는 점수 범위 오류");
                }
                observed.Add(trace.Slots[0].Candidates[trace.Slots[0].SelectedIndex].CardIndex);
                EnemyPlanningState.SimulatePlan(context, plan);
                UtilityPlanSelector.CaptureScores = false;
                var replay = selector.Select(context);
                Require(plan.Zip(replay, (a, b) => a.Card == b.Card && a.Direction == b.Direction).All(equal => equal),
                    "같은 시드·입력의 재현성 또는 디버그 켜기·끄기 독립성 오류");
            }
            Require(observed.SetEquals(new[] { 0, 1, 2 }), "다른 시드에서 유사 가치 후보의 다양성이 없습니다.");
            observed.Clear();
            for (int i = 0; i < cards.Length; i++) cards[i].Definition.effects[0].value = 20 - i;
            // A new context owns the prepared-card cache after the fixture's data change.
            for (uint seed = 0; seed < 64; seed++)
            {
                UtilityPlanSelector.CaptureScores = true;
                selector.Select(Context(cards, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 100, 100, seed));
                var slot = selector.LastTrace.Slots[0];
                Require(slot.EligibleIndices.Count == 3 && slot.EligibleIndices.All(i =>
                    slot.Candidates[i].Score.Total >= slot.MinimumScore), "10% 점수 경계 오류");
                observed.Add(slot.Candidates[slot.SelectedIndex].CardIndex);
            }
            Require(observed.SetEquals(new[] { 0, 1, 2 }), "최고점과 비슷한 차순위 후보가 선택되지 않았습니다.");

            UtilityPlanSelector.CaptureScores = true;
            var decisive = new[] { Attack("best", 0, 50), Attack("weak", 0, 5) };
            var negative = new[] { Attack("negativeA", 10, 5), Attack("negativeB", 10, 5), Attack("outside", 11, 5) };
            var negativeSelector = new UtilityPlanSelector(new UtilityAiSettings
                { damageWeight = 0, opportunityWeight = 0, remainingEnergyWeight = 0, attackCostWeight = 1 });
            for (uint seed = 0; seed < 32; seed++)
            {
                var context = Context(decisive, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 100, 100, seed);
                Require(selector.Select(context)[0].Card == decisive[0] && selector.LastTrace.Slots[0].EligibleIndices.Count == 1,
                    "명백히 낮은 점수 후보가 추첨에 포함됐습니다.");
                context = Context(negative, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 100, 100, seed);
                Require(negativeSelector.Select(context)[0].Card != negative[2], "음수 점수의 허용 범위 계산 오류");
                var slot = negativeSelector.LastTrace.Slots[0];
                Require(slot.BestScore < 0 && Math.Abs(slot.MinimumScore - (-1.1f)) < .0001f &&
                    slot.EligibleIndices.All(i => slot.Candidates[i].Score.Total >= slot.MinimumScore), "음수 점수 경계 오류");
            }
            var zero = new UtilityPlanSelector(new UtilityAiSettings
                { damageWeight = 0, opportunityWeight = 0, remainingEnergyWeight = 0, attackCostWeight = 0 });
            zero.Select(Context(cards, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 100, 100, 42));
            Require(zero.LastTrace.Slots[0].BestScore == 0 && zero.LastTrace.Slots[0].EligibleIndices.Count == 3,
                "0점 후보 추첨 오류");

            var seedMethod = typeof(UtilityPlanSelector).Assembly.GetType("DuelHero.Battle.EnemyDecisionRandom")
                .GetMethod("CreateSeed", BindingFlags.NonPublic | BindingFlags.Static);
            uint combined = (uint)seedMethod.Invoke(null, new object[] { 123u, "nodeA", "enemyA", 1 });
            Require(combined == (uint)seedMethod.Invoke(null, new object[] { 123u, "nodeA", "enemyA", 1 }) &&
                combined != (uint)seedMethod.Invoke(null, new object[] { 124u, "nodeA", "enemyA", 1 }) &&
                combined != (uint)seedMethod.Invoke(null, new object[] { 123u, "nodeB", "enemyA", 1 }) &&
                combined != (uint)seedMethod.Invoke(null, new object[] { 123u, "nodeA", "enemyB", 1 }) &&
                combined != (uint)seedMethod.Invoke(null, new object[] { 123u, "nodeA", "enemyA", 2 }), "판단 시드 구성 요소 누락");
            Reject(() => new UtilityPlanSelector(new UtilityAiSettings { randomCandidateCount = 0 }).Select(
                Context(cards, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 100, 100)));
            Reject(() => new UtilityPlanSelector(new UtilityAiSettings { randomScoreTolerance = float.NaN }).Select(
                Context(cards, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 100, 100)));
#if UNITY_6000_0_OR_NEWER
            Require(globalRandom.Equals(UnityEngine.Random.state), "Unity 전역 난수 상태를 변경했습니다.");
#endif
            Debug.Log("무작위 선택 검증 통과: 64개 시드 재현·다양성, 최대 3개·10% 범위, 방향 중복 제거, 음수·0점, 시드 구성.");
        }
        finally { UtilityPlanSelector.CaptureScores = recording; }
    }

    private static void ProfileUtilityCore()
    {
        var map = Map();
        var definitions = new[] { Move("up", 1), Move("down", 1), Move("left", 1), Move("right", 1),
            Guard(), Restore(20), Attack("a", 10, 10), Attack("b", 15, 15), Attack("c", 20, 20) };
        foreach (int copies in new[] { 1, 9 })
        {
            var cards = Enumerable.Range(0, copies).SelectMany(_ => definitions.Select(c =>
                new CardInstance(CardDirectionRules.ForExecution(c.Definition, Vector2Int.right)))).ToArray();
            var selectors = Enumerable.Range(0, BoardOccupancy.MaxUnits - 1)
                .Select(_ => new UtilityPlanSelector(new UtilityAiSettings())).ToArray();
            foreach (var selector in selectors) for (int i = 0; i < 5; i++) SampleUtility(selector, cards, map);
            var watch = new Stopwatch();
            int previews = 0, applied = 0;
            watch.Start();
            for (int sample = 0; sample < 100; sample++) foreach (var selector in selectors)
            {
                SampleUtility(selector, cards, map);
                previews = Math.Max(previews, selector.LastSearch.ActionPreviews);
                applied = Math.Max(applied, selector.LastSearch.AppliedActions);
            }
            watch.Stop();
            // Unity's allocation recorder itself adds profiling overhead. Measure bytes
            // in a separate pass so it does not inflate the ordinary decision timing.
#if UNITY_6000_0_OR_NEWER
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1,
                ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            long before = allocations.CurrentValue;
#else
            long before = GC.GetAllocatedBytesForCurrentThread();
#endif
            for (int sample = 0; sample < 100; sample++) foreach (var selector in selectors)
                SampleUtility(selector, cards, map);
#if UNITY_6000_0_OR_NEWER
            long bytes = allocations.CurrentValue - before;
            string allocation = allocations.Valid ? bytes / (100 * selectors.Length) + " bytes/회 (Unity GC 카운터)" : "GC 계측 불가";
#else
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            string allocation = before == 0 ? "현재 런타임에서 GC 할당 계측 미지원" : bytes / (100 * selectors.Length) + " bytes/회";
#endif
            Debug.Log($"유틸리티 프로파일: 카드 {cards.Length}장, 적 {selectors.Length}명 × 100라운드, " +
                $"판단+최종기록 평균 {watch.Elapsed.TotalMilliseconds / (100 * selectors.Length):F3}ms, " +
                $"{allocation}. 단일행동 확인 {previews}, 상태 갱신 {applied}회.");
        }
    }

    private static void SampleUtility(UtilityPlanSelector selector, CardInstance[] cards, GridMap map)
    {
        var context = Context(cards, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 30, 100);
        EnemyPlanningState.SimulatePlan(context, selector.Select(context));
    }

    private static GridMap Map() => new GridMap(
        Enumerable.Range(0, 6).SelectMany(x => Enumerable.Range(0, 5).Select(y => new Vector2Int(x, y))),
        Array.Empty<Vector2Int>());

    private static BattleGrid BuildGrid(GameObject root, Vector2Int? blocked)
    {
        var grid = root.AddComponent<BattleGrid>();
        for (int x = 0; x < 6; x++) for (int y = 0; y < 5; y++)
        {
            var go = new GameObject("cell");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(x, -.05f, y);
            var cell = go.AddComponent<GridCell>();
            var coordinate = new Vector2Int(x, y);
            Set(cell, "coordinate", coordinate);
            if (blocked == coordinate) Set(cell, "terrain", GridTerrain.LowObstacle);
        }
        grid.Rebuild();
        return grid;
    }

    private static (GridMovement movement, UnitStats stats) Unit(GameObject root, string name,
        Vector2Int position, int health, int energy, BoardOccupancy board)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var movement = go.AddComponent<GridMovement>();
        Set(movement, "gridPosition", position);
        Require(board.RegisterUnit(movement), "검증 유닛 등록 실패");
        var stats = go.AddComponent<UnitStats>();
        stats.Initialize(health, 100, energy, 100);
        return (movement, stats);
    }

    private static void Bind(CombatEffectResolver resolver, GridMovement actor, UnitStats actorStats,
        GridMovement target, UnitStats targetStats)
    {
        var binding = typeof(CombatEffectResolver).GetNestedType("UnitBinding", BindingFlags.NonPublic);
        var bindings = Array.CreateInstance(binding, 2);
        for (int i = 0; i < 2; i++)
        {
            var value = Activator.CreateInstance(binding, true);
            binding.GetField("id").SetValue(value, i == 0 ? "actor" : "target");
            binding.GetField("side").SetValue(value, i == 0 ? UnitDataKind.Enemy : UnitDataKind.Player);
            binding.GetField("movement").SetValue(value, i == 0 ? actor : target);
            binding.GetField("stats").SetValue(value, i == 0 ? actorStats : targetStats);
            bindings.SetValue(value, i);
        }
        Set(resolver, "units", bindings);
    }

    private static EnemyDecisionContext Context(CardInstance[] cards, GridMap map, Vector2Int origin, Vector2Int target,
        UnitStats actor, UnitStats victim) => new EnemyDecisionContext(cards,
            new EnemyPlanningState(origin, actor.Energy, actor.MaxEnergy, map), target, 1,
            new EnemyUnitSnapshot(actor.Health, actor.MaxHealth, actor.Energy, actor.MaxEnergy, actor.CaptureGuard()),
            new EnemyUnitSnapshot(victim.Health, victim.MaxHealth, victim.Energy, victim.MaxEnergy, victim.CaptureGuard()));
    private static EnemyDecisionContext Context(CardInstance[] cards, GridMap map, Vector2Int origin, Vector2Int target,
        int energy, int health, uint seed = 0) => new EnemyDecisionContext(cards, new EnemyPlanningState(origin, energy, 100, map), target, 1,
            new EnemyUnitSnapshot(100, 100, energy, 100, Array.Empty<GuardEffectSnapshot>()),
            new EnemyUnitSnapshot(health, 100, 50, 100, Array.Empty<GuardEffectSnapshot>()), seed);

    private static CardInstance Make(string id, int cost, params CardEffectDefinition[] effects) =>
        new CardInstance(new CardDefinition { id = id, name = id, description = "", energyCost = cost,
            keywordIds = new[] { "direction_select" }, effects = effects });
    private static CardInstance Attack(string id, int cost, params int[] damage) => Make(id, cost,
        damage.Select((value, i) => new CardEffectDefinition { effectType = "damage", targetType = "적",
            value = value, rangeOffsets = new[] { Vector2Int.right }, resolutionOrder = i }).ToArray());
    private static CardInstance Move(string id, int steps) => Make(id, 0,
        new CardEffectDefinition { effectType = "move", targetType = "자신", value = steps, rangeOffsets = new[] { Vector2Int.right } });
    private static CardInstance Dash(int steps, int damage) => Make("dash", 5,
        new CardEffectDefinition { effectType = "move", targetType = "자신", value = steps, rangeOffsets = new[] { Vector2Int.right } },
        new CardEffectDefinition { effectType = "damage", targetType = "적", value = damage,
            rangeOffsets = new[] { Vector2Int.right }, resolutionOrder = 1 });
    private static CardInstance Restore(int amount) => Make("restore", 0,
        new CardEffectDefinition { effectType = "energyRestore", targetType = "자신", value = amount });
    private static CardInstance Guard() => Make("guard", 0,
        new CardEffectDefinition { effectType = "guard", targetType = "자신", value = 10, durationSlots = 1 });
    private static CardInstance Wait() => new CardInstance(WaitCard.Definition);
    private static string DefinitionSignature(IEnumerable<CardInstance> cards) => string.Join("|", cards.SelectMany(c => c.Definition.effects)
        .Select(e => e.effectType + ":" + e.resolutionOrder + ":" + e.value + ":" +
            string.Join(",", e.rangeOffsets ?? Array.Empty<Vector2Int>())));
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("잘못된 계획을 거부하지 않았습니다.");
    }
}
