using System;
using DuelHero.Data;
using DuelHero.Run;
using UnityEngine;
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
        [SerializeField] private UnityEngine.UI.Text encounterNameLabel;
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
            if (encounterNameLabel != null && EncounterName is string name) encounterNameLabel.text = name;
        }
        public bool TryInitialize()
        {
            if (stats == null || database == null) { LastError = "UnitInitializer의 DB 또는 상태 참조가 없습니다."; return false; }
            if (stats.IsInitialized) return true;
            var definitions = dataKind == UnitDataKind.Player ? database.players : database.enemies;
            var definition = Array.Find(definitions, unit => unit.id == (Encounter?.unitId ?? unitId));
            if (definition == null) { LastError = "유닛 ID를 찾을 수 없습니다: " + unitId; return false; }
            var run = Application.isPlaying && dataKind == UnitDataKind.Player ? RunSessionStore.Current : null;
            bool fromRun = run?.PendingBattleNodeId != null;
            int health = fromRun ? run.PlayerHealth : definition.startingHealth;
            int maxHealth = fromRun ? run.PlayerMaxHealth : definition.maxHealth;
            int energy = fromRun ? definition.maxEnergy : definition.startingEnergy;
            try { stats.Initialize(health, maxHealth, energy, definition.maxEnergy); LastError = null; return true; }
            catch (ArgumentOutOfRangeException) { LastError = unitId + ": 시작·최대 체력/에너지 값이 올바르지 않습니다."; return false; }
        }
    }
}
