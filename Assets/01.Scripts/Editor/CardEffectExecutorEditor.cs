using DuelHero.Battle;
using UnityEditor;

[CustomEditor(typeof(CardEffectExecutor))]
[CanEditMultipleObjects]
internal sealed class CardEffectExecutorEditor : Editor
{
    private static readonly string[] EnergyReleaseFields =
    {
        "energyReleaseChargeVfx",
        "energyReleaseBurstVfx",
        "energyReleaseGroundVfx",
        "energyReleaseHitVfx",
        "energyReleaseRiseDuration",
        "energyReleaseLandingDuration",
        "energyReleaseCenterHeight",
        "energyReleaseLiftHeight",
        "energyReleaseAirHoldDuration",
        "energyReleaseZoom",
        "energyReleaseShakeStrength",
        "energyReleaseShakeDuration"
    };

    private bool energyReleaseExpanded;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, EnergyReleaseFields);
        energyReleaseExpanded = EditorGUILayout.Foldout(energyReleaseExpanded, "Energy Release", true);
        if (energyReleaseExpanded)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (string field in EnergyReleaseFields)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
}