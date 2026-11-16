using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    [DisallowMultipleComponent]
    public sealed class TopHUD : MonoBehaviour
    {
        [Serializable]
        public struct CompactNumberSuffix
        {
            [Min(1)] public int threshold;
            [Min(1)] public int divisor;
            public string suffix;
        }

        [Header("Editable Content")]
        [SerializeField] private string gameTitle = "ALCHEMY TOWER";
        [SerializeField, Min(0)] private int crystalPlaceholder;

        [Header("Text References")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text coinBalanceText;
        [SerializeField] private Text crystalBalanceText;

        [Header("Compact Coin Formatting")]
        [Tooltip("Largest matching threshold is used. Values remain unchanged in the wallet.")]
        [SerializeField] private CompactNumberSuffix[] compactNumberSuffixes =
        {
            new CompactNumberSuffix { threshold = 1_000, divisor = 1_000, suffix = "k" },
            new CompactNumberSuffix { threshold = 1_000_000, divisor = 1_000_000, suffix = "m" },
            new CompactNumberSuffix { threshold = 1_000_000_000, divisor = 1_000_000_000, suffix = "b" }
        };

        private void Awake()
        {
            ApplyAuthoredContent();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyAuthoredContent();
        }
#endif

        public void SetCoinBalance(int coins)
        {
            if (coinBalanceText != null)
                coinBalanceText.text = FormatCompact(coins, compactNumberSuffixes);
        }

        public static string FormatCompact(int value, CompactNumberSuffix[] suffixes)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            CompactNumberSuffix selected = default;
            bool found = false;
            if (suffixes != null)
            {
                for (int i = 0; i < suffixes.Length; i++)
                {
                    CompactNumberSuffix candidate = suffixes[i];
                    if (candidate.threshold <= 0
                        || candidate.divisor <= 0
                        || value < candidate.threshold
                        || (found && candidate.threshold <= selected.threshold))
                    {
                        continue;
                    }

                    selected = candidate;
                    found = true;
                }
            }

            if (!found)
                return value.ToString("N0", CultureInfo.InvariantCulture);

            double compactValue = value / (double)selected.divisor;
            return compactValue.ToString("0.#", CultureInfo.InvariantCulture) + selected.suffix;
        }

        private void ApplyAuthoredContent()
        {
            if (titleText != null)
                titleText.text = gameTitle;
            if (crystalBalanceText != null)
                crystalBalanceText.text = crystalPlaceholder.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
