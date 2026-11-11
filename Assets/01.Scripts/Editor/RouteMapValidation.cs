using System;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Map;
using UnityEditor;
using UnityEngine;

internal static class RouteMapValidation
{
    [MenuItem("Tools/Duel Hero/Map/Validate routes")]
    private static void ValidateRoutes()
    {
        var signatures = new HashSet<string>();
        for (uint seed = 0; seed < 1000; seed++)
        {
            var map = RouteMapGenerator.Generate(seed);
            var repeat = RouteMapGenerator.Generate(seed);
            signatures.Add(Signature(map));
            Require(Signature(map) == Signature(repeat), seed, "같은 시드의 결과가 다릅니다.");
            Require(map.Seed == seed && map.RulesVersion == RouteMapGenerator.RulesVersion,
                seed, "시드 또는 생성 규칙 버전이 다릅니다.");

            var start = map.GetNode(map.StartNodeId);
            var secondStageTypes = start.NextNodeIds.Select(id => map.GetNode(id).Type);
            Require(start.Stage == 1 && start.Type == RouteNodeType.Battle &&
                    new HashSet<RouteNodeType>(secondStageTypes).SetEquals(
                        new[] { RouteNodeType.Battle, RouteNodeType.Shop, RouteNodeType.Rest }),
                seed, "첫 전투 뒤에 전투·상점·휴식 선택지가 모두 있어야 합니다.");
            Require(map.Nodes.Select(node => node.Id).Distinct().Count() == map.Nodes.Count,
                seed, "노드 ID가 중복됩니다.");
            Require(map.Nodes.Select(node => (node.Stage, node.Type)).Distinct().Count() == map.Nodes.Count,
                seed, "화면에서 같은 아이콘에 실제 노드가 여러 개 겹칩니다.");

            var visited = new HashSet<string>();
            var lanes = map.Nodes.GroupBy(node => node.Stage)
                .SelectMany(stage => stage.GroupBy(node => node.Type)
                    .SelectMany((group, index) => group.Select(node =>
                        (node.Id, Lane: stage.Key == 1 || stage.Key == 5 ? 1 : index))))
                .ToDictionary(entry => entry.Id, entry => entry.Lane);
            var combinations = new HashSet<(int Battles, int Shops, int Rests)>();
            Visit(map, start, 0, 0, 0, visited, lanes, combinations);
            Require(visited.Count == map.Nodes.Count, seed, "도달할 수 없는 노드가 있습니다.");
            for (int stage = 1; stage < 5; stage++)
            {
                var links = map.Nodes.Where(node => node.Stage == stage)
                    .SelectMany(node => node.NextNodeIds.Select(target => (From: lanes[node.Id], To: lanes[target])))
                    .Distinct().ToArray();
                Require(links.Length <= 6, seed, "인접 단계 사이의 선이 6개를 초과합니다.");
                int crossings = links.Sum(left => links.Count(right => left.From < right.From && left.To > right.To));
                Require(crossings <= 1, seed, "인접 단계 사이의 교차가 한 곳을 초과합니다.");
            }
            Require(combinations.SetEquals(new[] { (2, 1, 1), (2, 2, 0), (2, 0, 2), (3, 1, 0), (3, 0, 1) }),
                seed, "보스 전 방문 조합이 누락되었습니다.");
        }

        Require(signatures.Count > 1, 0, "시드에 따른 맵 배치 변화가 없습니다.");
        Debug.Log("경로 맵 검증 완료: 시드 1,000개와 모든 시작점→보스 경로가 통과했습니다.");
    }

    private static void Visit(RouteMap map, RouteNode node, int battles, int shops, int rests,
        HashSet<string> visited, Dictionary<string, int> lanes,
        HashSet<(int Battles, int Shops, int Rests)> combinations)
    {
        visited.Add(node.Id);
        if (node.Type == RouteNodeType.Battle) battles++;
        if (node.Type == RouteNodeType.Shop) shops++;
        if (node.Type == RouteNodeType.Rest) rests++;
        if (node.Type == RouteNodeType.Boss)
        {
            Require(node.Id == map.BossNodeId && node.Stage == 5 && battles >= 2 && battles <= 3,
                map.Seed, "완주 경로의 노드 구성이 잘못되었습니다.");
            Require(node.NextNodeIds.Count == 0, map.Seed, "보스 뒤에 연결된 노드가 있습니다.");
            combinations.Add((battles, shops, rests));
            return;
        }

        Require(node.NextNodeIds.Count > 0, map.Seed, $"막다른 노드: {node.Id}");
        foreach (var targetId in node.NextNodeIds)
        {
            var target = map.GetNode(targetId);
            Require(target.Stage == node.Stage + 1, map.Seed, "인접하지 않은 단계가 연결되었습니다.");
            Require(Math.Abs(lanes[node.Id] - lanes[target.Id]) <= 1, map.Seed, "두 칸 이상 건너뛰는 연결이 있습니다.");
            Visit(map, target, battles, shops, rests, visited, lanes, combinations);
        }
    }

    private static string Signature(RouteMap map) => string.Join("|", map.Nodes.Select(node =>
        $"{node.Id}:{node.Stage}:{node.Type}:{string.Join(",", node.NextNodeIds)}"));

    [MenuItem("Tools/Duel Hero/Map/Validate progress")]
    private static void ValidateProgress()
    {
        int prefixes = 0;
        for (uint seed = 0; seed < 1000; seed++)
        {
            var map = RouteMapGenerator.Generate(seed);
            string before = Signature(map);
            VisitProgress(map, new List<string>(), ref prefixes);
            Require(Signature(map) == before, seed, "진행 처리 중 경로 데이터가 변경되었습니다.");
        }
        Debug.Log($"맵 진행 검증 완료: 시드 1,000개, 진행 상태 {prefixes}개가 통과했습니다.");
    }

    private static void VisitProgress(RouteMap map, List<string> path, ref int prefixes)
    {
        // Reconstruct each possible path prefix independently, including merges.
        var progress = new RouteMapProgress(map);
        foreach (var id in path)
            Require(progress.TrySelect(id), map.Seed, $"유효한 노드를 선택하지 못했습니다: {id}");

        string current = path.Count == 0 ? null : path[path.Count - 1];
        bool complete = current == map.BossNodeId;
        Require(progress.CurrentNodeId == current && progress.IsComplete == complete,
            map.Seed, "현재 위치 또는 완주 상태가 다릅니다.");
        Require(progress.CompletedNodeIds.SequenceEqual(path), map.Seed, "완료 기록이 다릅니다.");

        var expected = complete ? new string[0]
            : current == null ? new[] { map.StartNodeId }
            : map.GetNode(current).NextNodeIds.ToArray();
        foreach (var node in map.Nodes)
        {
            bool allowed = expected.Contains(node.Id);
            Require(progress.CanSelect(node.Id) == allowed, map.Seed,
                $"선택 가능 판정이 다릅니다: {node.Id}");
            if (!allowed)
                Require(!progress.TrySelect(node.Id), map.Seed, $"금지된 선택이 허용되었습니다: {node.Id}");
        }
        Require(!progress.CanSelect(null) && !progress.TrySelect(null) &&
                !progress.CanSelect("missing-node") && !progress.TrySelect("missing-node"),
            map.Seed, "존재하지 않는 노드 선택이 허용되었습니다.");
        Require(progress.CurrentNodeId == current && progress.IsComplete == complete &&
                progress.CompletedNodeIds.SequenceEqual(path),
            map.Seed, "거부된 선택이 진행 상태를 변경했습니다.");
        prefixes++;

        foreach (var nextId in expected)
        {
            path.Add(nextId);
            VisitProgress(map, path, ref prefixes);
            path.RemoveAt(path.Count - 1);
        }
    }

    private static void Require(bool condition, uint seed, string message)
    {
        if (!condition) throw new InvalidOperationException($"시드 {seed}: {message}");
    }
}
