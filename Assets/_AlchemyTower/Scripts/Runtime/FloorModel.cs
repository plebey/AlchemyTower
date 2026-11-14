using System;
using System.Collections.Generic;

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
        AutomatedCycleCompleted,
        Locked
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
            int automationUnlockCostMultiplier = 20,
            int automationRequiredLevel = 5)
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
            if (automationRequiredLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(automationRequiredLevel));

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
            AutomationRequiredLevel = automationRequiredLevel;
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
        public int AutomationRequiredLevel { get; }

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

    public sealed class CoinWallet
    {
        private readonly Action<bool> stateChanged;

        public CoinWallet(int initialCoins = 0)
            : this(initialCoins, null)
        {
        }

        internal CoinWallet(int initialCoins, Action<bool> stateChanged)
        {
            if (initialCoins < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCoins));

            Coins = initialCoins;
            this.stateChanged = stateChanged;
        }

        public int Coins { get; private set; }

        internal bool TrySpend(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (Coins < amount)
                return false;

            Coins -= amount;
            stateChanged?.Invoke(true);
            return true;
        }

        internal void Add(int amount)
        {
            if (amount <= 0)
                return;

            Coins = amount > int.MaxValue - Coins ? int.MaxValue : Coins + amount;
            stateChanged?.Invoke(false);
        }

        internal void AddForCycles(int rewardPerCycle, double completedCycles)
        {
            if (Coins == int.MaxValue || rewardPerCycle <= 0 || completedCycles < 1d)
                return;

            int availableSpace = int.MaxValue - Coins;
            double cyclesUntilSaturation = Math.Ceiling(availableSpace / (double)rewardPerCycle);
            if (completedCycles >= cyclesUntilSaturation)
            {
                Coins = int.MaxValue;
                stateChanged?.Invoke(false);
                return;
            }

            Coins += rewardPerCycle * (int)completedCycles;
            stateChanged?.Invoke(false);
        }

        internal void Restore(int coins) => Coins = coins;
    }

    public sealed class FloorModel
    {
        private readonly FloorRules rules;
        private readonly CoinWallet wallet;
        private readonly Action<bool> stateChanged;

        public FloorModel(FloorRules rules, int initialCoins = 0)
            : this(rules, new CoinWallet(initialCoins), true, null)
        {
        }

        internal FloorModel(FloorRules rules, CoinWallet wallet, bool isUnlocked, Action<bool> stateChanged)
        {
            this.rules = rules;
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.stateChanged = stateChanged;
            IsUnlocked = isUnlocked;
            Level = 1;
            Phase = FloorPhase.Brewing;
        }

        public string FloorId => rules.FloorId;
        public int FloorNumber => rules.FloorNumber;
        public int UnlockCostCoins => rules.UnlockCostCoins;
        public bool IsUnlocked { get; private set; }
        public int Coins => wallet.Coins;
        public int Level { get; private set; }
        public float ProgressSeconds { get; private set; }
        public FloorPhase Phase { get; private set; }
        public bool IsAutomated { get; private set; }
        public float BrewDurationSeconds => rules.BrewDurationSeconds;
        public float Progress01 => IsUnlocked ? ProgressSeconds / rules.BrewDurationSeconds : 0f;
        public int CurrentRewardCoins => EconomyMath.RewardForLevel(rules, Level);
        public float NominalIncomePerSecond => rules.BrewDurationSeconds > 0f
            ? CurrentRewardCoins / rules.BrewDurationSeconds
            : 0f;
        public int CurrentUpgradeCostCoins => EconomyMath.UpgradeCostForLevel(rules, Level);
        public int AutomationCostCoins => EconomyMath.AutomationCost(rules);
        public int AutomationRequiredLevel => rules.AutomationRequiredLevel;
        public bool CanBuyAutomation => IsUnlocked
            && !IsAutomated
            && Level >= AutomationRequiredLevel
            && Coins >= AutomationCostCoins;

        public void Tick(float deltaSeconds)
        {
            if (!IsUnlocked || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f)
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
            if (!IsUnlocked)
                return FloorTapResult.Locked;

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
            if (!IsUnlocked || IsAutomated || Phase != FloorPhase.ReadyToCollect)
                return false;

            wallet.Add(CurrentRewardCoins);
            ProgressSeconds = 0f;
            Phase = FloorPhase.Brewing;
            stateChanged?.Invoke(false);
            return true;
        }

        public bool TryBuyAutomation()
        {
            if (!CanBuyAutomation || !wallet.TrySpend(AutomationCostCoins))
                return false;

            IsAutomated = true;

            if (Phase == FloorPhase.ReadyToCollect)
            {
                wallet.Add(CurrentRewardCoins);
                ProgressSeconds = 0f;
                Phase = FloorPhase.Brewing;
            }

            stateChanged?.Invoke(true);
            return true;
        }

        public bool TryUpgrade()
        {
            if (!IsUnlocked || !wallet.TrySpend(CurrentUpgradeCostCoins))
                return false;

            Level++;
            stateChanged?.Invoke(true);
            return true;
        }

        internal void Unlock()
        {
            IsUnlocked = true;
            ProgressSeconds = 0f;
            Phase = FloorPhase.Brewing;
            stateChanged?.Invoke(true);
        }

        private void ApplyProgress(float seconds)
        {
            float previousProgress = ProgressSeconds;
            FloorPhase previousPhase = Phase;
            ProgressSeconds = Math.Min(rules.BrewDurationSeconds, ProgressSeconds + seconds);
            if (ProgressSeconds >= rules.BrewDurationSeconds)
                Phase = FloorPhase.ReadyToCollect;
            if (ProgressSeconds != previousProgress || Phase != previousPhase)
                stateChanged?.Invoke(false);
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
                stateChanged?.Invoke(false);
                return false;
            }

            ProgressSeconds = (float)(totalProgress % duration);
            Phase = FloorPhase.Brewing;
            wallet.AddForCycles(CurrentRewardCoins, completedCycles);
            stateChanged?.Invoke(false);
            return true;
        }

        internal FloorSnapshot CaptureSnapshot()
        {
            return new FloorSnapshot(FloorId, IsUnlocked, Level, IsAutomated, ProgressSeconds, Phase);
        }

        internal void Restore(FloorSnapshot snapshot)
        {
            IsUnlocked = snapshot.IsUnlocked;
            Level = snapshot.Level;
            IsAutomated = snapshot.IsAutomated;
            ProgressSeconds = snapshot.ProgressSeconds;
            Phase = snapshot.Phase;
        }
    }

    public readonly struct FloorSnapshot
    {
        public FloorSnapshot(string floorId, bool isUnlocked, int level, bool isAutomated,
            float progressSeconds, FloorPhase phase)
        {
            FloorId = floorId;
            IsUnlocked = isUnlocked;
            Level = level;
            IsAutomated = isAutomated;
            ProgressSeconds = progressSeconds;
            Phase = phase;
        }

        public string FloorId { get; }
        public bool IsUnlocked { get; }
        public int Level { get; }
        public bool IsAutomated { get; }
        public float ProgressSeconds { get; }
        public FloorPhase Phase { get; }
    }

    public sealed class TowerSnapshot
    {
        public TowerSnapshot(int coins, IReadOnlyList<FloorSnapshot> floors)
        {
            Coins = coins;
            Floors = floors ?? throw new ArgumentNullException(nameof(floors));
        }

        public int Coins { get; }
        public IReadOnlyList<FloorSnapshot> Floors { get; }
    }

    public sealed class TowerModel
    {
        private readonly FloorModel[] floors;

        public TowerModel(IReadOnlyList<FloorRules> floorRules, int initialCoins = 0)
        {
            if (floorRules == null)
                throw new ArgumentNullException(nameof(floorRules));
            if (floorRules.Count == 0)
                throw new ArgumentException("At least one floor is required.", nameof(floorRules));

            Wallet = new CoinWallet(initialCoins, RaiseStateChanged);
            floors = new FloorModel[floorRules.Count];
            HashSet<string> floorIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < floorRules.Count; i++)
            {
                FloorRules rules = floorRules[i];
                int expectedFloorNumber = i + 1;
                if (rules.FloorNumber != expectedFloorNumber)
                {
                    throw new ArgumentException(
                        $"Floor rules must be ordered and consecutive. Expected floor {expectedFloorNumber}.",
                        nameof(floorRules));
                }
                if (!floorIds.Add(rules.FloorId))
                {
                    throw new ArgumentException(
                        $"Floor ID '{rules.FloorId}' is duplicated.",
                        nameof(floorRules));
                }

                floors[i] = new FloorModel(rules, Wallet, i == 0, RaiseStateChanged);
            }
        }

        public CoinWallet Wallet { get; }
        public int Coins => Wallet.Coins;
        public IReadOnlyList<FloorModel> Floors => floors;
        public event Action<bool> StateChanged;

        public FloorModel GetFloor(int floorNumber)
        {
            if (floorNumber < 1 || floorNumber > floors.Length)
                throw new ArgumentOutOfRangeException(nameof(floorNumber));

            return floors[floorNumber - 1];
        }

        public bool TryUnlockFloor(int floorNumber)
        {
            if (!CanUnlockFloor(floorNumber))
                return false;

            FloorModel floor = floors[floorNumber - 1];
            if (!Wallet.TrySpend(floor.UnlockCostCoins))
                return false;

            floor.Unlock();
            return true;
        }

        public bool CanUnlockFloor(int floorNumber)
        {
            if (floorNumber <= 1 || floorNumber > floors.Length)
                return false;

            FloorModel floor = floors[floorNumber - 1];
            FloorModel precedingFloor = floors[floorNumber - 2];
            return !floor.IsUnlocked
                && precedingFloor.IsUnlocked
                && Coins >= floor.UnlockCostCoins;
        }

        public void Tick(float deltaSeconds)
        {
            for (int i = 0; i < floors.Length; i++)
                floors[i].Tick(deltaSeconds);
        }

        internal TowerSnapshot CaptureSnapshot()
        {
            FloorSnapshot[] snapshots = new FloorSnapshot[floors.Length];
            for (int i = 0; i < floors.Length; i++)
                snapshots[i] = floors[i].CaptureSnapshot();
            return new TowerSnapshot(Coins, snapshots);
        }

        internal void Restore(TowerSnapshot snapshot)
        {
            Wallet.Restore(snapshot.Coins);
            for (int i = 0; i < floors.Length; i++)
            {
                FloorSnapshot savedFloor = default;
                bool found = false;
                for (int j = 0; j < snapshot.Floors.Count; j++)
                {
                    if (snapshot.Floors[j].FloorId != floors[i].FloorId)
                        continue;
                    savedFloor = snapshot.Floors[j];
                    found = true;
                    break;
                }
                if (found)
                    floors[i].Restore(savedFloor);
            }
        }

        private void RaiseStateChanged(bool important) => StateChanged?.Invoke(important);
    }
}
