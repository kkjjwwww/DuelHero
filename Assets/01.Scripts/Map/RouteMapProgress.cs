using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DuelHero.Map
{
    public sealed class RouteMapProgress
    {
        private readonly RouteMap map;
        private readonly List<string> completedNodeIds = new List<string>();
        private readonly ReadOnlyCollection<string> completedView;

        // In the map-only preview, selecting a node completes it immediately.
        // Null means the first battle has not been selected yet.
        public string CurrentNodeId { get; private set; }
        public IReadOnlyList<string> CompletedNodeIds => completedView;
        public bool IsComplete => CurrentNodeId == map.BossNodeId;

        public RouteMapProgress(RouteMap map)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            completedView = completedNodeIds.AsReadOnly();
        }

        public bool CanSelect(string nodeId)
        {
            if (IsComplete || nodeId == null || completedNodeIds.Contains(nodeId)) return false;
            if (CurrentNodeId == null) return nodeId == map.StartNodeId;

            foreach (var nextId in map.GetNode(CurrentNodeId).NextNodeIds)
                if (nextId == nodeId) return true;
            return false;
        }

        public bool TrySelect(string nodeId)
        {
            if (!CanSelect(nodeId)) return false;
            CurrentNodeId = nodeId;
            completedNodeIds.Add(nodeId);
            return true;
        }
    }
}
