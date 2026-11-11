using System;
using System.Collections;
using DuelHero.Battle;
using DuelHero.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuelHero.Map
{
    internal sealed class MapSceneFade : MonoBehaviour
    {
        private CanvasGroup overlay;
        private static int activeTransitions;
        internal static bool IsInProgress => activeTransitions > 0;

        public static void Load(string scenePath, Action<Exception> onFailure, Action onLoadStarted = null)
        {
            var root = new GameObject("Map Scene Fade", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var cover = new GameObject("Black", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)cover.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            cover.GetComponent<Image>().color = Color.black;

            var transition = root.AddComponent<MapSceneFade>();
            transition.overlay = root.GetComponent<CanvasGroup>();
            transition.overlay.alpha = 0f;
            activeTransitions++;
            transition.StartCoroutine(transition.Run(scenePath, onFailure, onLoadStarted));
        }

        private void OnDestroy() => activeTransitions--;

        private IEnumerator Run(string scenePath, Action<Exception> onFailure, Action onLoadStarted)
        {
            yield return Fade(0f, 1f, .25f);
            yield return null;

            AsyncOperation operation = null;
            Exception failure = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
                if (operation == null)
                    throw new InvalidOperationException("씬 로드를 시작하지 못했습니다: " + scenePath);
            }
            catch (Exception error) { failure = error; }

            if (operation != null)
            {
                onLoadStarted?.Invoke();
                yield return operation;
                yield return null;
                var camera = Camera.main;
                if (camera != null && camera.gameObject.scene == SceneManager.GetActiveScene())
                {
                    if (camera.TryGetComponent<BattleCameraController>(out var battleCamera))
                        battleCamera.PlayEntryZoom();
                    else if (camera.TryGetComponent<RestCameraMotion>(out var restCamera))
                        restCamera.PlayEntryZoom();
                }
            }
            yield return Fade(1f, 0f, .45f);
            Destroy(gameObject);
            if (failure != null) onFailure?.Invoke(failure);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                overlay.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            overlay.alpha = to;
        }
    }
}
