using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    [DisallowMultipleComponent]
    public sealed class ShopItemView : MonoBehaviour
    {
        [Header("Item")]
        [SerializeField] private ShopUpgradeId upgradeId;

        [Header("Authored References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text bonusText;
        [SerializeField] private Text priceText;
        [SerializeField] private Button purchaseButton;

        private ShopModel model;
        private float baseTapBoostSeconds;

        public ShopUpgradeId UpgradeId => upgradeId;

        private void Awake()
        {
            if (purchaseButton != null)
                purchaseButton.onClick.AddListener(OnPurchaseClicked);
        }

        private void OnDestroy()
        {
            if (purchaseButton != null)
                purchaseButton.onClick.RemoveListener(OnPurchaseClicked);
        }

        public void Bind(ShopModel shopModel, float authoredBaseTapBoostSeconds)
        {
            model = shopModel ?? throw new ArgumentNullException(nameof(shopModel));
            baseTapBoostSeconds = authoredBaseTapBoostSeconds;
            Refresh();
        }

        public void Refresh()
        {
            if (model == null || levelText == null || bonusText == null
                || priceText == null || purchaseButton == null)
            {
                return;
            }

            int level = model.GetLevel(upgradeId);
            int maxLevel = model.GetMaxLevel(upgradeId);
            bool isMax = level >= maxLevel;
            levelText.text = $"Уровень: {level}/{maxLevel}";
            bonusText.text = FormatBonus(level, isMax ? level : level + 1, isMax);
            priceText.text = isMax
                ? "МАКС"
                : model.GetCurrentCostCoins(upgradeId).ToString("N0", CultureInfo.InvariantCulture);
            purchaseButton.interactable = !isMax && model.CanPurchase(upgradeId);
        }

        private void OnPurchaseClicked()
        {
            if (model != null)
                model.TryPurchase(upgradeId);
            Refresh();
        }

        private string FormatBonus(int currentLevel, int nextLevel, bool isMax)
        {
            if (upgradeId == ShopUpgradeId.AlchemicalGloves)
            {
                float current = model.GetTotalTapBoostSeconds(baseTapBoostSeconds, currentLevel);
                float next = model.GetTotalTapBoostSeconds(baseTapBoostSeconds, nextLevel);
                return isMax
                    ? $"Ускорение: {current:0.00} сек. (МАКС)"
                    : $"Ускорение: {current:0.00} → {next:0.00} сек.";
            }

            decimal currentMultiplier = model.GetRewardMultiplier(currentLevel);
            decimal nextMultiplier = model.GetRewardMultiplier(nextLevel);
            return isMax
                ? $"Награда: ×{currentMultiplier:0.00} (МАКС)"
                : $"Награда: ×{currentMultiplier:0.00} → ×{nextMultiplier:0.00}";
        }
    }
}
