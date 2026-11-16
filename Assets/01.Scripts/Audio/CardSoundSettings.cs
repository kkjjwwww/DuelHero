using System;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Audio
{
    [CreateAssetMenu(menuName = "Duel Hero/Audio/Card Sound Settings")]
    public sealed class CardSoundSettings : ScriptableObject
    {
        public enum Timing { Attack, DashLaunch, Hit, DashImpact, GuardPose, ForesightGain, EnergyCharge, EnergyBurst }
        public enum CommonSound { Movement, Damage, Shield, Energy }

        [Serializable]
        public sealed class SoundCue
        {
            public Timing timing;
            public string soundId;
            [Range(0f, 1f)] public float volume = 1f;
            [Min(0f)] public float delay;
        }

        [Serializable]
        public sealed class CardEntry
        {
            public string cardId;
            public SoundCue[] sounds = Array.Empty<SoundCue>();
            public bool suppressMovement, suppressDamage, suppressShield, suppressEnergy;
        }

        public CardDatabase cardDatabase;
        public SoundEffectCatalog soundCatalog;
        public CardEntry[] cards = Array.Empty<CardEntry>();

        public static bool UseCommonSound(string cardId, CommonSound sound)
        {
            if (string.IsNullOrEmpty(cardId)) return true;
            var settings = Resources.Load<CardSoundSettings>("CardSoundSettings");
            if (settings?.cards == null) return true;
            foreach (var card in settings.cards)
            {
                if (card == null || card.cardId != cardId) continue;
                return sound switch
                {
                    CommonSound.Movement => !card.suppressMovement,
                    CommonSound.Damage => !card.suppressDamage,
                    CommonSound.Shield => !card.suppressShield,
                    CommonSound.Energy => !card.suppressEnergy,
                    _ => true
                };
            }
            return true;
        }
    }
}
