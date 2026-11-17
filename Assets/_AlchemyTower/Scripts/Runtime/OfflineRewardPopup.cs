using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardPopup : MonoBehaviour
    {
        [Header("Replaceable Artwork")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image rewardIconImage;
        [SerializeField] private Image coinIconImage;

        [Header("Values")]
        [SerializeField] private string absenceLabel = "Фактическое время отсутствия: ";
        [SerializeField] private string creditedLabel = "Засчитанное время: ";
        [SerializeField] private string efficiencyLabel = "Эффективность: ";
        [SerializeField] private string rewardLabel = "Награда: ";
        [SerializeField] private Text absenceValueText;
        [SerializeField] private Text creditedValueText;
        [SerializeField] private Text efficiencyValueText;
        [SerializeField] private Text rewardValueText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        public void Show(OfflineProgressResult result, Func<int, string> formatCoins)
        {
            if (formatCoins == null)
                throw new ArgumentNullException(nameof(formatCoins));

            absenceValueText.text = absenceLabel + FormatDuration(result.AbsenceSeconds);
            creditedValueText.text = creditedLabel + FormatDuration(result.CreditedSeconds);
            efficiencyValueText.text = efficiencyLabel + result.EfficiencyPercent + "%";
            rewardValueText.text = rewardLabel + formatCoins(result.RewardCoins);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public static string FormatDuration(double seconds)
        {
            long wholeSeconds = seconds <= 0d || double.IsNaN(seconds) || double.IsInfinity(seconds)
                ? 0L
                : (long)Math.Floor(Math.Min(seconds, long.MaxValue));
            long days = wholeSeconds / 86400L;
            long hours = wholeSeconds / 3600L % 24L;
            long minutes = wholeSeconds / 60L % 60L;
            long remainderSeconds = wholeSeconds % 60L;
            return days > 0L
                ? $"{days}д {hours:00}:{minutes:00}:{remainderSeconds:00}"
                : $"{hours:00}:{minutes:00}:{remainderSeconds:00}";
        }
    }
}
