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

        internal RouteNode(string id, int stage, RouteNodeType type)
        {
            Id = id;
            Stage = stage;
            Type = type;
        }

        internal void ConnectTo(RouteNode target)
        {
            if (target.Stage != Stage + 1)
                throw new InvalidOperationException("인접한 다음 단계의 노드만 연결할 수 있습니다.");
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
