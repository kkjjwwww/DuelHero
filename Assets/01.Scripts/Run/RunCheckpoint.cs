using System;
using System.IO;
using System.Linq;
using DuelHero.Map;
using UnityEngine;

namespace DuelHero.Run
{
    public static class RunCheckpoint
    {
        private const int FormatVersion = 1;
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "run-checkpoint.json");

        [Serializable]
        private sealed class Data
        {
            public int formatVersion;
            public int mapRulesVersion;
            public uint seed;
            public int health;
            public int maxHealth;
            public int gold;
            public string[] cardIds;
            public string[] completedNodeIds;
        }

        public static void Save(RunSession session) => Save(session, DefaultPath);

        public static void Save(RunSession session, string path)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.HasPendingNode || session.PlayerHealth <= 0 || session.Progress.CompletedNodeIds.Count == 0)
                throw new InvalidOperationException("완료된 전투 체크포인트만 저장할 수 있습니다.");
            var last = session.Map.GetNode(session.Progress.CurrentNodeId).Type;
            if (last != RouteNodeType.Battle && last != RouteNodeType.Boss)
                throw new InvalidOperationException("전투 완료 직후에만 저장할 수 있습니다.");

            var data = new Data
            {
                formatVersion = FormatVersion, mapRulesVersion = session.Map.RulesVersion,
                seed = session.Map.Seed, health = session.PlayerHealth, maxHealth = session.PlayerMaxHealth,
                gold = session.Gold, cardIds = session.CardIds.ToArray(),
                completedNodeIds = session.Progress.CompletedNodeIds.ToArray()
            };
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(data));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static RunSession Load() => Load(DefaultPath);

        public static RunSession Load(string path)
        {
            var data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
            if (data == null || data.formatVersion != FormatVersion ||
                !RouteMapGenerator.SupportsRulesVersion(data.mapRulesVersion) ||
                data.cardIds == null || data.cardIds.Length == 0 || data.completedNodeIds == null || data.completedNodeIds.Length == 0)
                throw new InvalidDataException("지원하지 않거나 손상된 런 저장 데이터입니다.");
            var map = RouteMapGenerator.Generate(data.seed, data.mapRulesVersion);
            var session = new RunSession(map, data.health, data.maxHealth, data.cardIds, data.gold);
            foreach (var id in data.completedNodeIds)
                if (!session.Progress.TrySelect(id))
                    throw new InvalidDataException("저장된 맵 경로가 유효하지 않습니다.");
            var last = session.Map.GetNode(session.Progress.CurrentNodeId).Type;
            if (last != RouteNodeType.Battle && last != RouteNodeType.Boss)
                throw new InvalidDataException("저장된 위치가 전투 완료 지점이 아닙니다.");
            return session;
        }
    }
}
