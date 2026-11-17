using NUnit.Framework;

namespace AlchemyTower.Tests
{
    public sealed class OfflineProgressTests
    {
        [Test]
        public void AutomatedFloor_UsesPartialProgressCurrentRewardAndFinalEfficiency()
        {
            TowerModel tower = CreateTower(initialCoins: 20000);
            FloorModel floor = tower.GetFloor(1);
            UpgradeToAutomationLevel(floor);
            floor.Tick(2f);
            Assert.That(floor.TryBuyAutomation(), Is.True);
            Assert.That(tower.Shop.TryPurchase(ShopUpgradeId.GoldenSeal), Is.True);
            int coinsBefore = tower.Coins;

            OfflineProgressResult result = OfflineProgressCalculator.Apply(tower,
                new OfflineProgressRules(true, 6d, 0.25m, 60d), 12.5d);

            Assert.That(floor.CurrentRewardCoins, Is.EqualTo(16), "Golden Seal is included once.");
            Assert.That(result.CompletedCycles, Is.EqualTo(2));
            Assert.That(result.RewardCoins, Is.EqualTo(8));
            Assert.That(tower.Coins, Is.EqualTo(coinsBefore + 8));
            Assert.That(floor.ProgressSeconds, Is.EqualTo(4.5f).Within(0.0001f));
            Assert.That(floor.Phase, Is.EqualTo(FloorPhase.Brewing));
        }

        [Test]
        public void ManualFloor_FinishesAtReadyWithoutRewardOrAutomaticCollection()
        {
            TowerModel tower = CreateTower();
            FloorModel floor = tower.GetFloor(1);
            floor.Tick(4f);

            OfflineProgressResult result = OfflineProgressCalculator.Apply(tower,
                new OfflineProgressRules(true, 6d, 0.25m, 60d), 100d);

            Assert.That(result.RewardCoins, Is.Zero);
            Assert.That(tower.Coins, Is.Zero);
            Assert.That(floor.Phase, Is.EqualTo(FloorPhase.ReadyToCollect));
            Assert.That(floor.ProgressSeconds, Is.EqualTo(5f));

            OfflineProgressCalculator.Apply(tower,
                new OfflineProgressRules(true, 6d, 0.25m, 60d), 100d);
            Assert.That(floor.Phase, Is.EqualTo(FloorPhase.ReadyToCollect));
            Assert.That(tower.Coins, Is.Zero);
        }

        [Test]
        public void LockedFloor_DoesNotParticipate()
        {
            TowerModel tower = CreateTower();

            OfflineProgressCalculator.Apply(tower,
                new OfflineProgressRules(true, 6d, 1m, 0d), 100d);

            FloorModel locked = tower.GetFloor(2);
            Assert.That(locked.IsUnlocked, Is.False);
            Assert.That(locked.ProgressSeconds, Is.Zero);
            Assert.That(locked.Phase, Is.EqualTo(FloorPhase.Brewing));
        }

        [Test]
        public void AbsenceLongerThanLimit_UsesOnlyConfiguredDuration()
        {
            TowerModel tower = CreateTower(initialCoins: 1000);
            FloorModel floor = tower.GetFloor(1);
            UpgradeToAutomationLevel(floor);
            Assert.That(floor.TryBuyAutomation(), Is.True);

            OfflineProgressResult result = OfflineProgressCalculator.Apply(tower,
                new OfflineProgressRules(true, 0.001d, 1m, 0d), 100d);

            Assert.That(result.AbsenceSeconds, Is.EqualTo(100d));
            Assert.That(result.CreditedSeconds, Is.EqualTo(3.6d).Within(0.0001d));
            Assert.That(result.CompletedCycles, Is.Zero);
            Assert.That(floor.ProgressSeconds, Is.EqualTo(3.6f).Within(0.0001f));
        }

        [Test]
        public void MultipleAutomatedFloors_AreSummedBeforeSingleEconomyRounding()
        {
            TowerModel tower = CreateTower(initialCoins: 50000);
            Assert.That(tower.TryUnlockFloor(2), Is.True);
            for (int floorNumber = 1; floorNumber <= 2; floorNumber++)
            {
                FloorModel floor = tower.GetFloor(floorNumber);
                UpgradeToAutomationLevel(floor);
                Assert.That(floor.TryBuyAutomation(), Is.True);
            }
            int coinsBefore = tower.Coins;

            OfflineProgressResult result = OfflineProgressCalculator.Apply(tower,
                new OfflineProgressRules(true, 6d, 0.10m, 0d), 6d);

            Assert.That(tower.GetFloor(1).CurrentRewardCoins, Is.EqualTo(15));
            Assert.That(tower.GetFloor(2).CurrentRewardCoins, Is.EqualTo(59));
            Assert.That(result.CompletedCycles, Is.EqualTo(2));
            Assert.That(result.RewardCoins, Is.EqualTo(7), "Round((15 + 59) * 0.10) once.");
            Assert.That(tower.Coins, Is.EqualTo(coinsBefore + 7));
        }

        [Test]
        public void NegativeOrDisabledAbsence_DoesNotChangeState()
        {
            TowerModel tower = CreateTower();
            tower.GetFloor(1).Tick(1f);

            OfflineProgressResult negative = OfflineProgressCalculator.Apply(tower,
                new OfflineProgressRules(true, 6d, 0.25m, 0d), -100d);
            OfflineProgressResult disabled = OfflineProgressCalculator.Apply(tower,
                new OfflineProgressRules(false, 6d, 0.25m, 0d), 100d);

            Assert.That(negative.CreditedSeconds, Is.Zero);
            Assert.That(disabled.CreditedSeconds, Is.Zero);
            Assert.That(tower.GetFloor(1).ProgressSeconds, Is.EqualTo(1f));
        }

        [TestCase(-1d, 0.25, 0d)]
        [TestCase(6d, -0.01, 0d)]
        [TestCase(6d, 0.25, -1d)]
        public void Rules_RejectInvalidAuthoredValues(double hours, double multiplier, double popupSeconds)
        {
            Assert.That(() => new OfflineProgressRules(true, hours, (decimal)multiplier, popupSeconds),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        private static TowerModel CreateTower(int initialCoins = 0)
        {
            return new TowerModel(new[]
            {
                new FloorRules("first_floor", 5f, 0.35f, 10, 30, 1.10m, 1.15m),
                new FloorRules("second_floor", 6f, 0.35f, 40, 120, 1.10m, 1.15m,
                    floorNumber: 2, unlockCostCoins: 150)
            }, initialCoins);
        }

        private static void UpgradeToAutomationLevel(FloorModel floor)
        {
            while (floor.Level < floor.AutomationRequiredLevel)
                Assert.That(floor.TryUpgrade(), Is.True);
        }
    }
}
