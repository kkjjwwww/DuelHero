using System;
using System.Collections.Generic;

namespace DuelHero.Map
{
    public static class RouteMapGenerator
    {
        public const int RulesVersion = 8;
        private const int MergedLayoutRulesVersion = 7;
        private const int LimitedCrossingsRulesVersion = 6;
        private const int LimitedConnectionsRulesVersion = 5;
        private const int AdjacentRulesVersion = 4;
        private const int LegacyRulesVersion = 3;

        // Rows use Battle, Shop and Rest initials. Each destination string
        // lists zero-based lanes reachable from one source lane, left to right.
        private static readonly (string[] Rows, string[] IntoThird, string[] IntoFourth)[] DirectLayouts =
        {
            (new[] { "BSR", "BRS", "SBR" }, new[] { "1", "0", "2" }, new[] { "01", "12", "1" }),
            (new[] { "BRS", "SRB", "SBR" }, new[] { "0", "12", "2" }, new[] { "0", "1", "12" }),
            (new[] { "SBR", "SRB", "BSR" }, new[] { "01", "01", "2" }, new[] { "0", "0", "12" }),
            (new[] { "SRB", "RSB", "SBR" }, new[] { "0", "02", "1" }, new[] { "1", "01", "12" }),
            (new[] { "RBS", "SBR", "SRB" }, new[] { "1", "01", "2" }, new[] { "0", "01", "2" }),
            (new[] { "RSB", "SBR", "BSR" }, new[] { "01", "1", "12" }, new[] { "0", "12", "1" })
        };

        // Each layout gives the left-to-right order of stages 2–4 and one
        // omitted diagonal (from lane, to lane) for each interior boundary.
        private static readonly (RouteNodeType[][] Rows, (int From, int To) IntoThird,
            (int From, int To) IntoFourth)[] Layouts =
        {
            (new[] { new[] { RouteNodeType.Battle, RouteNodeType.Shop, RouteNodeType.Rest }, new[] { RouteNodeType.Rest, RouteNodeType.Battle, RouteNodeType.Shop }, new[] { RouteNodeType.Shop, RouteNodeType.Battle, RouteNodeType.Rest } }, (1, 0), (0, 1)),
            (new[] { new[] { RouteNodeType.Battle, RouteNodeType.Rest, RouteNodeType.Shop }, new[] { RouteNodeType.Shop, RouteNodeType.Battle, RouteNodeType.Rest }, new[] { RouteNodeType.Shop, RouteNodeType.Rest, RouteNodeType.Battle } }, (1, 0), (1, 2)),
            (new[] { new[] { RouteNodeType.Shop, RouteNodeType.Battle, RouteNodeType.Rest }, new[] { RouteNodeType.Rest, RouteNodeType.Shop, RouteNodeType.Battle }, new[] { RouteNodeType.Rest, RouteNodeType.Battle, RouteNodeType.Shop } }, (2, 1), (1, 2)),
            (new[] { new[] { RouteNodeType.Shop, RouteNodeType.Rest, RouteNodeType.Battle }, new[] { RouteNodeType.Battle, RouteNodeType.Rest, RouteNodeType.Shop }, new[] { RouteNodeType.Rest, RouteNodeType.Shop, RouteNodeType.Battle } }, (1, 2), (2, 1)),
            (new[] { new[] { RouteNodeType.Rest, RouteNodeType.Battle, RouteNodeType.Shop }, new[] { RouteNodeType.Battle, RouteNodeType.Shop, RouteNodeType.Rest }, new[] { RouteNodeType.Rest, RouteNodeType.Shop, RouteNodeType.Battle } }, (1, 2), (0, 1)),
            (new[] { new[] { RouteNodeType.Rest, RouteNodeType.Shop, RouteNodeType.Battle }, new[] { RouteNodeType.Rest, RouteNodeType.Battle, RouteNodeType.Shop }, new[] { RouteNodeType.Shop, RouteNodeType.Battle, RouteNodeType.Rest } }, (1, 0), (1, 2))
        };

        internal static bool SupportsRulesVersion(int version)
            => version >= LegacyRulesVersion && version <= RulesVersion;

        public static uint CreateSeed() => BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);

        public static RouteMap Generate(uint seed) => Generate(seed, RulesVersion);

        internal static RouteMap Generate(uint seed, int rulesVersion)
        {
            if (!SupportsRulesVersion(rulesVersion))
                throw new ArgumentOutOfRangeException(nameof(rulesVersion));
            if (rulesVersion == RulesVersion)
                return GenerateDirectMap(seed);

            bool adjacentOnly = rulesVersion >= AdjacentRulesVersion;
            bool limitConnections = rulesVersion >= LimitedConnectionsRulesVersion;
            bool limitCrossings = rulesVersion >= LimitedCrossingsRulesVersion;
            bool useLayout = rulesVersion == MergedLayoutRulesVersion;
            int trimmedSide = (seed & 1) == 0 ? 0 : 2;
            var random = new SeededRandom(seed);
            var layout = useLayout ? Layouts[random.Next(Layouts.Length)] : default;
            bool fixedShopOnLeft = !useLayout && limitCrossings && random.Next(2) == 0;
            var start = new RouteNode("stage-1", 1, RouteNodeType.Battle);
            var boss = new RouteNode("stage-5", 5, RouteNodeType.Boss);
            var nodes = new List<RouteNode> { start };
            var previous = new List<(RouteNode Node, int Battles, int Lane)> { (start, 1, 1) };
            var types = new[] { RouteNodeType.Battle, RouteNodeType.Shop, RouteNodeType.Rest };

            for (int stage = 2; stage <= 4; stage++)
            {
                if (useLayout)
                    types = layout.Rows[stage - 2];
                else if (adjacentOnly)
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
                        if (useLayout)
                        {
                            var omitted = stage == 3 ? layout.IntoThird : layout.IntoFourth;
                            if (stage >= 3 && source.Lane == omitted.From && lane == omitted.To) continue;
                        }
                        else if (limitConnections && stage >= 3)
                        {
                            int sideToTrim = limitCrossings && stage == 4 ? 2 - trimmedSide : trimmedSide;
                            int trimmedSource = limitCrossings || stage == 3 ? 1 : trimmedSide;
                            if (source.Lane == trimmedSource && lane == sideToTrim) continue;
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

        private static RouteMap GenerateDirectMap(uint seed)
        {
            var random = new SeededRandom(seed);
            var layout = DirectLayouts[random.Next(DirectLayouts.Length)];
            var start = new RouteNode("stage-1", 1, RouteNodeType.Battle);
            var boss = new RouteNode("stage-5", 5, RouteNodeType.Boss);
            var nodes = new List<RouteNode> { start };
            var rows = new RouteNode[3, 3];
            for (int row = 0; row < 3; row++)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    var type = layout.Rows[row][lane] switch
                    {
                        'B' => RouteNodeType.Battle,
                        'S' => RouteNodeType.Shop,
                        'R' => RouteNodeType.Rest,
                        _ => throw new InvalidOperationException("잘못된 맵 노드 종류입니다.")
                    };
                    var node = new RouteNode($"stage-{row + 2}-lane-{lane + 1}", row + 2, type);
                    rows[row, lane] = node;
                    nodes.Add(node);
                }
            }

            for (int lane = 0; lane < 3; lane++)
            {
                start.ConnectTo(rows[0, lane]);
                foreach (char target in layout.IntoThird[lane])
                    rows[0, lane].ConnectTo(rows[1, target - '0']);
                foreach (char target in layout.IntoFourth[lane])
                    rows[1, lane].ConnectTo(rows[2, target - '0']);
                rows[2, lane].ConnectTo(boss);
            }
            nodes.Add(boss);
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
