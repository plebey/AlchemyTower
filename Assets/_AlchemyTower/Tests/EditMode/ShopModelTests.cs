using NUnit.Framework;

namespace AlchemyTower.Tests
{
    public sealed class ShopModelTests
    {
        [Test]
        public void GlovesPurchase_DeductsOnceAndIncreasesTapBoostOnEveryFloor()
        {
            TowerModel tower = CreateTower(10000);
            Assert.That(tower.TryUnlockFloor(2), Is.True);

            Assert.That(tower.Shop.TryPurchase(ShopUpgradeId.AlchemicalGloves), Is.True);
            Assert.That(tower.Coins, Is.EqualTo(7350));

            tower.GetFloor(1).TapFloor();
            tower.GetFloor(2).TapFloor();

            Assert.That(tower.GetFloor(1).ProgressSeconds, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(tower.GetFloor(2).ProgressSeconds, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(tower.GetFloor(1).BrewDurationSeconds, Is.EqualTo(5f));
            Assert.That(tower.GetFloor(2).BrewDurationSeconds, Is.EqualTo(6f));
        }

        [Test]
        public void Gloves_AffectAutomatedFloorTaps()
        {
            TowerModel tower = CreateTower(10000);
            FloorModel floor = tower.GetFloor(1);
            while (floor.Level < 5)
                Assert.That(floor.TryUpgrade(), Is.True);
            Assert.That(floor.TryBuyAutomation(), Is.True);
            Assert.That(tower.Shop.TryPurchase(ShopUpgradeId.AlchemicalGloves), Is.True);

            floor.TapFloor();

            Assert.That(floor.ProgressSeconds, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void GoldenSeal_ChangesActualRewardAndNominalIncomeWithoutDoubleMultiplication()
        {
            TowerModel tower = CreateTower(6000);
            FloorModel floor = tower.GetFloor(1);

            Assert.That(tower.Shop.TryPurchase(ShopUpgradeId.GoldenSeal), Is.True);
            Assert.That(tower.Shop.GlobalRewardMultiplier, Is.EqualTo(1.05m));
            Assert.That(floor.CurrentRewardCoins, Is.EqualTo(11));
            Assert.That(floor.NominalIncomePerSecond, Is.EqualTo(2.2f).Within(0.0001f));

            tower.Tick(5f);
            Assert.That(floor.TryCollect(), Is.True);
            Assert.That(tower.Coins, Is.EqualTo(11));
            tower.Tick(5f);
            Assert.That(floor.TryCollect(), Is.True);
            Assert.That(tower.Coins, Is.EqualTo(22));
        }

        [Test]
        public void GoldenSeal_AppliesExactlyOnceToAutomatedCyclePayouts()
        {
            TowerModel tower = CreateTower(100000);
            FloorModel floor = tower.GetFloor(1);
            while (floor.Level < 5)
                Assert.That(floor.TryUpgrade(), Is.True);
            Assert.That(tower.Shop.TryPurchase(ShopUpgradeId.GoldenSeal), Is.True);
            Assert.That(floor.TryBuyAutomation(), Is.True);
            int beforePayout = tower.Coins;

            tower.Tick(10f);

            Assert.That(floor.CurrentRewardCoins, Is.EqualTo(16));
            Assert.That(tower.Coins - beforePayout, Is.EqualTo(32));
        }

        [Test]
        public void Purchase_WithInsufficientCoinsIsAtomic()
        {
            TowerModel tower = CreateTower(2499);
            int importantChanges = 0;
            tower.StateChanged += important => importantChanges += important ? 1 : 0;

            Assert.That(tower.Shop.TryPurchase(ShopUpgradeId.AlchemicalGloves), Is.False);

            Assert.That(tower.Coins, Is.EqualTo(2499));
            Assert.That(tower.Shop.AlchemicalGlovesLevel, Is.Zero);
            Assert.That(importantChanges, Is.Zero);
        }

        [Test]
        public void UpgradeCostsDoubleAndStopAtConfiguredMaximum()
        {
            TowerModel tower = CreateTower(200000);
            int[] expectedCosts = { 2500, 5000, 10000, 20000, 40000 };

            for (int level = 0; level < expectedCosts.Length; level++)
            {
                Assert.That(tower.Shop.GetCurrentCostCoins(ShopUpgradeId.AlchemicalGloves),
                    Is.EqualTo(expectedCosts[level]));
                Assert.That(tower.Shop.TryPurchase(ShopUpgradeId.AlchemicalGloves), Is.True);
            }

            Assert.That(tower.Shop.AlchemicalGlovesLevel, Is.EqualTo(5));
            Assert.That(tower.Shop.TapBoostBonusSeconds, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(tower.Shop.GetCurrentCostCoins(ShopUpgradeId.AlchemicalGloves), Is.Zero);
            Assert.That(tower.Shop.TryPurchase(ShopUpgradeId.AlchemicalGloves), Is.False);
        }

        private static TowerModel CreateTower(int initialCoins)
        {
            return new TowerModel(new[]
            {
                new FloorRules("first_floor", 5f, 0.35f, 10, 30, 1.10m, 1.15m),
                new FloorRules("second_floor", 6f, 0.35f, 40, 120, 1.10m, 1.15m,
                    floorNumber: 2, unlockCostCoins: 150)
            }, initialCoins);
        }
    }
}
