using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower.Tests
{
    public sealed class BottomNavigationTests
    {
        private const string PrefabPath = "Assets/_AlchemyTower/Prefabs/UI/BottomNavigation.prefab";

        [Test]
        public void Select_ActivatesExactlyOneCentralScreenAndReusesTowerScreen()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            Image shopBackground = prefab.transform.Find("Future Pages/Shop Page").GetComponent<Image>();
            Image tasksBackground = prefab.transform.Find("Future Pages/Tasks Page").GetComponent<Image>();
            Assert.That(shopBackground.color.a, Is.EqualTo(1f));
            Assert.That(tasksBackground.color.a, Is.EqualTo(1f));
            Assert.That(shopBackground.sprite, Is.Null);
            Assert.That(tasksBackground.sprite, Is.Null);

            GameObject instance = Object.Instantiate(prefab);
            GameObject towerScreen = new GameObject("Test Tower Screen");
            try
            {
                BottomNavigation navigation = instance.GetComponent<BottomNavigation>();
                GameObject shopPage = instance.transform.Find("Future Pages/Shop Page").gameObject;
                GameObject tasksPage = instance.transform.Find("Future Pages/Tasks Page").gameObject;
                navigation.SetTowerScreen(towerScreen);

                navigation.Select(0);
                Assert.That(shopPage.activeSelf, Is.True);
                Assert.That(towerScreen.activeSelf, Is.False);
                Assert.That(tasksPage.activeSelf, Is.False);

                navigation.Select(1);
                Assert.That(shopPage.activeSelf, Is.False);
                Assert.That(towerScreen.activeSelf, Is.True);
                Assert.That(tasksPage.activeSelf, Is.False);

                navigation.Select(2);
                Assert.That(shopPage.activeSelf, Is.False);
                Assert.That(towerScreen.activeSelf, Is.False);
                Assert.That(tasksPage.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(towerScreen);
            }
        }

        [Test]
        public void AuthoredIconSourceImages_AreNotReplacedByInitializationOrSelection()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            Texture2D texture = new Texture2D(4, 4);
            Sprite authoredSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            try
            {
                BottomNavigation navigation = instance.GetComponent<BottomNavigation>();
                string[] itemNames = { "Shop", "Tower", "Tasks" };
                Image[] icons = new Image[itemNames.Length];
                for (int i = 0; i < itemNames.Length; i++)
                {
                    Transform item = instance.transform.Find($"Navigation Bar/Items/{itemNames[i]} Item");
                    Button button = item.GetComponent<Button>();
                    icons[i] = item.Find($"{itemNames[i]} Icon").GetComponent<Image>();
                    icons[i].sprite = authoredSprite;

                    Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
                    Assert.That(button.targetGraphic, Is.Not.SameAs(icons[i]));
                }

                MethodInfo initialize = typeof(BottomNavigation).GetMethod(
                    "ApplyAuthoredContent",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(initialize, Is.Not.Null);
                initialize.Invoke(navigation, null);

                for (int selected = 0; selected < itemNames.Length; selected++)
                {
                    navigation.Select(selected);
                    for (int icon = 0; icon < icons.Length; icon++)
                        Assert.That(icons[icon].sprite, Is.SameAs(authoredSprite));
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(authoredSprite);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
