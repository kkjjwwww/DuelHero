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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreVolumes()
        {
            master = PlayerPrefs.GetFloat("Audio.Master", .75f);
            music = PlayerPrefs.GetFloat("Audio.Music", .75f);
            effects = PlayerPrefs.GetFloat("Audio.Effects", .75f);
            AudioMixer audio = Resources.Load<AudioMixer>("DuelHeroAudio");
            audio.SetFloat("MasterVolume", ToDecibels(master));
            audio.SetFloat("MusicVolume", ToDecibels(music));
            audio.SetFloat("EffectsVolume", ToDecibels(effects));
        }

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

        private void SetMaster(float value) { master = value; mixer.SetFloat("MasterVolume", ToDecibels(value)); PlayerPrefs.SetFloat("Audio.Master", value); }
        private void SetMusic(float value) { music = value; mixer.SetFloat("MusicVolume", ToDecibels(value)); PlayerPrefs.SetFloat("Audio.Music", value); }
        private void SetEffects(float value) { effects = value; mixer.SetFloat("EffectsVolume", ToDecibels(value)); PlayerPrefs.SetFloat("Audio.Effects", value); }
        private static float ToDecibels(float value) { return value <= 0f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(value)); }
    }
}
