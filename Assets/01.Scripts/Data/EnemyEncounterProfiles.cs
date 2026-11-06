using System;
using DuelHero.Map;
using DuelHero.Run;
using UnityEngine;

namespace DuelHero.Data
{
    [Serializable]
    public sealed class EnemyEncounterProfile
    {
        public string unitId;
        public string[] fixedCards, bonusCandidates;
        public Sprite idle, attackBefore, attackAfter, jumpGround, jumpAir, block;
    }

    public sealed class EnemyEncounterProfiles : ScriptableObject
    {
        [SerializeField] private EnemyEncounterProfile warden, hunter, boss;

        public bool IsBoss(string unitId) => boss != null && boss.unitId == unitId;

        public EnemyEncounterProfile FindByUnitId(string unitId)
        {
            if (warden?.unitId == unitId) return warden;
            if (hunter?.unitId == unitId) return hunter;
            return boss?.unitId == unitId ? boss : null;
        }

        public EnemyEncounterProfile Resolve(RunSession run)
        {
            if (run?.PendingBattleNodeId == null) return warden;
            var node = run.Map.GetNode(run.PendingBattleNodeId);
            if (node.Type == RouteNodeType.Boss) return boss;
            int previousBattles = 0;
            foreach (var id in run.Progress.CompletedNodeIds)
                if (run.Map.GetNode(id).Type == RouteNodeType.Battle) previousBattles++;
            return ((run.Map.Seed + (uint)previousBattles) & 1) == 0 ? warden : hunter;
        }
    }
}
