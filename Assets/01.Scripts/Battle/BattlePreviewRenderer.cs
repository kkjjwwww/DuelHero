using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace DuelHero.Battle
{
    // Display only. No colliders, GridMovement, or BoardOccupancy registrations.
    public sealed class BattlePreviewRenderer : MonoBehaviour
    {
        [SerializeField] private GridMovement player;
        [SerializeField] private SpriteRenderer playerSprite;
        [SerializeField, Range(0.5f, 1f)] private float attackCellSize = .94f;
        [SerializeField] private Color reservedAttackColor = new Color(1f, .04f, .04f, .3f);
        [SerializeField] private Color focusedAttackColor = new Color(1f, .02f, .02f, .6f);
        [SerializeField] private Color pathColor = new Color(1f, 1f, 1f, .85f);
        [SerializeField, Range(0f, 1f)] private float ghostAlpha = .35f;
        [SerializeField, Min(.1f)] private float pulseHalfPeriod = .4f;
        private readonly List<SpriteRenderer> reservedCells = new();
        private readonly List<SpriteRenderer> focusedCells = new();
        private readonly List<SpriteRenderer> pathDots = new();
        private readonly List<SpriteRenderer> ghosts = new();
        private Tween pulse;
        private Sprite whiteSprite;
        private Texture2D whiteTexture;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            whiteTexture.SetPixel(0, 0, Color.white); whiteTexture.Apply();
            whiteSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        }
        public void Show(IReadOnlyList<CardPreviewResult> reserved, CardPreviewResult focus)
        {
            if (whiteSprite == null || player == null || playerSprite == null) { Clear(); return; }
            Clear();
            var cells = new HashSet<Vector2Int>();
            foreach (var card in reserved.Where(c => c.CanExecute))
            {
                foreach (var cell in card.AttackCells) cells.Add(cell);
                DrawMovement(card, false);
            }
            int index = 0;
            foreach (var cell in cells.OrderBy(c => c.y).ThenBy(c => c.x))
                DrawCell(Get(reservedCells, index++, "ReservedAttack"), cell, reservedAttackColor, .012f);
            if (focus == null || !focus.CanExecute) return;
            if (focus.Slot >= reserved.Count) DrawMovement(focus, true);
            index = 0;
            foreach (var cell in focus.AttackCells)
                DrawCell(Get(focusedCells, index++, "FocusedAttack"), cell, focusedAttackColor, .018f);
            if (index > 0)
            {
                // One tween drives every focused tile so their blinking stays synchronized.
                float alpha = focusedAttackColor.a;
                pulse = DOTween.To(() => alpha, value =>
                {
                    alpha = value;
                    foreach (var tile in focusedCells.Where(t => t.gameObject.activeSelf))
                    { var color = tile.color; color.a = value; tile.color = color; }
                }, focusedAttackColor.a * .2f, pulseHalfPeriod).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
        }
        private void DrawMovement(CardPreviewResult card, bool focused)
        {
            var path = card.MovementPath;
            if (path.Count < 2) return;
            float cellSize = player.CellSize;
            for (int step = 1; step < path.Count; step++)
            {
                Vector3 from = World(path[step - 1], .03f), to = World(path[step], .03f);
                for (int dot = 1; dot <= 5; dot++)
                {
                    var sprite = Get(pathDots, ActiveCount(pathDots), "PathDot");
                    sprite.sprite = whiteSprite;
                    sprite.transform.position = Vector3.Lerp(from, to, dot / 6f);
                    sprite.transform.rotation = Quaternion.Euler(-90, 0, 0);
                    sprite.transform.localScale = Vector3.one * cellSize * .075f;
                    sprite.color = focused ? Color.white : pathColor;
                }
            }
            var ghost = Get(ghosts, ActiveCount(ghosts), "DestinationGhost");
            ghost.sprite = playerSprite.sprite;
            ghost.transform.rotation = playerSprite.transform.rotation;
            ghost.transform.position = World(card.EndPosition, 0) + (playerSprite.transform.position - player.transform.position);
            ghost.transform.localScale = playerSprite.transform.lossyScale;
            ghost.flipX = playerSprite.flipX;
            ghost.sortingLayerID = playerSprite.sortingLayerID;
            ghost.sortingOrder = playerSprite.sortingOrder + 1;
            ghost.color = new Color(1, 1, 1, ghostAlpha);
        }
        private void DrawCell(SpriteRenderer sprite, Vector2Int cell, Color color, float elevation)
        {
            sprite.sprite = whiteSprite;
            sprite.transform.position = World(cell, elevation);
            sprite.transform.rotation = Quaternion.Euler(-90, 0, 0);
            sprite.transform.localScale = new Vector3(player.CellSize * attackCellSize, player.CellSize * attackCellSize, 1);
            sprite.color = color;
        }
        private Vector3 World(Vector2Int cell, float elevation) =>
            player.transform.position - new Vector3(player.GridPosition.x * player.CellSize, 0, player.GridPosition.y * player.CellSize)
            + new Vector3(cell.x * player.CellSize, elevation, cell.y * player.CellSize);
        private SpriteRenderer Get(List<SpriteRenderer> pool, int index, string name)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                pool.Add(go.AddComponent<SpriteRenderer>());
            }
            var result = pool[index]; result.gameObject.SetActive(true); return result;
        }
        private static int ActiveCount(List<SpriteRenderer> pool) => pool.Count(r => r.gameObject.activeSelf);
        public void Clear()
        {
            pulse?.Kill(); pulse = null;
            foreach (var pool in new[] { reservedCells, focusedCells, pathDots, ghosts })
                foreach (var sprite in pool) if (sprite != null) sprite.gameObject.SetActive(false);
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            pulse?.Kill();
            if (whiteSprite != null) Destroy(whiteSprite);
            if (whiteTexture != null) Destroy(whiteTexture);
        }
    }
}
