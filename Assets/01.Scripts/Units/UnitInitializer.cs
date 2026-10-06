using System;
using DuelHero.Data;
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
        public string LastError { get; private set; }
        private void Awake()
        {
            if (!TryInitialize()) Debug.LogError(LastError, this);
        }
        public bool TryInitialize()
        {
            if (stats == null || database == null) { LastError = "UnitInitializer의 DB 또는 상태 참조가 없습니다."; return false; }
            if (stats.IsInitialized) return true;
            var definitions = dataKind == UnitDataKind.Player ? database.players : database.enemies;
            var definition = Array.Find(definitions, unit => unit.id == unitId);
            if (definition == null) { LastError = "유닛 ID를 찾을 수 없습니다: " + unitId; return false; }
            try { stats.Initialize(definition.startingHealth, definition.maxHealth, definition.startingEnergy, definition.maxEnergy); LastError = null; return true; }
            catch (ArgumentOutOfRangeException) { LastError = unitId + ": 시작·최대 체력/에너지 값이 올바르지 않습니다."; return false; }
        }
    }
}
