using System;

namespace AlchemyTower
{
    public enum ShopUpgradeId
    {
        AlchemicalGloves,
        GoldenSeal
    }

    public readonly struct ShopUpgradeRules
    {
        public ShopUpgradeRules(int initialCostCoins, int costMultiplier, int maxLevel)
        {
            if (initialCostCoins <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCostCoins));
            if (costMultiplier < 1)
                throw new ArgumentOutOfRangeException(nameof(costMultiplier));
            if (maxLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(maxLevel));

            InitialCostCoins = initialCostCoins;
            CostMultiplier = costMultiplier;
            MaxLevel = maxLevel;
        }

        public int InitialCostCoins { get; }
        public int CostMultiplier { get; }
        public int MaxLevel { get; }
    }

    public readonly struct ShopRules
    {
        public ShopRules(
            ShopUpgradeRules alchemicalGloves,
            float gloveTapBoostSecondsPerLevel,
            ShopUpgradeRules goldenSeal,
            int goldenSealRewardPercentPerLevel)
        {
            if (float.IsNaN(gloveTapBoostSecondsPerLevel)
                || float.IsInfinity(gloveTapBoostSecondsPerLevel)
                || gloveTapBoostSecondsPerLevel < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(gloveTapBoostSecondsPerLevel));
            }
            if (goldenSealRewardPercentPerLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(goldenSealRewardPercentPerLevel));

            AlchemicalGloves = alchemicalGloves;
            GloveTapBoostSecondsPerLevel = gloveTapBoostSecondsPerLevel;
            GoldenSeal = goldenSeal;
            GoldenSealRewardPercentPerLevel = goldenSealRewardPercentPerLevel;
        }

        public ShopUpgradeRules AlchemicalGloves { get; }
        public float GloveTapBoostSecondsPerLevel { get; }
        public ShopUpgradeRules GoldenSeal { get; }
        public int GoldenSealRewardPercentPerLevel { get; }

        public static ShopRules Default => new ShopRules(
            new ShopUpgradeRules(2500, 2, 5),
            0.05f,
            new ShopUpgradeRules(6000, 2, 5),
            5);
    }

    public readonly struct ShopSnapshot
    {
        public ShopSnapshot(int alchemicalGlovesLevel, int goldenSealLevel)
        {
            AlchemicalGlovesLevel = alchemicalGlovesLevel;
            GoldenSealLevel = goldenSealLevel;
        }

        public int AlchemicalGlovesLevel { get; }
        public int GoldenSealLevel { get; }
    }

    public sealed class ShopModel
    {
        private readonly ShopRules rules;
        private readonly CoinWallet wallet;
        private readonly Action<bool> stateChanged;

        internal ShopModel(ShopRules rules, CoinWallet wallet, Action<bool> stateChanged)
        {
            this.rules = rules;
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.stateChanged = stateChanged;
        }

        public int AlchemicalGlovesLevel { get; private set; }
        public int GoldenSealLevel { get; private set; }
        public float TapBoostBonusSeconds => AlchemicalGlovesLevel * rules.GloveTapBoostSecondsPerLevel;
        public decimal GlobalRewardMultiplier => 1m
            + GoldenSealLevel * rules.GoldenSealRewardPercentPerLevel / 100m;

        public float GetTotalTapBoostSeconds(float baseTapBoostSeconds, int glovesLevel)
        {
            return baseTapBoostSeconds + glovesLevel * rules.GloveTapBoostSecondsPerLevel;
        }

        public decimal GetRewardMultiplier(int goldenSealLevel)
        {
            return 1m + goldenSealLevel * rules.GoldenSealRewardPercentPerLevel / 100m;
        }

        public int GetLevel(ShopUpgradeId id) => id == ShopUpgradeId.AlchemicalGloves
            ? AlchemicalGlovesLevel
            : GoldenSealLevel;

        public int GetMaxLevel(ShopUpgradeId id) => GetRules(id).MaxLevel;

        public int GetCurrentCostCoins(ShopUpgradeId id)
        {
            int level = GetLevel(id);
            ShopUpgradeRules upgrade = GetRules(id);
            if (level >= upgrade.MaxLevel)
                return 0;

            long cost = upgrade.InitialCostCoins;
            for (int i = 0; i < level; i++)
            {
                if (cost > int.MaxValue / upgrade.CostMultiplier)
                    return int.MaxValue;
                cost *= upgrade.CostMultiplier;
            }

            return (int)cost;
        }

        public bool CanPurchase(ShopUpgradeId id)
        {
            return GetLevel(id) < GetMaxLevel(id)
                && wallet.Coins >= GetCurrentCostCoins(id);
        }

        public bool TryPurchase(ShopUpgradeId id)
        {
            if (GetLevel(id) >= GetMaxLevel(id))
                return false;

            int cost = GetCurrentCostCoins(id);
            if (!wallet.TrySpend(cost, notifyStateChanged: false))
                return false;

            if (id == ShopUpgradeId.AlchemicalGloves)
                AlchemicalGlovesLevel++;
            else
                GoldenSealLevel++;

            stateChanged?.Invoke(true);
            return true;
        }

        internal ShopSnapshot CaptureSnapshot()
        {
            return new ShopSnapshot(AlchemicalGlovesLevel, GoldenSealLevel);
        }

        internal void Restore(ShopSnapshot snapshot)
        {
            AlchemicalGlovesLevel = snapshot.AlchemicalGlovesLevel;
            GoldenSealLevel = snapshot.GoldenSealLevel;
        }

        private ShopUpgradeRules GetRules(ShopUpgradeId id)
        {
            return id == ShopUpgradeId.AlchemicalGloves
                ? rules.AlchemicalGloves
                : rules.GoldenSeal;
        }
    }
}
