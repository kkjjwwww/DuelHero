using UnityEngine;

namespace DuelHero.Battle
{
    public enum GridTerrain { Floor, LowObstacle }

    public sealed class GridCell : MonoBehaviour
    {
        [SerializeField] private Vector2Int coordinate;
        [SerializeField] private GridTerrain terrain;
        public Vector2Int Coordinate => coordinate;
        public GridTerrain Terrain => terrain;
        private void OnEnable() => GetComponentInParent<BattleGrid>()?.Invalidate();
        private void OnDisable() => GetComponentInParent<BattleGrid>()?.Invalidate();
        private void OnValidate() => GetComponentInParent<BattleGrid>()?.Invalidate();
    }
}
