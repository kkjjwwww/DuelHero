using System;
using System.Collections;
using DuelHero.Map;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DuelHero.Battle
{
    public sealed class BattleTutorial : MonoBehaviour
    {
        private const string SeenKey = "DuelHero.BattleTutorialSeen";

        [Serializable]
        private sealed class Step
        {
            public RectTransform[] targets;
            public bool highlightGrid;
        }

        [SerializeField] private Step[] steps;
        [SerializeField] private BattleGrid grid;
        [SerializeField] private Canvas battleCanvas;
        [SerializeField] private BattleTutorialView overlayPrefab;
        [SerializeField] private Camera battleCamera;
        [SerializeField, Min(0f)] private float clickDelay = .4f;
        [SerializeField, Min(0f)] private float cornerRadius = 12f;
        [SerializeField, Min(0f)] private float padding = 8f;
#if UNITY_EDITOR
        [SerializeField] private bool previewInEditor;
#endif

        private readonly Vector3[] worldCorners = new Vector3[4];
        private readonly Vector2[] hole = new Vector2[4];
        private RectTransform overlay;
        private BattleTutorialMask mask;
        private BattleTutorialView view;
        private EventSystem eventSystem;
        private bool navigationEnabled;
        private int stepIndex;
        private float nextClickTime;
        private bool previewSession;

        private IEnumerator Start()
        {
#if UNITY_EDITOR
            previewSession = previewInEditor;
#endif
            if ((!previewSession && PlayerPrefs.GetInt(SeenKey, 0) != 0) || steps == null || steps.Length == 0 ||
                battleCanvas == null || overlayPrefab == null || overlayPrefab.StepCount != steps.Length) yield break;
            yield return null;
            while (MapSceneFade.IsInProgress) yield return null;
            if (!isActiveAndEnabled) yield break;

            CreateOverlay();
            ShowStep();
        }

        private void CreateOverlay()
        {
            view = Instantiate(overlayPrefab, battleCanvas.transform, false);
            overlay = (RectTransform)view.transform;
            mask = view.Mask;
            view.AdvanceButton.onClick.AddListener(Advance);
            view.SkipButton.onClick.AddListener(Finish);

            eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                navigationEnabled = eventSystem.sendNavigationEvents;
                eventSystem.SetSelectedGameObject(null);
                eventSystem.sendNavigationEvents = false;
            }
        }

        private void ShowStep()
        {
            view.ShowStep(stepIndex);
            nextClickTime = Time.unscaledTime + clickDelay;
            UpdateHighlight();
        }

        private void LateUpdate()
        {
            if (overlay == null) return;
            UpdateHighlight();
        }

        private void UpdateHighlight()
        {
            Step step = steps[stepIndex];
            bool found = step.highlightGrid ? TryGetGridHole() : TryGetUiHole(step.targets);
            mask.SetHole(found ? hole : null, cornerRadius);
        }

        private bool TryGetUiHole(RectTransform[] targets)
        {
            if (targets == null) return false;
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            Camera uiCamera = battleCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : battleCanvas.worldCamera;
            foreach (var target in targets)
            {
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                target.GetWorldCorners(worldCorners);
                foreach (var point in worldCorners)
                {
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, point);
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, uiCamera, out var local))
                        continue;
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
            }
            if (min.x >= max.x || min.y >= max.y) return false;
            min -= Vector2.one * padding;
            max += Vector2.one * padding;
            hole[0] = min;
            hole[1] = new Vector2(min.x, max.y);
            hole[2] = max;
            hole[3] = new Vector2(max.x, min.y);
            return true;
        }

        private bool TryGetGridHole()
        {
            if (grid == null || battleCamera == null) return false;
            Vector3 origin = grid.CellToWorld(Vector2Int.zero);
            Vector3 halfX = (grid.CellToWorld(Vector2Int.right) - origin) * .5f;
            Vector3 halfY = (grid.CellToWorld(Vector2Int.up) - origin) * .5f;
            worldCorners[0] = origin - halfX - halfY;
            worldCorners[1] = worldCorners[0] + 2 * grid.Height * halfY;
            worldCorners[2] = worldCorners[1] + 2 * grid.Width * halfX;
            worldCorners[3] = worldCorners[0] + 2 * grid.Width * halfX;
            Camera uiCamera = battleCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : battleCanvas.worldCamera;
            for (int i = 0; i < 4; i++)
            {
                Vector3 screen = battleCamera.WorldToScreenPoint(worldCorners[i]);
                if (screen.z <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, uiCamera, out hole[i]))
                    return false;
            }
            return true;
        }

        private void Advance()
        {
            if (Time.unscaledTime < nextClickTime) return;
            if (stepIndex + 1 >= steps.Length) Finish();
            else
            {
                stepIndex++;
                ShowStep();
            }
        }

        private void Finish()
        {
            if (!previewSession)
            {
                PlayerPrefs.SetInt(SeenKey, 1);
                PlayerPrefs.Save();
            }
            CloseOverlay();
        }

        private void OnDisable() => CloseOverlay();

        private void CloseOverlay()
        {
            if (eventSystem != null) eventSystem.sendNavigationEvents = navigationEnabled;
            eventSystem = null;
            if (overlay == null) return;
            overlay.gameObject.SetActive(false);
            Destroy(overlay.gameObject);
            overlay = null;
        }

    }
}
