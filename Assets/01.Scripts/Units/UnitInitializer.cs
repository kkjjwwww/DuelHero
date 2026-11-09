using System;
using DuelHero.Data;
using DuelHero.Map;
using DuelHero.Run;
using UnityEngine;
using UnityEngine.Serialization;
namespace DuelHero.Units
{
    public enum UnitDataKind { Player, Enemy }
    public sealed class UnitInitializer : MonoBehaviour
    {
        [SerializeField] private UnitDatabase database;
        [SerializeField] private UnitDataKind dataKind;
        [SerializeField] private string unitId;
        [SerializeField] private UnitStats stats;
        [SerializeField] private EnemyEncounterProfiles encounterProfiles;
        [SerializeField] private BattleConfig battleConfig;
        [FormerlySerializedAs("encounterNameLabel")]
        [SerializeField] private UnityEngine.UI.Text unitNameLabel;
        private EnemyEncounterProfile encounter;
        public EnemyEncounterProfile Encounter => encounterProfiles == null ? null :
            encounter ??= encounterProfiles.Resolve(RunSessionStore.Current);
        public string SceneUnitId => unitId;
        public string EncounterName => Encounter == null || database == null ? null :
            Array.Find(database.enemies, unit => unit.id == Encounter.unitId)?.name;
        public string LastError { get; private set; }
        private void Awake()
        {
            if (!TryInitialize()) Debug.LogError(LastError, this);
        }
        public bool TryInitialize()
        {
            if (stats == null || database == null) { LastError = "UnitInitializer의 DB 또는 상태 참조가 없습니다."; return false; }
            var definitions = dataKind == UnitDataKind.Player ? database.players : database.enemies;
            var definition = Array.Find(definitions, unit => unit.id == (Encounter?.unitId ?? unitId));
            if (unitNameLabel != null && !string.IsNullOrEmpty(definition?.name))
                unitNameLabel.text = definition.name;
            if (stats.IsInitialized) return true;
            if (definition == null) { LastError = "유닛 ID를 찾을 수 없습니다: " + unitId; return false; }
            var run = Application.isPlaying ? RunSessionStore.Current : null;
            bool fromRun = dataKind == UnitDataKind.Player && run?.PendingBattleNodeId != null;
            int health = fromRun ? run.PlayerHealth : definition.startingHealth;
            int maxHealth = fromRun ? run.PlayerMaxHealth : definition.maxHealth;
            int energy = fromRun ? definition.maxEnergy : definition.startingEnergy;
            int maxEnergy = definition.maxEnergy;
            if (dataKind == UnitDataKind.Enemy && battleConfig != null && run?.PendingBattleNodeId != null)
            {
                var node = run.Map.GetNode(run.PendingBattleNodeId);
                if (node.Type == RouteNodeType.Battle)
                {
                    int steps = node.Stage - 1;
                    decimal healthPercent = 100m + steps * (decimal)battleConfig.enemyHealthIncreasePercentPerStage;
                    decimal energyPercent = 100m + steps * (decimal)battleConfig.enemyEnergyIncreasePercentPerStage;
                    health = ScaleStat(health, healthPercent);
                    maxHealth = ScaleStat(maxHealth, healthPercent);
                    energy = ScaleStat(energy, energyPercent);
                    maxEnergy = ScaleStat(maxEnergy, energyPercent);
                }
            }
            try { stats.Initialize(health, maxHealth, energy, maxEnergy); LastError = null; return true; }
            catch (ArgumentOutOfRangeException) { LastError = unitId + ": 시작·최대 체력/에너지 값이 올바르지 않습니다."; return false; }
        }

        private static int ScaleStat(int value, decimal percent) =>
            (int)Math.Min(int.MaxValue, Math.Round(value * percent / 100m, MidpointRounding.AwayFromZero));
    }
}
