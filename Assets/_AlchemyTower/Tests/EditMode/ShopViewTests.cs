using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower.Tests
{
    public sealed class ShopViewTests
    {
        private const string NavigationPath = "Assets/_AlchemyTower/Prefabs/UI/BottomNavigation.prefab";
        private const string GlovesPath = "Assets/_AlchemyTower/Prefabs/UI/AlchemicalGlovesCard.prefab";
        private const string SealPath = "Assets/_AlchemyTower/Prefabs/UI/GoldenSealCard.prefab";

        [Test]
        public void AuthoredCardsHaveReplaceableIconsAndCorrectUpgradeIds()
        {
            AssertCard(GlovesPath, ShopUpgradeId.AlchemicalGloves);
            AssertCard(SealPath, ShopUpgradeId.GoldenSeal);
        }

        [Test]
        public void NavigationPrefabContainsBoundShopPresenterInsteadOfComingSoonLabel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NavigationPath);
            Assert.That(prefab, Is.Not.Null);
            BottomNavigation navigation = prefab.GetComponent<BottomNavigation>();
            Assert.That(navigation.ShopView, Is.Not.Null);
            Assert.That(prefab.transform.Find("Future Pages/Shop Page/Shop Cards"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Future Pages/Shop Page/Coming Soon Label"), Is.Null);
        }

        private static void AssertCard(string path, ShopUpgradeId expectedId)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            ShopItemView card = prefab.GetComponent<ShopItemView>();
            Assert.That(card, Is.Not.Null);
            Assert.That(card.UpgradeId, Is.EqualTo(expectedId));
            Image icon = prefab.transform.Find("Replaceable Icon").GetComponent<Image>();
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.sprite, Is.Not.Null);
        }
    }
}
