using NUnit.Framework;

namespace AlchemyTower.Tests
{
    public sealed class TowerModelTests
    {
        [Test]
        public void Floors_CollectRewardsIntoOneSharedWallet()
        {
            TowerModel tower = CreateTower(initialCoins: 150);
            FloorModel first = tower.GetFloor(1);
            FloorModel second = tower.GetFloor(2);

            Assert.That(tower.TryUnlockFloor(2), Is.True);
            tower.Tick(5f);
            Assert.That(first.TryCollect(), Is.True);
            tower.Tick(1f);
            Assert.That(second.TryCollect(), Is.True);

            Assert.That(tower.Coins, Is.EqualTo(50));
            Assert.That(first.Coins, Is.EqualTo(50));
            Assert.That(second.Coins, Is.EqualTo(50));
        }

        [Test]
        public void LockedFloor_DoesNotProduceOrAcceptActions()
        {
            TowerModel tower = CreateTower(initialCoins: 149);
            FloorModel second = tower.GetFloor(2);

            tower.Tick(60f);

            Assert.That(tower.TryUnlockFloor(2), Is.False);
            Assert.That(second.IsUnlocked, Is.False);
            Assert.That(second.ProgressSeconds, Is.Zero);
            Assert.That(second.TapFloor(), Is.EqualTo(FloorTapResult.Locked));
            Assert.That(second.TryUpgrade(), Is.False);
            Assert.That(second.TryBuyAutomation(), Is.False);
            Assert.That(tower.Coins, Is.EqualTo(149));
        }

        [Test]
        public void UnlockFloor_DeductsOnceAndStartsOnlyThatFloor()
        {
            TowerModel tower = CreateTower(initialCoins: 200);
            FloorModel first = tower.GetFloor(1);
            FloorModel second = tower.GetFloor(2);
            tower.Tick(2f);

            Assert.That(tower.CanUnlockFloor(2), Is.True);
            Assert.That(tower.TryUnlockFloor(2), Is.True);
            Assert.That(tower.CanUnlockFloor(2), Is.False);
            Assert.That(tower.TryUnlockFloor(2), Is.False);

            Assert.That(tower.Coins, Is.EqualTo(50));
            Assert.That(first.ProgressSeconds, Is.EqualTo(2f));
            Assert.That(second.IsUnlocked, Is.True);
            Assert.That(second.ProgressSeconds, Is.Zero);
            Assert.That(second.Phase, Is.EqualTo(FloorPhase.Brewing));
        }

        [Test]
        public void Floors_KeepIndependentProgressLevelsAndAutomation()
        {
            TowerModel tower = CreateTower(initialCoins: 4000);
            FloorModel first = tower.GetFloor(1);
            FloorModel second = tower.GetFloor(2);
            Assert.That(tower.TryUnlockFloor(2), Is.True);

            UpgradeToLevelFive(second);
            Assert.That(second.TryBuyAutomation(), Is.True);
            tower.Tick(6f);

            Assert.That(first.Level, Is.EqualTo(1));
            Assert.That(first.IsAutomated, Is.False);
            Assert.That(first.Phase, Is.EqualTo(FloorPhase.ReadyToCollect));
            Assert.That(first.ProgressSeconds, Is.EqualTo(5f));
            Assert.That(second.Level, Is.EqualTo(5));
            Assert.That(second.IsAutomated, Is.True);
            Assert.That(second.Phase, Is.EqualTo(FloorPhase.Brewing));
            Assert.That(second.ProgressSeconds, Is.Zero);
            Assert.That(tower.Coins, Is.EqualTo(309));
        }

        [Test]
        public void Automation_IsRejectedBelowLevelFiveEvenWithEnoughCoins()
        {
            TowerModel tower = CreateTower(initialCoins: 10000);
            FloorModel first = tower.GetFloor(1);

            Assert.That(first.TryBuyAutomation(), Is.False);

            Assert.That(first.Level, Is.EqualTo(1));
            Assert.That(first.IsAutomated, Is.False);
            Assert.That(tower.Coins, Is.EqualTo(10000));
        }

        [Test]
        public void Automation_BecomesPurchasableAtLevelFive()
        {
            TowerModel tower = CreateTower(initialCoins: 1000);
            FloorModel first = tower.GetFloor(1);
            UpgradeToLevelFive(first);

            Assert.That(first.CanBuyAutomation, Is.True);
            Assert.That(first.TryBuyAutomation(), Is.True);

            Assert.That(first.IsAutomated, Is.True);
            Assert.That(tower.Coins, Is.EqualTo(749));
        }

        [Test]
        public void ManualCollection_PaysCurrentRewardOnceAndRestartsProduction()
        {
            TowerModel tower = CreateTower();
            FloorModel first = tower.GetFloor(1);
            tower.Tick(5f);

            Assert.That(first.TryCollect(), Is.True);
            Assert.That(first.TryCollect(), Is.False);

            Assert.That(tower.Coins, Is.EqualTo(10));
            Assert.That(first.Phase, Is.EqualTo(FloorPhase.Brewing));
            Assert.That(first.ProgressSeconds, Is.Zero);
        }

        private static void UpgradeToLevelFive(FloorModel floor)
        {
            while (floor.Level < 5)
                Assert.That(floor.TryUpgrade(), Is.True);
        }

        private static TowerModel CreateTower(int initialCoins = 0)
        {
            return new TowerModel(
                new[]
                {
                    new FloorRules("first_floor", 5f, 0.35f, 10, 30, 1.10m, 1.15m),
                    new FloorRules(
                        "second_floor",
                        6f,
                        0.35f,
                        40,
                        120,
                        1.10m,
                        1.15m,
                        floorNumber: 2,
                        unlockCostCoins: 150)
                },
                initialCoins);
        }
    }
}
