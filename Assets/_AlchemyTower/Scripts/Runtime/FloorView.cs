using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    public sealed class FloorView : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private FloorDefinitionSO definition;

        [Header("Input")]
        [SerializeField] private Button productionButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button automationButton;

        [Header("Display")]
        [SerializeField] private Text floorNameText;
        [SerializeField] private Text coinsText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text rewardText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text upgradeButtonText;
        [SerializeField] private Text automationButtonText;
        [SerializeField] private Image progressFill;

        private FloorModel model;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            model = new FloorModel(definition.CreateRules());
            floorNameText.text = definition.DisplayName;
            productionButton.onClick.AddListener(OnProductionTapped);
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
            automationButton.onClick.AddListener(OnAutomationClicked);
            Refresh();
        }

        private void Update()
        {
            model.Tick(Time.deltaTime);
            Refresh();
        }

        private void OnDestroy()
        {
            if (productionButton != null)
                productionButton.onClick.RemoveListener(OnProductionTapped);
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
            if (automationButton != null)
                automationButton.onClick.RemoveListener(OnAutomationClicked);
        }

        private void OnProductionTapped()
        {
            model.TapFloor();
            Refresh();
        }

        private void OnUpgradeClicked()
        {
            model.TryUpgrade();
            Refresh();
        }

        private void OnAutomationClicked()
        {
            model.TryBuyAutomation();
            Refresh();
        }

        private void Refresh()
        {
            coinsText.text = $"Coins: {model.Coins}";
            levelText.text = $"Level: {model.Level}";
            rewardText.text = $"Potion reward: {model.CurrentRewardCoins}";
            progressText.text = $"{model.ProgressSeconds:0.00} / {model.BrewDurationSeconds:0.00} s";
            progressFill.fillAmount = Mathf.Clamp01(model.Progress01);

            bool ready = model.Phase == FloorPhase.ReadyToCollect;
            statusText.text = model.IsAutomated
                ? "AUTOMATED - TAP TO SPEED UP"
                : ready ? "READY - TAP TO COLLECT" : "BREWING - TAP TO SPEED UP";
            statusText.color = ready ? new Color(0.35f, 1f, 0.55f) : Color.white;

            upgradeButtonText.text = $"Upgrade income: {model.CurrentUpgradeCostCoins}";
            upgradeButton.interactable = model.Coins >= model.CurrentUpgradeCostCoins;

            automationButtonText.text = model.IsAutomated
                ? "Automated"
                : $"Hire Assistant — {model.AutomationCostCoins}";
            automationButton.interactable = !model.IsAutomated && model.Coins >= model.AutomationCostCoins;
        }

        private bool ValidateReferences()
        {
            List<string> missing = new List<string>();
            AddIfMissing(definition, nameof(definition), missing);
            AddIfMissing(productionButton, nameof(productionButton), missing);
            AddIfMissing(upgradeButton, nameof(upgradeButton), missing);
            AddIfMissing(automationButton, nameof(automationButton), missing);
            AddIfMissing(floorNameText, nameof(floorNameText), missing);
            AddIfMissing(coinsText, nameof(coinsText), missing);
            AddIfMissing(statusText, nameof(statusText), missing);
            AddIfMissing(levelText, nameof(levelText), missing);
            AddIfMissing(rewardText, nameof(rewardText), missing);
            AddIfMissing(progressText, nameof(progressText), missing);
            AddIfMissing(upgradeButtonText, nameof(upgradeButtonText), missing);
            AddIfMissing(automationButtonText, nameof(automationButtonText), missing);
            AddIfMissing(progressFill, nameof(progressFill), missing);

            if (missing.Count == 0)
                return true;

            Debug.LogError($"FloorView is missing required references: {string.Join(", ", missing)}.", this);
            return false;
        }

        private static void AddIfMissing(Object reference, string fieldName, ICollection<string> missing)
        {
            if (reference == null)
                missing.Add(fieldName);
        }
    }
}
