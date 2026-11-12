using NUnit.Framework;

namespace AlchemyTower.Tests
{
    public sealed class TowerScrollNavigatorTests
    {
        [Test]
        public void TargetPosition_CentersFloorWhenBoundsAllowIt()
        {
            float result = TowerScrollNavigator.CalculateTargetNormalizedPosition(
                contentHeight: 2400f,
                viewportHeight: 800f,
                targetCenterFromBottom: 1200f);

            Assert.That(result, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void TargetPosition_ClampsToBottomForLowestFloor()
        {
            float result = TowerScrollNavigator.CalculateTargetNormalizedPosition(2400f, 800f, 200f);

            Assert.That(result, Is.Zero);
        }

        [Test]
        public void TargetPosition_ClampsToTopForHighestFloor()
        {
            float result = TowerScrollNavigator.CalculateTargetNormalizedPosition(2400f, 800f, 2300f);

            Assert.That(result, Is.EqualTo(1f));
        }

        [Test]
        public void TargetPosition_StaysStableWhenContentFitsViewport()
        {
            float result = TowerScrollNavigator.CalculateTargetNormalizedPosition(600f, 800f, 300f);

            Assert.That(result, Is.Zero);
        }
    }
}
