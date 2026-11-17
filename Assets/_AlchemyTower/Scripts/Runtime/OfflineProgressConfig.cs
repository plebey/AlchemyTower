using System;
using UnityEngine;

namespace AlchemyTower
{
    [CreateAssetMenu(fileName = "OfflineProgressConfig", menuName = "Alchemy Tower/Offline Progress Config")]
    public sealed class OfflineProgressConfig : ScriptableObject
    {
        [SerializeField] private bool enabled = true;
        [SerializeField, Min(0f)] private float maxOfflineHours = 6f;
        [SerializeField, Min(0f)] private float incomeMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float minPopupAbsenceSeconds = 60f;

        public OfflineProgressRules CreateRules()
        {
            return new OfflineProgressRules(enabled, maxOfflineHours, ToDecimal(incomeMultiplier),
                minPopupAbsenceSeconds);
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            maxOfflineHours = SanitizeNonNegative(maxOfflineHours, 6f);
            incomeMultiplier = SanitizeNonNegative(incomeMultiplier, 0.25f);
            minPopupAbsenceSeconds = SanitizeNonNegative(minPopupAbsenceSeconds, 60f);
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0f, value);
        }

        private static decimal ToDecimal(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException("Offline income multiplier must be finite.");
            return Convert.ToDecimal(value);
        }
    }
}
