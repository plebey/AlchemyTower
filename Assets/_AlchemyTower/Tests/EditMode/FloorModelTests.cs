using NUnit.Framework;

namespace AlchemyTower.Tests
{
    public sealed class FloorModelTests
    {
        [Test]
        public void Tick_ReachesReadyWithoutPaying_AndStopsThere()
        {
            FloorModel model = CreateModel();

            model.Tick(5f);
            model.Tick(10f);

            Assert.That(model.Phase, Is.EqualTo(FloorPhase.ReadyToCollect));
            Assert.That(model.ProgressSeconds, Is.EqualTo(5f));
            Assert.That(model.Coins, Is.Zero);
        }

        [Test]
        public void TapThatFinishesBrewing_DoesNotCollectInSameAction()
        {
            FloorModel model = CreateModel();
            model.Tick(4.8f);

            FloorTapResult result = model.TapFloor();

            Assert.That(result, Is.EqualTo(FloorTapResult.BecameReady));
            Assert.That(model.Phase, Is.EqualTo(FloorPhase.ReadyToCollect));
            Assert.That(model.Coins, Is.Zero);
        }

        [Test]
        public void BrewingTap_AddsConfiguredPointThreeFiveSeconds()
        {
            FloorModel model = CreateModel();

            FloorTapResult result = model.TapFloor();

            Assert.That(result, Is.EqualTo(FloorTapResult.Progressed));
            Assert.That(model.ProgressSeconds, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(model.Coins, Is.Zero);
        }

        [Test]
        public void ReadyTap_CollectsExactlyOnce_AndStartsNextCycle()
        {
            FloorModel model = CreateModel();
            model.Tick(5f);

            FloorTapResult result = model.TapFloor();
            FloorTapResult immediateSecondTap = model.TapFloor();
            bool duplicateCollect = model.TryCollect();

            Assert.That(result, Is.EqualTo(FloorTapResult.Collected));
            Assert.That(immediateSecondTap, Is.EqualTo(FloorTapResult.Progressed));
            Assert.That(duplicateCollect, Is.False);
            Assert.That(model.Coins, Is.EqualTo(10));
            Assert.That(model.Phase, Is.EqualTo(FloorPhase.Brewing));
            Assert.That(model.ProgressSeconds, Is.EqualTo(0.35f).Within(0.0001f));
        }

        [Test]
        public void UpgradeWithoutEnoughCoins_DoesNotChangeState()
        {
            FloorModel model = CreateModel(initialCoins: 29);
            model.Tick(2f);

            bool upgraded = model.TryUpgrade();

            Assert.That(upgraded, Is.False);
            Assert.That(model.Coins, Is.EqualTo(29));
            Assert.That(model.Level, Is.EqualTo(1));
            Assert.That(model.ProgressSeconds, Is.EqualTo(2f));
            Assert.That(model.Phase, Is.EqualTo(FloorPhase.Brewing));
        }

        [Test]
        public void RepeatableUpgrades_UseConfiguredGrowthAndDoNotChangeProduction()
        {
            FloorModel model = CreateModel(initialCoins: 100);
            model.Tick(5f);

            Assert.That(model.CurrentUpgradeCostCoins, Is.EqualTo(30));
            Assert.That(model.TryUpgrade(), Is.True);
            Assert.That(model.Level, Is.EqualTo(2));
            Assert.That(model.CurrentRewardCoins, Is.EqualTo(11));
            Assert.That(model.CurrentUpgradeCostCoins, Is.EqualTo(35));
            Assert.That(model.Coins, Is.EqualTo(70));
            Assert.That(model.Phase, Is.EqualTo(FloorPhase.ReadyToCollect));
            Assert.That(model.ProgressSeconds, Is.EqualTo(5f));

            Assert.That(model.TryCollect(), Is.True);
            Assert.That(model.Coins, Is.EqualTo(81));

            Assert.That(model.TryUpgrade(), Is.True);
            Assert.That(model.Level, Is.EqualTo(3));
            Assert.That(model.CurrentRewardCoins, Is.EqualTo(12));
            Assert.That(model.CurrentUpgradeCostCoins, Is.EqualTo(40));
            Assert.That(model.Coins, Is.EqualTo(46));
            Assert.That(model.BrewDurationSeconds, Is.EqualTo(5f));
        }

        [TestCase("10.49", 10)]
        [TestCase("10.50", 11)]
        [TestCase("34.50", 35)]
        public void RoundCoins_UsesNearestWithMidpointsAwayFromZero(string input, int expected)
        {
            decimal value = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(EconomyMath.RoundCoins(value), Is.EqualTo(expected));
        }

        private static FloorModel CreateModel(int initialCoins = 0)
        {
            FloorRules rules = new FloorRules(
                "first_floor",
                5f,
                0.35f,
                10,
                30,
                1.10m,
                1.15m);

            return new FloorModel(rules, initialCoins);
        }
    }
}
