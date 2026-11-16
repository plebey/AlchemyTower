using NUnit.Framework;

namespace AlchemyTower.Tests
{
    public sealed class SafeAreaContentLayoutTests
    {
        [Test]
        public void CalculateInsets_UsesActualBarsAndGaps()
        {
            SafeAreaContentLayout.CalculateInsets(112f, 152f, 12f, 10f, out float top, out float bottom);

            Assert.That(top, Is.EqualTo(124f));
            Assert.That(bottom, Is.EqualTo(162f));
        }

        [Test]
        public void CalculateInsets_ClampsNegativeAuthoredValues()
        {
            SafeAreaContentLayout.CalculateInsets(-20f, -30f, -4f, -8f, out float top, out float bottom);

            Assert.That(top, Is.Zero);
            Assert.That(bottom, Is.Zero);
        }
    }
}
