using UnityEngine;

namespace AlchemyTower
{
    [CreateAssetMenu(fileName = "ShopDefinition", menuName = "Alchemy Tower/Shop Definition")]
    public sealed class ShopDefinitionSO : ScriptableObject
    {
        [Header("Alchemical Gloves")]
        [SerializeField, Min(1)] private int glovesInitialCostCoins = 2500;
        [SerializeField, Min(1)] private int glovesCostMultiplier = 2;
        [SerializeField, Min(1)] private int glovesMaxLevel = 5;
        [SerializeField, Min(0f)] private float tapBoostSecondsPerLevel = 0.05f;

        [Header("Golden Seal")]
        [SerializeField, Min(1)] private int goldenSealInitialCostCoins = 6000;
        [SerializeField, Min(1)] private int goldenSealCostMultiplier = 2;
        [SerializeField, Min(1)] private int goldenSealMaxLevel = 5;
        [SerializeField, Min(0)] private int rewardPercentPerLevel = 5;

        public ShopRules CreateRules()
        {
            return new ShopRules(
                new ShopUpgradeRules(glovesInitialCostCoins, glovesCostMultiplier, glovesMaxLevel),
                tapBoostSecondsPerLevel,
                new ShopUpgradeRules(goldenSealInitialCostCoins, goldenSealCostMultiplier, goldenSealMaxLevel),
                rewardPercentPerLevel);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            glovesInitialCostCoins = Mathf.Max(1, glovesInitialCostCoins);
            glovesCostMultiplier = Mathf.Max(1, glovesCostMultiplier);
            glovesMaxLevel = Mathf.Max(1, glovesMaxLevel);
            tapBoostSecondsPerLevel = Mathf.Max(0f, tapBoostSecondsPerLevel);
            goldenSealInitialCostCoins = Mathf.Max(1, goldenSealInitialCostCoins);
            goldenSealCostMultiplier = Mathf.Max(1, goldenSealCostMultiplier);
            goldenSealMaxLevel = Mathf.Max(1, goldenSealMaxLevel);
            rewardPercentPerLevel = Mathf.Max(0, rewardPercentPerLevel);
        }
#endif
    }
}
