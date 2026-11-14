using System;
using DuelHero.Cards;
using UnityEngine;

namespace DuelHero.Audio
{
    [CreateAssetMenu(menuName = "Duel Hero/Audio/Card Sound Settings")]
    public sealed class CardSoundSettings : ScriptableObject
    {
        public enum Timing { Attack, DashLaunch, Hit, DashImpact }

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
        }

        public CardDatabase cardDatabase;
        public SoundEffectCatalog soundCatalog;
        public CardEntry[] cards = Array.Empty<CardEntry>();
    }
}
