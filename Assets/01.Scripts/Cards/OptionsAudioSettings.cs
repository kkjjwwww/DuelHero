using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    public sealed class OptionsAudioSettings : MonoBehaviour
    {
        [SerializeField] private AudioMixer mixer;
        private Slider masterSlider, musicSlider, effectsSlider;
        private static float master = .75f, music = .75f, effects = .75f;

        private void Start()
        {
            Transform content = transform.Find("OptionsContent");
            masterSlider = content.Find("MasterSlider").GetComponent<Slider>();
            musicSlider = content.Find("MusicSlider").GetComponent<Slider>();
            effectsSlider = content.Find("EffectsSlider").GetComponent<Slider>();
            masterSlider.interactable = musicSlider.interactable = effectsSlider.interactable = true;
            masterSlider.onValueChanged.AddListener(SetMaster);
            musicSlider.onValueChanged.AddListener(SetMusic);
            effectsSlider.onValueChanged.AddListener(SetEffects);
            Sync();
        }

        private void OnEnable()
        {
            if (masterSlider != null) Sync();
        }

        private void Sync()
        {
            masterSlider.SetValueWithoutNotify(master);
            musicSlider.SetValueWithoutNotify(music);
            effectsSlider.SetValueWithoutNotify(effects);
            SetMaster(master);
            SetMusic(music);
            SetEffects(effects);
        }

        private void SetMaster(float value) { master = value; mixer.SetFloat("MasterVolume", ToDecibels(value)); }
        private void SetMusic(float value) { music = value; mixer.SetFloat("MusicVolume", ToDecibels(value)); }
        private void SetEffects(float value) { effects = value; mixer.SetFloat("EffectsVolume", ToDecibels(value)); }
        private static float ToDecibels(float value) { return value <= 0f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(value)); }
    }
}
