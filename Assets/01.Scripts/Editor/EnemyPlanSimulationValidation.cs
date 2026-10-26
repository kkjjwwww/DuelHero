using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DuelHero.Battle;
using DuelHero.Cards;
using DuelHero.Units;
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
        Profile();
        Debug.Log($"적 계획 시뮬레이션 검증 완료: 실행기 대조 {comparisons}개, 입력 불변·계획 제한·캐시·성능 검사 통과.");
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
            var plan = utility ? new UtilityPlanSelector(new UtilityAiSettings()).Select(context) :
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

    private static void Profile()
    {
        var map = Map();
        var definitions = new[] { Move("up", 1), Move("down", 1), Move("left", 1), Move("right", 1),
            Guard(), Restore(20), Attack("a", 10, 10), Attack("b", 15, 15), Attack("c", 20, 20) };
        foreach (int copies in new[] { 1, 9 })
        {
            var cards = Enumerable.Range(0, copies).SelectMany(_ => definitions.Select(c =>
                new CardInstance(CardDirectionRules.ForExecution(c.Definition, Vector2Int.right)))).ToArray();
            var planner = new EnemyActionPlanner<EnemyTacticState>(new StandardEnemyFsm(), new PriorityPlanSelector());
            for (int i = 0; i < 5; i++) Sample(planner, cards, map);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            // Current board limit is three enemies plus the player.
            for (int sample = 0; sample < 100; sample++)
                for (int enemy = 0; enemy < 3; enemy++) Sample(planner, cards, map);
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            string allocation = before == 0 ? "현재 런타임에서 GC 할당 계측 미지원" : allocated + " bytes";
            Debug.Log($"기존 우선순위 AI 프로파일: 카드 {cards.Length}장, 판단·시뮬레이션 300회, {watch.Elapsed.TotalMilliseconds:F2}ms, {allocation}.");
        }
    }

    private static void Sample(EnemyActionPlanner<EnemyTacticState> planner, CardInstance[] cards, GridMap map)
    {
        var context = Context(cards, map, new Vector2Int(2, 2), new Vector2Int(3, 2), 30, 100);
        EnemyPlanningState.SimulatePlan(context, planner.CreatePlan(context));
    }

    private static void ValidateProvider()
    {
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
            var provider = root.AddComponent<EnemyActionPlanProvider>();
            Set(provider, "database", database); Set(provider, "stats", actor.stats);
            Set(provider, "movement", actor.movement); Set(provider, "target", target.movement);
            Set(provider, "effects", executor); Set(provider, "actorId", "actor");
            Set(provider, "cardIds", database.cards.Select(c => c.id).ToArray());
            int notifications = 0;
            provider.CardsChanged += () => notifications++;
            var plan = provider.CreatePlan(1);
            var simulation = provider.LastSimulation;
            Set(target.movement, "gridPosition", new Vector2Int(0, 0));
            Require(ReferenceEquals(plan, provider.CreatePlan(1)) && ReferenceEquals(simulation, provider.LastSimulation),
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
            Debug.Log("Provider 연결 검증 통과: 계획 → 시뮬레이션 → 기존 실행 → 사용·쿨다운.");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(database); }
    }

    [MenuItem("Tools/Duel Hero/AI/Validate utility AI")]
    public static void RunUtility()
    {
        Run();
        var settings = new UtilityAiSettings();
        var selector = new UtilityPlanSelector(settings);
        var evaluator = new EnemyPlanEvaluator(settings);
        Require(evaluator.AttackCostPenalty(20, 20) > evaluator.AttackCostPenalty(20, 100) &&
            evaluator.AttackCostPenalty(30, 50) > evaluator.AttackCostPenalty(10, 50), "저에너지·고비용 감점 오류");
        var energyChoices = new[] { Attack("cheap", 1, 10), Attack("expensive", 20, 11) };
        var low = Context(energyChoices, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 20, 100);
        var high = Context(energyChoices, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 100, 100);
        Require(selector.Select(low)[0].Card == energyChoices[0] && selector.Select(high)[0].Card == energyChoices[1],
            "사용 직전 에너지가 실제 공격 선택에 반영되지 않았습니다.");

        var combo = new[] { Attack("cheap", 10, 4), Attack("strong", 30, 40), Restore(20) };
        var context = Context(combo, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 10, 100);
        var priority = new EnemyActionPlanner<EnemyTacticState>(new StandardEnemyFsm(), new PriorityPlanSelector());
        var result = EnemyPlanningState.SimulatePlan(context, selector.Select(context));
        Require(result.TotalEffectiveDamage == 40 && result.TotalEffectiveDamage >
            EnemyPlanningState.SimulatePlan(context, priority.CreatePlan(context)).TotalEffectiveDamage,
            "즉시 약공격보다 회복→강공격 조합을 선택하지 못했습니다.");
        Compare(combo, Vector2Int.right, 10, 100, utility: true);

        var crowded = Enumerable.Range(0, 8).Select(i => Attack("weak" + i, 0, 1))
            .Concat(new[] { Restore(40), Attack("strong", 50, 80) }).ToArray();
        context = Context(crowded, Map(), new Vector2Int(2, 2), new Vector2Int(3, 2), 10, 100);
        Require(EnemyPlanningState.SimulatePlan(context, selector.Select(context)).TotalEffectiveDamage >= 80,
            "공격 후보가 많을 때 회복 준비 경로가 탈락했습니다.");

        var weakRanged = Enumerable.Range(0, 8).Select(i => Attack("ranged" + i, 0, 1)).ToArray();
        foreach (var card in weakRanged) card.Definition.effects[0].rangeOffsets =
            new[] { Vector2Int.right, Vector2Int.right * 2, Vector2Int.right * 3 };
        var approach = weakRanged.Concat(new[] { Move("move1", 1), Move("move2", 1), Attack("strong", 10, 40) }).ToArray();
        context = Context(approach, Map(), new Vector2Int(1, 2), new Vector2Int(4, 2), 30, 100);
        result = EnemyPlanningState.SimulatePlan(context, selector.Select(context));
        Require(result.TotalEffectiveDamage >= 40, "즉시 원거리 공격 후보 때문에 이동→이동→공격 경로가 탈락했습니다.");
        Compare(approach, Vector2Int.right, 30, 100, targetDistance: 3, utility: true);

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
            Require(result.EnergyAfter >= 0 && metrics.PeakRetainedPlans <= settings.retainedPlans &&
                metrics.ExpandedPlans <= settings.actionsPerPlan * (1 + settings.retainedPlans * 2), "탐색 상한 또는 경계 행동 오류");
            cases++;
        }
        ProfileUtility();
        Debug.Log($"유틸리티 AI 검증 통과: 회복·접근 조합, 저에너지 감점, 카드 제한·반복 대기·결정성, 경계·실행 대조 {cases}개.");
    }

    private static void ProfileUtility()
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
            int previews = 0, expanded = 0, retained = 0;
            watch.Start();
            for (int sample = 0; sample < 100; sample++) foreach (var selector in selectors)
            {
                SampleUtility(selector, cards, map);
                previews = Math.Max(previews, selector.LastSearch.ActionPreviews);
                expanded = Math.Max(expanded, selector.LastSearch.ExpandedPlans);
                retained = Math.Max(retained, selector.LastSearch.PeakRetainedPlans);
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
                $"{allocation}. 단일행동 확인 {previews}, 다음슬롯 확장 {expanded}, 최대유지 {retained}.");
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
        int energy, int health) => new EnemyDecisionContext(cards, new EnemyPlanningState(origin, energy, 100, map), target, 1,
            new EnemyUnitSnapshot(100, 100, energy, 100, Array.Empty<GuardEffectSnapshot>()),
            new EnemyUnitSnapshot(health, 100, 50, 100, Array.Empty<GuardEffectSnapshot>()));

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
