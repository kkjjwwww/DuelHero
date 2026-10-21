using System;
using System.Collections.Generic;

namespace DuelHero.Map
{
    public enum RouteNodeType { Battle, Rest, Shop, Boss }

    public sealed class RouteNode
    {
        private readonly List<string> nextNodeIds = new List<string>();

        public string Id { get; }
        public int Stage { get; }
        public RouteNodeType Type { get; }
        public IReadOnlyList<string> NextNodeIds => nextNodeIds;

        // Used while generating links. A route may only join another route with
        // the same number of rest/shop visits at that point.
        internal int UtilityVisitsBefore { get; }
        internal int UtilityVisitsAfter => UtilityVisitsBefore +
            (Type == RouteNodeType.Rest || Type == RouteNodeType.Shop ? 1 : 0);

        internal RouteNode(string id, int stage, RouteNodeType type, int utilityVisitsBefore)
        {
            Id = id;
            Stage = stage;
            Type = type;
            UtilityVisitsBefore = utilityVisitsBefore;
        }

        internal void ConnectTo(RouteNode target)
        {
            if (target.Stage != Stage + 1 || UtilityVisitsAfter != target.UtilityVisitsBefore)
                throw new InvalidOperationException("경로의 단계 또는 휴식·상점 방문 횟수가 맞지 않습니다.");
            if (!nextNodeIds.Contains(target.Id)) nextNodeIds.Add(target.Id);
        }
    }

    public sealed class RouteMap
    {
        private readonly List<RouteNode> nodes;
        private readonly Dictionary<string, RouteNode> nodesById;

        public uint Seed { get; }
        public int RulesVersion { get; }
        public string StartNodeId { get; }
        public string BossNodeId { get; }
        public IReadOnlyList<RouteNode> Nodes => nodes;

        internal RouteMap(uint seed, int rulesVersion, List<RouteNode> nodes,
            string startNodeId, string bossNodeId)
        {
            Seed = seed;
            RulesVersion = rulesVersion;
            this.nodes = nodes;
            StartNodeId = startNodeId;
            BossNodeId = bossNodeId;
            nodesById = new Dictionary<string, RouteNode>();
            foreach (var node in nodes) nodesById.Add(node.Id, node);
        }

        public RouteNode GetNode(string id) => nodesById[id];
    }
}
