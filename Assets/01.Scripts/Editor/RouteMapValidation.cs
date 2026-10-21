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
        Require(Signature(RouteMapGenerator.Generate(0)) !=
                Signature(RouteMapGenerator.Generate(0x6D2B79F5u)),
            0, "시드 0과 이전 대체 시드가 같은 맵을 만듭니다.");

        for (uint seed = 0; seed < 1000; seed++)
        {
            var map = RouteMapGenerator.Generate(seed);
            var repeat = RouteMapGenerator.Generate(seed);
            Require(Signature(map) == Signature(repeat), seed, "같은 시드의 결과가 다릅니다.");
            Require(map.Seed == seed && map.RulesVersion == RouteMapGenerator.RulesVersion,
                seed, "시드 또는 생성 규칙 버전이 다릅니다.");

            var start = map.GetNode(map.StartNodeId);
            Require(start.Stage == 1 && start.Type == RouteNodeType.Battle && start.NextNodeIds.Count >= 2,
                seed, "첫 전투 뒤에 두 갈래 이상이 없습니다.");
            Require(map.Nodes.Select(node => node.Id).Distinct().Count() == map.Nodes.Count,
                seed, "노드 ID가 중복됩니다.");

            var visited = new HashSet<string>();
            var utilityStages = new HashSet<int>();
            Visit(map, start, 0, 0, visited, utilityStages);
            Require(visited.Count == map.Nodes.Count, seed, "도달할 수 없는 노드가 있습니다.");
            Require(utilityStages.Count >= 2, seed, "경로별 휴식·상점 조우 시점이 같습니다.");
        }

        Debug.Log("경로 맵 검증 완료: 시드 1,000개와 모든 시작점→보스 경로가 통과했습니다.");
    }

    private static void Visit(RouteMap map, RouteNode node, int battles, int utilities,
        HashSet<string> visited, HashSet<int> utilityStages)
    {
        visited.Add(node.Id);
        if (node.Type == RouteNodeType.Battle) battles++;
        if (node.Type == RouteNodeType.Rest || node.Type == RouteNodeType.Shop)
        {
            utilities++;
            utilityStages.Add(node.Stage);
        }

        if (node.Type == RouteNodeType.Boss)
        {
            Require(node.Id == map.BossNodeId && node.Stage == 5 && battles == 3 && utilities == 1,
                map.Seed, "완주 경로의 노드 구성이 잘못되었습니다.");
            Require(node.NextNodeIds.Count == 0, map.Seed, "보스 뒤에 연결된 노드가 있습니다.");
            return;
        }

        Require(node.NextNodeIds.Count > 0, map.Seed, $"막다른 노드: {node.Id}");
        foreach (var targetId in node.NextNodeIds)
        {
            var target = map.GetNode(targetId);
            Require(target.Stage == node.Stage + 1, map.Seed, "인접하지 않은 단계가 연결되었습니다.");
            Visit(map, target, battles, utilities, visited, utilityStages);
        }
    }

    private static string Signature(RouteMap map) => string.Join("|", map.Nodes.Select(node =>
        $"{node.Id}:{node.Stage}:{node.Type}:{string.Join(",", node.NextNodeIds)}"));

    private static void Require(bool condition, uint seed, string message)
    {
        if (!condition) throw new InvalidOperationException($"시드 {seed}: {message}");
    }
}
