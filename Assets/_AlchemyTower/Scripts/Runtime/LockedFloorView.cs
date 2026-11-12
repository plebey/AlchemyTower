using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    public sealed class LockedFloorView : MonoBehaviour
    {
        [SerializeField] private Button unlockButton;
        [SerializeField] private Text unlockPriceText;

        private Action unlockAction;
        private Func<bool> canProcessInput;

        private void Awake()
        {
            if (unlockButton == null || unlockPriceText == null)
            {
                Debug.LogError("LockedFloorView is missing required references.", this);
                enabled = false;
                return;
            }

            unlockButton.onClick.AddListener(OnUnlockClicked);
        }

        public void Bind(int unlockPriceCoins, Action onUnlock, bool canAfford, Func<bool> inputGuard = null)
        {
            unlockAction = onUnlock ?? throw new ArgumentNullException(nameof(onUnlock));
            canProcessInput = inputGuard;
            unlockPriceText.text = $"{unlockPriceCoins} coins";
            unlockButton.interactable = canAfford;
        }

        public void SetAffordable(bool canAfford)
        {
            if (unlockButton != null)
                unlockButton.interactable = canAfford;
        }

        private void OnDestroy()
        {
            if (unlockButton != null)
                unlockButton.onClick.RemoveListener(OnUnlockClicked);
        }

        private void OnUnlockClicked()
        {
            if (canProcessInput == null || canProcessInput())
                unlockAction?.Invoke();
        }
    }
}
