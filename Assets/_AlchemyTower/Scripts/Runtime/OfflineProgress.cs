using System;

namespace AlchemyTower
{
    public readonly struct OfflineProgressRules
    {
        public OfflineProgressRules(bool enabled, double maxOfflineHours, decimal incomeMultiplier,
            double minPopupAbsenceSeconds)
        {
            if (double.IsNaN(maxOfflineHours) || double.IsInfinity(maxOfflineHours) || maxOfflineHours < 0d)
                throw new ArgumentOutOfRangeException(nameof(maxOfflineHours));
            if (incomeMultiplier < 0m)
                throw new ArgumentOutOfRangeException(nameof(incomeMultiplier));
            if (double.IsNaN(minPopupAbsenceSeconds) || double.IsInfinity(minPopupAbsenceSeconds)
                || minPopupAbsenceSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(minPopupAbsenceSeconds));
            }

            Enabled = enabled;
            MaxOfflineHours = maxOfflineHours;
            IncomeMultiplier = incomeMultiplier;
            MinPopupAbsenceSeconds = minPopupAbsenceSeconds;
        }

        public bool Enabled { get; }
        public double MaxOfflineHours { get; }
        public decimal IncomeMultiplier { get; }
        public double MinPopupAbsenceSeconds { get; }
        public double MaxOfflineSeconds => MaxOfflineHours * 60d * 60d;
    }

    public readonly struct OfflineProgressResult
    {
        public OfflineProgressResult(double absenceSeconds, double creditedSeconds,
            decimal incomeMultiplier, int rewardCoins, long completedCycles)
        {
            AbsenceSeconds = Math.Max(0d, absenceSeconds);
            CreditedSeconds = Math.Max(0d, creditedSeconds);
            IncomeMultiplier = incomeMultiplier;
            RewardCoins = Math.Max(0, rewardCoins);
            CompletedCycles = Math.Max(0L, completedCycles);
        }

        public double AbsenceSeconds { get; }
        public double CreditedSeconds { get; }
        public decimal IncomeMultiplier { get; }
        public int RewardCoins { get; }
        public long CompletedCycles { get; }
        public int EfficiencyPercent => EconomyMath.RoundCoins(IncomeMultiplier * 100m);
        public bool HasReward => RewardCoins > 0;

        public bool ShouldShowPopup(OfflineProgressRules rules)
        {
            return HasReward && AbsenceSeconds >= rules.MinPopupAbsenceSeconds;
        }
    }

    public static class OfflineProgressCalculator
    {
        public static OfflineProgressResult Apply(TowerModel tower, OfflineProgressRules rules,
            double absenceSeconds)
        {
            if (tower == null)
                throw new ArgumentNullException(nameof(tower));

            double safeAbsence = double.IsNaN(absenceSeconds) || double.IsInfinity(absenceSeconds)
                ? 0d
                : Math.Max(0d, absenceSeconds);
            if (!rules.Enabled || safeAbsence <= 0d || rules.MaxOfflineSeconds <= 0d)
                return new OfflineProgressResult(safeAbsence, 0d, rules.IncomeMultiplier, 0, 0);

            double creditedSeconds = Math.Min(safeAbsence, rules.MaxOfflineSeconds);
            decimal grossReward = 0m;
            long completedCycles = 0L;
            for (int i = 0; i < tower.Floors.Count; i++)
            {
                OfflineFloorProgress floorProgress = tower.Floors[i].ApplyOfflineProgress(creditedSeconds);
                grossReward = SaturatingAdd(grossReward, floorProgress.GrossRewardCoins);
                completedCycles = SaturatingAdd(completedCycles, floorProgress.CompletedCycles);
            }

            int rewardCoins;
            try
            {
                rewardCoins = EconomyMath.RoundCoins(checked(grossReward * rules.IncomeMultiplier));
            }
            catch (OverflowException)
            {
                rewardCoins = int.MaxValue;
            }

            int coinsBefore = tower.Coins;
            tower.Wallet.Add(rewardCoins);
            int creditedRewardCoins = tower.Coins - coinsBefore;
            return new OfflineProgressResult(safeAbsence, creditedSeconds, rules.IncomeMultiplier,
                creditedRewardCoins, completedCycles);
        }

        private static decimal SaturatingAdd(decimal left, decimal right)
        {
            try
            {
                return checked(left + right);
            }
            catch (OverflowException)
            {
                return decimal.MaxValue;
            }
        }

        private static long SaturatingAdd(long left, long right)
        {
            return right > long.MaxValue - left ? long.MaxValue : left + right;
        }
    }

    internal readonly struct OfflineFloorProgress
    {
        public OfflineFloorProgress(long completedCycles, decimal grossRewardCoins)
        {
            CompletedCycles = completedCycles;
            GrossRewardCoins = grossRewardCoins;
        }

        public long CompletedCycles { get; }
        public decimal GrossRewardCoins { get; }
    }
}
