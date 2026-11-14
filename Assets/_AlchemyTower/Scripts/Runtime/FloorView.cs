using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    public sealed class FloorView : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private Button productionButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button automationButton;

        [Header("Display")]
        [SerializeField] private Text floorNameText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text rewardText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text upgradeButtonText;
        [SerializeField] private Text automationButtonText;
        [SerializeField] private Image progressFill;

        [Header("Status Colors")]
        [SerializeField] private Color brewingStatusColor = Color.white;
        [SerializeField] private Color readyStatusColor = new Color(0.35f, 1f, 0.55f);

        private FloorModel model;
        private bool listenersRegistered;
        private System.Func<bool> canProcessInput;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            productionButton.onClick.AddListener(OnProductionTapped);
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
            automationButton.onClick.AddListener(OnAutomationClicked);
            listenersRegistered = true;
        }

        public void Bind(FloorModel floorModel, string displayName, System.Func<bool> inputGuard = null)
        {
            model = floorModel ?? throw new System.ArgumentNullException(nameof(floorModel));
            canProcessInput = inputGuard;
            floorNameText.text = displayName;
            Refresh();
        }

        private void OnDestroy()
        {
            if (listenersRegistered && productionButton != null)
                productionButton.onClick.RemoveListener(OnProductionTapped);
            if (listenersRegistered && upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
            if (listenersRegistered && automationButton != null)
                automationButton.onClick.RemoveListener(OnAutomationClicked);
        }

        private void OnProductionTapped()
        {
            if (model == null || !CanProcessInput())
                return;

            model.TapFloor();
            Refresh();
        }

        private void OnUpgradeClicked()
        {
            if (model == null || !CanProcessInput())
                return;

            model.TryUpgrade();
            Refresh();
        }

        private void OnAutomationClicked()
        {
            if (model == null || !CanProcessInput())
                return;

            model.TryBuyAutomation();
            Refresh();
        }

        public void Refresh()
        {
            if (model == null)
                return;

            levelText.text = $"Level: {model.Level}";
            rewardText.text = $"Potion reward: {model.CurrentRewardCoins}";
            progressText.text = $"{model.ProgressSeconds:0.00} / {model.BrewDurationSeconds:0.00} s";
            progressFill.fillAmount = Mathf.Clamp01(model.Progress01);

            bool ready = model.Phase == FloorPhase.ReadyToCollect;
            statusText.text = model.IsAutomated
                ? "AUTOMATED - TAP TO SPEED UP"
                : ready ? "READY - TAP TO COLLECT" : "BREWING - TAP TO SPEED UP";
            statusText.color = ready ? readyStatusColor : brewingStatusColor;

            upgradeButtonText.text = $"Upgrade income: {model.CurrentUpgradeCostCoins}";
            upgradeButton.interactable = model.Coins >= model.CurrentUpgradeCostCoins;

            GameObject automationControl = automationButton.transform.parent.gameObject;
            automationControl.SetActive(!model.IsAutomated);
            if (!model.IsAutomated)
            {
                automationButtonText.text = model.Level < model.AutomationRequiredLevel
                    ? $"Hire Assistant — Level {model.AutomationRequiredLevel}"
                    : $"Hire Assistant — {model.AutomationCostCoins}";
                automationButton.interactable = model.CanBuyAutomation;
            }
        }

        private bool ValidateReferences()
        {
            List<string> missing = new List<string>();
            AddIfMissing(productionButton, nameof(productionButton), missing);
            AddIfMissing(upgradeButton, nameof(upgradeButton), missing);
            AddIfMissing(automationButton, nameof(automationButton), missing);
            AddIfMissing(floorNameText, nameof(floorNameText), missing);
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

        private bool CanProcessInput()
        {
            return canProcessInput == null || canProcessInput();
        }
    }
}
