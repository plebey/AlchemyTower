using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("statusText")]
        [SerializeField] private Text productNameText;
        [SerializeField] private Text levelText;
        [FormerlySerializedAs("rewardText")]
        [SerializeField] private Text incomePerSecondText;
        [FormerlySerializedAs("progressText")]
        [SerializeField] private Text remainingTimeText;
        [SerializeField] private Text upgradeButtonText;
        [SerializeField] private Text automationButtonText;
        [SerializeField] private Image progressFill;

        [Header("Authored Content")]
        [SerializeField] private string productDisplayName = "Potion";

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
            productNameText.text = productDisplayName;
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

            levelText.text = $"Ур. {model.Level}";
            incomePerSecondText.text = $"+{model.NominalIncomePerSecond:0.##}/сек";
            float progress01 = Mathf.Clamp01(model.Progress01);
            RectTransform progressFillRect = progressFill.rectTransform;
            Vector2 fillAnchorMax = progressFillRect.anchorMax;
            fillAnchorMax.x = progress01;
            progressFillRect.anchorMax = fillAnchorMax;

            bool ready = model.Phase == FloorPhase.ReadyToCollect;
            float remainingSeconds = Mathf.Max(0f, model.BrewDurationSeconds - model.ProgressSeconds);
            remainingTimeText.text = ready ? "Готово!" : $"{remainingSeconds:0.0}с";

            upgradeButtonText.text = $"Улучшить\n{model.CurrentUpgradeCostCoins}";
            upgradeButton.interactable = model.Coins >= model.CurrentUpgradeCostCoins;

            GameObject automationControl = automationButton.transform.parent.gameObject;
            automationControl.SetActive(!model.IsAutomated);
            if (!model.IsAutomated)
            {
                automationButtonText.text = model.Level < model.AutomationRequiredLevel
                    ? $"Доступно с ур. {model.AutomationRequiredLevel}"
                    : $"Нанять автоматизатора\n{model.AutomationCostCoins}  +";
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
            AddIfMissing(productNameText, nameof(productNameText), missing);
            AddIfMissing(levelText, nameof(levelText), missing);
            AddIfMissing(incomePerSecondText, nameof(incomePerSecondText), missing);
            AddIfMissing(remainingTimeText, nameof(remainingTimeText), missing);
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
