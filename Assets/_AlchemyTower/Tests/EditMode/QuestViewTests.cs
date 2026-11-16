using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlchemyTower.Tests
{
    public sealed class QuestViewTests
    {
        private const string NavigationPath = "Assets/_AlchemyTower/Prefabs/UI/BottomNavigation.prefab";
        private const string QuestCardPath = "Assets/_AlchemyTower/Prefabs/UI/QuestCard.prefab";

        [Test]
        public void NavigationContainsQuestBookInsteadOfComingSoonPlaceholder()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NavigationPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<BottomNavigation>().QuestBookView, Is.Not.Null);
            Assert.That(prefab.transform.Find("Future Pages/Tasks Page/Quest Cards"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Future Pages/Tasks Page/Coming Soon Label"), Is.Null);
        }

        [Test]
        public void QuestCardIsSeparateEditablePrefabWithReplaceableIcon()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuestCardPath);

            Assert.That(prefab, Is.Not.Null);
            QuestCardView card = prefab.GetComponent<QuestCardView>();
            Assert.That(card, Is.Not.Null);
            Assert.That(card.IconImage, Is.Not.Null);
            Assert.That(card.IconImage.sprite, Is.Not.Null);
        }
    }
}
