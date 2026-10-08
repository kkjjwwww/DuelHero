using System.Collections.Generic;
using UnityEngine;

namespace DuelHero.Battle
{
    // Board membership is independent of action plans and turn execution.
    public sealed class BoardOccupancy : MonoBehaviour
    {
        public const int MaxUnits = 4;
        [SerializeField] private GridMovement[] initialUnits = new GridMovement[0];
        private readonly List<GridMovement> units = new();
        private bool initialized;
        public IReadOnlyList<GridMovement> Units { get { Initialize(); return units.AsReadOnly(); } }
        private void Awake() => Initialize();
        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            foreach (var unit in initialUnits)
                if (unit != null && !RegisterUnit(unit)) Debug.LogError("전장에 등록할 수 있는 유닛은 플레이어 포함 최대 4명입니다.", unit);
        }
        public bool RegisterUnit(GridMovement unit)
        {
            Initialize();
            if (unit == null) return false;
            units.RemoveAll(registered => registered == null);
            if (units.Contains(unit)) return true;
            if (units.Count >= MaxUnits) return false;
            if (unit.Board != null && unit.Board != this) unit.Board.UnregisterUnit(unit);
            units.Add(unit);
            unit.AttachBoard(this);
            return true;
        }
        public void UnregisterUnit(GridMovement unit)
        {
            units.Remove(unit);
        }
        public int CountAt(Vector2Int cell, GridMovement excluding = null)
        {
            Initialize();
            int count = 0;
            foreach (var unit in units)
                if (unit != null && unit != excluding && unit.isActiveAndEnabled && unit.gameObject.activeInHierarchy && unit.GridPosition == cell) count++;
            return count;
        }
    }
}
