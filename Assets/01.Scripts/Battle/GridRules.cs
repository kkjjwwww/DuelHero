using UnityEngine;
namespace DuelHero.Battle
{
    public static class GridRules
    {
        public static bool Contains(Vector2Int cell, int width, int height) =>
            cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
        public static Vector2Int Move(Vector2Int origin, Vector2Int direction, int steps, int width, int height)
        {
            for (int i = 0; i < steps; i++)
            {
                var next = origin + direction;
                if (!Contains(next, width, height)) break;
                origin = next;
            }
            return origin;
        }
    }
}
