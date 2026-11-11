using System;

namespace AlchemyTower
{
    public enum FloorPhase
    {
        Brewing,
        ReadyToCollect
    }

    public enum FloorTapResult
    {
        Progressed,
        BecameReady,
        Collected,
        AutomatedCycleCompleted
    }

    public readonly struct FloorRules
    {
        public FloorRules(
            string floorId,
            float brewDurationSeconds,
            float tapBoostSeconds,
            int baseRewardCoins,
            int initialUpgradeCostCoins,
            decimal rewardGrowth,
            decimal upgradeCostGrowth,
            int floorNumber = 1,
            int unlockCostCoins = 0,
            int firstFloorAutomationCostCoins = 100,
            int automationUnlockCostMultiplier = 20)
        {
            if (string.IsNullOrWhiteSpace(floorId))
                throw new ArgumentException("Floor ID must not be empty.", nameof(floorId));
            if (!IsFinite(brewDurationSeconds) || brewDurationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(brewDurationSeconds));
            if (!IsFinite(tapBoostSeconds) || tapBoostSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(tapBoostSeconds));
            if (baseRewardCoins <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseRewardCoins));
            if (initialUpgradeCostCoins <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialUpgradeCostCoins));
            if (rewardGrowth < 1m)
                throw new ArgumentOutOfRangeException(nameof(rewardGrowth));
            if (upgradeCostGrowth < 1m)
                throw new ArgumentOutOfRangeException(nameof(upgradeCostGrowth));
            if (floorNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(floorNumber));
            if (unlockCostCoins < 0)
                throw new ArgumentOutOfRangeException(nameof(unlockCostCoins));
            if (floorNumber > 1 && unlockCostCoins <= 0)
                throw new ArgumentOutOfRangeException(nameof(unlockCostCoins));
            if (firstFloorAutomationCostCoins <= 0)
                throw new ArgumentOutOfRangeException(nameof(firstFloorAutomationCostCoins));
            if (automationUnlockCostMultiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(automationUnlockCostMultiplier));

            FloorId = floorId;
            BrewDurationSeconds = brewDurationSeconds;
            TapBoostSeconds = tapBoostSeconds;
            BaseRewardCoins = baseRewardCoins;
            InitialUpgradeCostCoins = initialUpgradeCostCoins;
            RewardGrowth = rewardGrowth;
            UpgradeCostGrowth = upgradeCostGrowth;
            FloorNumber = floorNumber;
            UnlockCostCoins = unlockCostCoins;
            FirstFloorAutomationCostCoins = firstFloorAutomationCostCoins;
            AutomationUnlockCostMultiplier = automationUnlockCostMultiplier;
        }

        public string FloorId { get; }
        public float BrewDurationSeconds { get; }
        public float TapBoostSeconds { get; }
        public int BaseRewardCoins { get; }
        public int InitialUpgradeCostCoins { get; }
        public decimal RewardGrowth { get; }
        public decimal UpgradeCostGrowth { get; }
        public int FloorNumber { get; }
        public int UnlockCostCoins { get; }
        public int FirstFloorAutomationCostCoins { get; }
        public int AutomationUnlockCostMultiplier { get; }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class EconomyMath
    {
        public static int RewardForLevel(FloorRules rules, int level)
        {
            if (level < 1)
                throw new ArgumentOutOfRangeException(nameof(level));

            return RoundCoins(SaturatingMultiply(rules.BaseRewardCoins, rules.RewardGrowth, level - 1));
        }

        public static int UpgradeCostForLevel(FloorRules rules, int currentLevel)
        {
            if (currentLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(currentLevel));

            return RoundCoins(SaturatingMultiply(
                rules.InitialUpgradeCostCoins,
                rules.UpgradeCostGrowth,
                currentLevel - 1));
        }

        public static int AutomationCost(FloorRules rules)
        {
            if (rules.FloorNumber == 1)
                return rules.FirstFloorAutomationCostCoins;

            long cost = (long)rules.UnlockCostCoins * rules.AutomationUnlockCostMultiplier;
            return cost >= int.MaxValue ? int.MaxValue : (int)cost;
        }

        // Economy values use nearest-integer rounding; exact .5 ties round away from zero.
        public static int RoundCoins(decimal value)
        {
            if (value <= 0m)
                return 0;
            if (value >= int.MaxValue)
                return int.MaxValue;

            return decimal.ToInt32(decimal.Round(value, 0, MidpointRounding.AwayFromZero));
        }

        private static decimal SaturatingMultiply(decimal baseValue, decimal growth, int exponent)
        {
            decimal value = baseValue;

            try
            {
                checked
                {
                    for (int i = 0; i < exponent; i++)
                        value *= growth;
                }
            }
            catch (OverflowException)
            {
                return decimal.MaxValue;
            }

            return value;
        }
    }

    public sealed class FloorModel
    {
        private readonly FloorRules rules;

        public FloorModel(FloorRules rules, int initialCoins = 0)
        {
            if (initialCoins < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCoins));

            this.rules = rules;
            Coins = initialCoins;
            Level = 1;
            Phase = FloorPhase.Brewing;
        }

        public int Coins { get; private set; }
        public int Level { get; private set; }
        public float ProgressSeconds { get; private set; }
        public FloorPhase Phase { get; private set; }
        public bool IsAutomated { get; private set; }
        public float BrewDurationSeconds => rules.BrewDurationSeconds;
        public float Progress01 => ProgressSeconds / rules.BrewDurationSeconds;
        public int CurrentRewardCoins => EconomyMath.RewardForLevel(rules, Level);
        public int CurrentUpgradeCostCoins => EconomyMath.UpgradeCostForLevel(rules, Level);
        public int AutomationCostCoins => EconomyMath.AutomationCost(rules);

        public void Tick(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f)
                return;
            if (IsAutomated)
            {
                ApplyAutomatedProgress(deltaSeconds);
                return;
            }
            if (Phase == FloorPhase.ReadyToCollect)
                return;

            ApplyProgress(deltaSeconds);
        }

        public FloorTapResult TapFloor()
        {
            if (IsAutomated)
            {
                bool completedCycle = ApplyAutomatedProgress(rules.TapBoostSeconds);
                return completedCycle
                    ? FloorTapResult.AutomatedCycleCompleted
                    : FloorTapResult.Progressed;
            }

            // Select exactly one branch from the state at the beginning of this action.
            if (Phase == FloorPhase.ReadyToCollect)
            {
                TryCollect();
                return FloorTapResult.Collected;
            }

            ApplyProgress(rules.TapBoostSeconds);
            return Phase == FloorPhase.ReadyToCollect
                ? FloorTapResult.BecameReady
                : FloorTapResult.Progressed;
        }

        public bool TryCollect()
        {
            if (IsAutomated || Phase != FloorPhase.ReadyToCollect)
                return false;

            AddCoins(CurrentRewardCoins);
            ProgressSeconds = 0f;
            Phase = FloorPhase.Brewing;
            return true;
        }

        public bool TryBuyAutomation()
        {
            if (IsAutomated)
                return false;

            int cost = AutomationCostCoins;
            if (Coins < cost)
                return false;

            Coins -= cost;
            IsAutomated = true;

            if (Phase == FloorPhase.ReadyToCollect)
            {
                AddCoins(CurrentRewardCoins);
                ProgressSeconds = 0f;
                Phase = FloorPhase.Brewing;
            }

            return true;
        }

        public bool TryUpgrade()
        {
            int cost = CurrentUpgradeCostCoins;
            if (Coins < cost)
                return false;

            Coins -= cost;
            Level++;
            return true;
        }

        private void ApplyProgress(float seconds)
        {
            ProgressSeconds = Math.Min(rules.BrewDurationSeconds, ProgressSeconds + seconds);
            if (ProgressSeconds >= rules.BrewDurationSeconds)
                Phase = FloorPhase.ReadyToCollect;
        }

        private bool ApplyAutomatedProgress(float seconds)
        {
            if (seconds <= 0f)
                return false;

            double duration = rules.BrewDurationSeconds;
            double totalProgress = ProgressSeconds + seconds;
            double completedCycles = Math.Floor(totalProgress / duration);
            if (completedCycles < 1d)
            {
                ProgressSeconds = (float)totalProgress;
                Phase = FloorPhase.Brewing;
                return false;
            }

            ProgressSeconds = (float)(totalProgress % duration);
            Phase = FloorPhase.Brewing;
            AddCoinsForCycles(CurrentRewardCoins, completedCycles);
            return true;
        }

        private void AddCoins(int amount)
        {
            Coins = amount > int.MaxValue - Coins ? int.MaxValue : Coins + amount;
        }

        private void AddCoinsForCycles(int rewardPerCycle, double completedCycles)
        {
            if (Coins == int.MaxValue || rewardPerCycle <= 0 || completedCycles < 1d)
                return;

            int availableSpace = int.MaxValue - Coins;
            double cyclesUntilSaturation = Math.Ceiling(availableSpace / (double)rewardPerCycle);
            if (completedCycles >= cyclesUntilSaturation)
            {
                Coins = int.MaxValue;
                return;
            }

            Coins += rewardPerCycle * (int)completedCycles;
        }
    }
}
