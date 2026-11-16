using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Battle
{
    public sealed class BattleTutorialView : MonoBehaviour
    {
        [SerializeField] private BattleTutorialMask mask;
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private GameObject[] descriptions;

        internal BattleTutorialMask Mask => mask;
        internal Button AdvanceButton => advanceButton;
        internal Button SkipButton => skipButton;
        internal int StepCount => descriptions.Length;

        internal void ShowStep(int index)
        {
            for (int i = 0; i < descriptions.Length; i++)
                descriptions[i].SetActive(i == index);
        }
    }
}
