using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DuelHero.Map;
using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class OptionsDisplaySettings : MonoBehaviour
    {
        private Dropdown resolutionDropdown, modeDropdown;
        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private bool initialized;
        private bool currentOutsideList;

        private void OnEnable()
        {
            var content = transform.Find("OptionsContent");
            bool showReturn = gameObject.scene.name != "TitleScene";
            content.Find("ReturnToTitle").gameObject.SetActive(showReturn);
            if (!initialized) Initialize();
            RefreshOptions();
        }

        private void Initialize()
        {
            var content = transform.Find("OptionsContent");
            resolutionDropdown = content.Find("ResolutionField").GetComponent<Dropdown>();
            modeDropdown = content.Find("WindowModeField").GetComponent<Dropdown>();
            resolutionDropdown.onValueChanged.AddListener(ApplyResolution);
            modeDropdown.onValueChanged.AddListener(ApplyMode);
            content.Find("ReturnToTitle").GetComponent<Button>().onClick.AddListener(ReturnToTitle);
            initialized = true;
        }

        private void ReturnToTitle()
        {
            const string titleScene = "Assets/00.Scenes/TitleScene.unity";
            if (MapSceneFade.IsInProgress) return;
            if (!Application.CanStreamedLevelBeLoaded(titleScene))
            {
                Debug.LogWarning("타이틀 씬을 불러올 수 없습니다.", this);
                return;
            }
            gameObject.SetActive(false);
            try
            {
                MapSceneFade.Load(titleScene, error =>
                {
                    Debug.LogWarning("타이틀 씬을 불러오지 못했습니다: " + error.Message, this);
                    if (this != null) gameObject.SetActive(true);
                });
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("타이틀 씬 전환을 시작하지 못했습니다: " + error.Message, this);
                gameObject.SetActive(true);
            }
        }

        private void RefreshOptions()
        {
            resolutions.Clear();
            resolutions.AddRange(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height))
                .Where(r => Mathf.Abs((float)r.x / r.y - 16f / 9f) < .004f)
                .Distinct().OrderBy(r => r.x).ThenBy(r => r.y));
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(resolutions.Select(r => r.x + " × " + r.y).ToList());
            modeDropdown.ClearOptions();
            modeDropdown.AddOptions(new List<string> { "창 모드", "전체 화면" });
            SyncValues();
        }

        private void SyncValues()
        {
            int index = resolutions.FindIndex(r => r.x == Screen.width && r.y == Screen.height);
            currentOutsideList = index < 0;
            var options = resolutions.Select(r => r.x + " × " + r.y).ToList();
            if (currentOutsideList) options.Insert(0, "현재: " + Screen.width + " × " + Screen.height);
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(currentOutsideList ? 0 : index);
            modeDropdown.SetValueWithoutNotify(Screen.fullScreen ? 1 : 0);
        }

        private void ApplyResolution(int index)
        {
            if (currentOutsideList) index--;
            if (index < 0 || index >= resolutions.Count) return;
            var size = resolutions[index];
            Screen.SetResolution(size.x, size.y, Screen.fullScreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed);
            StartCoroutine(SyncAfterApply());
        }

        private void ApplyMode(int index)
        {
            Screen.SetResolution(Screen.width, Screen.height, index == 1 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed);
            StartCoroutine(SyncAfterApply());
        }

        private IEnumerator SyncAfterApply()
        {
            yield return new WaitForEndOfFrame();
            yield return null;
            SyncValues();
        }
    }
}
