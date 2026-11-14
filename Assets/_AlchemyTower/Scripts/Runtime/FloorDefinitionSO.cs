using System;
using UnityEngine;

namespace AlchemyTower
{
    [CreateAssetMenu(fileName = "FloorDefinition", menuName = "Alchemy Tower/Floor Definition")]
    public sealed class FloorDefinitionSO : ScriptableObject
    {
        [SerializeField] private string floorId = "first_floor";
        [SerializeField] private string displayName = "First Floor";
        [SerializeField, Min(0.01f)] private float brewDurationSeconds = 5f;
        [SerializeField, Min(0f)] private float tapBoostSeconds = 0.35f;
        [SerializeField, Min(1)] private int baseRewardCoins = 10;
        [SerializeField, Min(1)] private int initialUpgradeCostCoins = 30;
        [SerializeField, Min(1f)] private double rewardGrowth = 1.10d;
        [SerializeField, Min(1f)] private double upgradeCostGrowth = 1.15d;
        [Header("Floor Economy")]
        [SerializeField, Min(1)] private int floorNumber = 1;
        [SerializeField, Min(0)] private int unlockCostCoins;
        [SerializeField, Min(1)] private int firstFloorAutomationCostCoins = 100;
        [SerializeField, Min(1)] private int automationUnlockCostMultiplier = 20;
        [SerializeField, Min(1)] private int automationRequiredLevel = 5;

        public string FloorId => floorId;
        public string DisplayName => displayName;

        public FloorRules CreateRules()
        {
            return new FloorRules(
                floorId,
                brewDurationSeconds,
                tapBoostSeconds,
                baseRewardCoins,
                initialUpgradeCostCoins,
                ToDecimal(rewardGrowth, nameof(rewardGrowth)),
                ToDecimal(upgradeCostGrowth, nameof(upgradeCostGrowth)),
                floorNumber,
                unlockCostCoins,
                firstFloorAutomationCostCoins,
                automationUnlockCostMultiplier,
                automationRequiredLevel);
        }

        private void OnValidate()
        {
            floorNumber = Math.Max(1, floorNumber);
            unlockCostCoins = Math.Max(0, unlockCostCoins);
            if (firstFloorAutomationCostCoins <= 0)
                firstFloorAutomationCostCoins = 100;
            if (automationUnlockCostMultiplier <= 0)
                automationUnlockCostMultiplier = 20;
            if (automationRequiredLevel <= 0)
                automationRequiredLevel = 5;
        }

        private static decimal ToDecimal(double value, string fieldName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException($"{fieldName} must be a finite number.");
            }

            try
            {
                return Convert.ToDecimal(value);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException($"{fieldName} is outside the supported range.", exception);
            }
        }
    }
}
