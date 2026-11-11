using System;
using System.Collections.Generic;

namespace DuelHero.Map
{
    public static class RouteMapGenerator
    {
        public const int RulesVersion = 6;
        private const int LimitedConnectionsRulesVersion = 5;
        private const int AdjacentRulesVersion = 4;
        private const int LegacyRulesVersion = 3;

        internal static bool SupportsRulesVersion(int version)
            => version >= LegacyRulesVersion && version <= RulesVersion;

        public static uint CreateSeed() => BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);

        public static RouteMap Generate(uint seed) => Generate(seed, RulesVersion);

        internal static RouteMap Generate(uint seed, int rulesVersion)
        {
            if (!SupportsRulesVersion(rulesVersion))
                throw new ArgumentOutOfRangeException(nameof(rulesVersion));

            bool adjacentOnly = rulesVersion >= AdjacentRulesVersion;
            bool limitConnections = rulesVersion >= LimitedConnectionsRulesVersion;
            bool limitCrossings = rulesVersion == RulesVersion;
            int trimmedSide = (seed & 1) == 0 ? 0 : 2;
            var random = new SeededRandom(seed);
            bool fixedShopOnLeft = limitCrossings && random.Next(2) == 0;
            var start = new RouteNode("stage-1", 1, RouteNodeType.Battle);
            var boss = new RouteNode("stage-5", 5, RouteNodeType.Boss);
            var nodes = new List<RouteNode> { start };
            var previous = new List<(RouteNode Node, int Battles, int Lane)> { (start, 1, 1) };
            var types = new[] { RouteNodeType.Battle, RouteNodeType.Shop, RouteNodeType.Rest };

            for (int stage = 2; stage <= 4; stage++)
            {
                if (adjacentOnly)
                {
                    // A central battle stays reachable from both utility lanes.
                    bool shopOnLeft = limitCrossings ? fixedShopOnLeft : random.Next(2) == 0;
                    types[0] = shopOnLeft ? RouteNodeType.Shop : RouteNodeType.Rest;
                    types[1] = RouteNodeType.Battle;
                    types[2] = shopOnLeft ? RouteNodeType.Rest : RouteNodeType.Shop;
                }
                else
                    for (int index = types.Length - 1; index > 0; index--)
                    {
                        int other = random.Next(index + 1);
                        (types[index], types[other]) = (types[other], types[index]);
                    }

                var current = new List<(RouteNode Node, int Battles, int Lane)>();
                var byTypeAndBattles = new Dictionary<(RouteNodeType Type, int Battles), RouteNode>();
                foreach (var source in previous)
                {
                    for (int lane = 0; lane < types.Length; lane++)
                    {
                        if (adjacentOnly && Math.Abs(source.Lane - lane) > 1) continue;
                        // Removing one outward diagonal breaks one of the two X pairs.
                        int sideToTrim = limitCrossings && stage == 4 ? 2 - trimmedSide : trimmedSide;
                        if (limitConnections && stage >= 3 && lane == sideToTrim)
                        {
                            int trimmedSource = limitCrossings || stage == 3 ? 1 : trimmedSide;
                            if (source.Lane == trimmedSource) continue;
                        }
                        var type = types[lane];
                        int battles = source.Battles + (type == RouteNodeType.Battle ? 1 : 0);
                        int remaining = 4 - stage;
                        if (battles > 3 || battles + remaining < 2) continue;

                        // Before the final choice, only merge paths with equal battle counts.
                        // On stage 4 every valid node has the same continuation: the boss.
                        var key = (type, stage == 4 ? 0 : battles);
                        if (!byTypeAndBattles.TryGetValue(key, out var target))
                        {
                            target = new RouteNode($"stage-{stage}-lane-{current.Count + 1}", stage, type);
                            byTypeAndBattles.Add(key, target);
                            current.Add((target, battles, lane));
                        }
                        source.Node.ConnectTo(target);
                    }
                }
                // RouteMapView uses each type's first occurrence as its visual lane.
                if (adjacentOnly)
                    current.Sort((left, right) => left.Lane.CompareTo(right.Lane));
                foreach (var entry in current)
                    nodes.Add(entry.Node);
                previous = current;
            }

            nodes.Add(boss);
            foreach (var source in previous)
                source.Node.ConnectTo(boss);

            return new RouteMap(seed, rulesVersion, nodes, start.Id, boss.Id);
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
