using System;
using System.Collections.Generic;
using UnityEngine;
namespace DuelHero.Logging
{
    public sealed class BattleLog : MonoBehaviour
    {
        private readonly List<BattleLogEntry> entries = new();
        private IReadOnlyList<BattleLogEntry> readOnlyEntries;
        public IReadOnlyList<BattleLogEntry> Entries => readOnlyEntries ??= entries.AsReadOnly();
        public event Action Changed;
        public void Record(BattleLogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            entries.Add(entry);
            if (entries.Count > 100) entries.RemoveAt(0);
            Changed?.Invoke();
        }
    }
}
