using NUnit.Framework;

namespace AlchemyTower.Tests
{
    public sealed class TopHUDTests
    {
        private static readonly TopHUD.CompactNumberSuffix[] Suffixes =
        {
            new TopHUD.CompactNumberSuffix { threshold = 1_000_000, divisor = 1_000_000, suffix = "m" },
            new TopHUD.CompactNumberSuffix { threshold = 1_000, divisor = 1_000, suffix = "k" },
            new TopHUD.CompactNumberSuffix { threshold = 1_000_000_000, divisor = 1_000_000_000, suffix = "b" }
        };

        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(1_000, "1k")]
        [TestCase(1_250, "1.3k")]
        [TestCase(999_999, "1000k")]
        [TestCase(1_000_000, "1m")]
        [TestCase(12_500_000, "12.5m")]
        [TestCase(1_000_000_000, "1b")]
        [TestCase(int.MaxValue, "2.1b")]
        public void FormatCompact_UsesLargestMatchingAuthoredSuffix(int value, string expected)
        {
            Assert.That(TopHUD.FormatCompact(value, Suffixes), Is.EqualTo(expected));
        }

        [Test]
        public void FormatCompact_DoesNotRequireSuffixConfiguration()
        {
            Assert.That(TopHUD.FormatCompact(12_345, null), Is.EqualTo("12,345"));
        }
    }
}
