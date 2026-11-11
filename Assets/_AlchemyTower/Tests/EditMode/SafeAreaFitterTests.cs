using NUnit.Framework;
using UnityEngine;

namespace AlchemyTower.Tests
{
    public sealed class SafeAreaFitterTests
    {
        [Test]
        public void CalculateAnchors_ConvertsPortraitNotchToNormalizedRect()
        {
            SafeAreaFitter.CalculateAnchors(
                new Rect(0f, 100f, 1080f, 2200f),
                1080f,
                2400f,
                out Vector2 anchorMin,
                out Vector2 anchorMax);

            Assert.That(anchorMin.x, Is.EqualTo(0f));
            Assert.That(anchorMin.y, Is.EqualTo(100f / 2400f).Within(0.0001f));
            Assert.That(anchorMax.x, Is.EqualTo(1f));
            Assert.That(anchorMax.y, Is.EqualTo(2300f / 2400f).Within(0.0001f));
        }

        [Test]
        public void CalculateAnchors_InvalidScreenSizeFallsBackToFullCanvas()
        {
            SafeAreaFitter.CalculateAnchors(
                new Rect(10f, 10f, 100f, 100f),
                0f,
                0f,
                out Vector2 anchorMin,
                out Vector2 anchorMax);

            Assert.That(anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(anchorMax, Is.EqualTo(Vector2.one));
        }
    }
}
