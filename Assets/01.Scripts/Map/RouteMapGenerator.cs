using System;
using System.Collections.Generic;

namespace DuelHero.Map
{
    public static class RouteMapGenerator
    {
        public const int RulesVersion = 2;

        public static uint CreateSeed() => BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);

        public static RouteMap Generate(uint seed)
        {
            var random = new SeededRandom(seed);
            int laneCount = 2 + random.Next(2);
            var utilityStages = new int[laneCount];
            utilityStages[0] = 2 + random.Next(3);
            utilityStages[1] = 2 + (utilityStages[0] - 1 + random.Next(2)) % 3;
            for (int lane = 2; lane < laneCount; lane++)
                utilityStages[lane] = 2 + random.Next(3);

            var nodes = new List<RouteNode>();
            var start = new RouteNode("stage-1", 1, RouteNodeType.Battle);
            nodes.Add(start);
            var lanes = new RouteNode[laneCount, 3];
            for (int stage = 2; stage <= 4; stage++)
            {
                for (int lane = 0; lane < laneCount; lane++)
                {
                    bool utility = utilityStages[lane] == stage;
                    RouteNodeType type = utility
                        ? (random.Next(2) == 0 ? RouteNodeType.Rest : RouteNodeType.Shop)
                        : RouteNodeType.Battle;
                    var node = new RouteNode($"stage-{stage}-lane-{lane + 1}", stage, type);
                    lanes[lane, stage - 2] = node;
                    nodes.Add(node);
                }
            }

            var boss = new RouteNode("stage-5", 5, RouteNodeType.Boss);
            nodes.Add(boss);
            for (int lane = 0; lane < laneCount; lane++)
            {
                start.ConnectTo(lanes[lane, 0]);
                lanes[lane, 0].ConnectTo(lanes[lane, 1]);
                lanes[lane, 1].ConnectTo(lanes[lane, 2]);
                lanes[lane, 2].ConnectTo(boss);
            }

            // Cross-lane links add optional branches and merges. Matching visit
            // counts makes every possible path include exactly one utility node.
            for (int stage = 2; stage <= 3; stage++)
            {
                for (int fromLane = 0; fromLane < laneCount; fromLane++)
                {
                    for (int toLane = 0; toLane < laneCount; toLane++)
                    {
                        if (fromLane == toLane || random.Next(2) == 0) continue;
                        var source = lanes[fromLane, stage - 2];
                        var target = lanes[toLane, stage - 1];
                        bool sourceVisitedUtility = utilityStages[fromLane] <= stage;
                        bool targetRequiresUtilityVisit = utilityStages[toLane] < stage + 1;
                        if (sourceVisitedUtility == targetRequiresUtilityVisit)
                            source.ConnectTo(target);
                    }
                }
            }

            return new RouteMap(seed, RulesVersion, nodes, start.Id, boss.Id);
        }

        // Explicit PRNG keeps the map stable across .NET/Unity runtime changes.
        private struct SeededRandom
        {
            private uint state;

            public SeededRandom(uint seed) => state = seed;

            public int Next(int exclusiveMax)
            {
                if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
                state = unchecked(state + 0x9E3779B9u);
                uint value = state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return (int)(value % (uint)exclusiveMax);
            }
        }
    }
}
