using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower.Tests
{
    public sealed class OfflineProgressAssetTests
    {
        private const string ConfigPath = "Assets/_AlchemyTower/Data/OfflineProgressConfig.asset";
        private const string PopupPath = "Assets/_AlchemyTower/Prefabs/UI/OfflineRewardPopup.prefab";

        [Test]
        public void ConfigAssetHasRequestedDefaults()
        {
            OfflineProgressConfig config = AssetDatabase.LoadAssetAtPath<OfflineProgressConfig>(ConfigPath);

            Assert.That(config, Is.Not.Null);
            OfflineProgressRules rules = config.CreateRules();
            Assert.That(rules.Enabled, Is.True);
            Assert.That(rules.MaxOfflineHours, Is.EqualTo(6d));
            Assert.That(rules.IncomeMultiplier, Is.EqualTo(0.25m));
            Assert.That(rules.MinPopupAbsenceSeconds, Is.EqualTo(60d));
        }

        [Test]
        public void PopupIsAuthoredStandardUguiWithReplaceableImagesAndCloseButton()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<OfflineRewardPopup>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Image>(true).Length, Is.GreaterThanOrEqualTo(4));
            Button button = prefab.GetComponentInChildren<Button>(true);
            Assert.That(button, Is.Not.Null);
            Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero,
                "The runtime listener only closes the already-paid popup.");
            Assert.That(prefab.GetComponentInChildren<RoundedImage>(true), Is.Null);
            Assert.That(prefab.GetComponentInChildren<RoundedCorners>(true), Is.Null);
        }
    }
}
