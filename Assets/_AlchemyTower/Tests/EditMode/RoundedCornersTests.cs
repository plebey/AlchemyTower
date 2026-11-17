using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower.Tests
{
    public sealed class RoundedCornersTests
    {
        private const string ExamplesPrefabPath =
            "Assets/_AlchemyTower/Prefabs/Tests/RoundedCornersExamples.prefab";

        [Test]
        public void EffectiveRadiiAreClampedAndSupportIndividualCorners()
        {
            GameObject gameObject = CreateImageObject(new Vector2(100f, 40f));
            try
            {
                RoundedCorners corners = gameObject.AddComponent<RoundedCorners>();
                corners.UseIndividualCorners = true;
                corners.TopLeftRadius = 100f;
                corners.TopRightRadius = 15f;
                corners.BottomRightRadius = 10f;
                corners.BottomLeftRadius = 5f;

                Assert.That(corners.GetEffectiveCornerRadii(), Is.EqualTo(new Vector4(20f, 15f, 10f, 5f)));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ModifierKeepsEveryStandardImageModeAndAuthoredProperties()
        {
            foreach (Image.Type type in new[]
                     {
                         Image.Type.Simple,
                         Image.Type.Sliced,
                         Image.Type.Tiled,
                         Image.Type.Filled
                     })
            {
                GameObject gameObject = CreateImageObject(new Vector2(120f, 80f));
                try
                {
                    Image image = gameObject.GetComponent<Image>();
                    image.type = type;
                    image.color = new Color(0.2f, 0.4f, 0.8f, 0.6f);
                    image.preserveAspect = true;
                    image.fillAmount = 0.35f;
                    RoundedCorners corners = gameObject.AddComponent<RoundedCorners>();

                    Material modified = corners.GetModifiedMaterial(Graphic.defaultGraphicMaterial);

                    Assert.That(modified, Is.Not.Null);
                    Assert.That(modified.shader.name, Is.EqualTo("AlchemyTower/UI/Rounded Corners"));
                    Assert.That(image.type, Is.EqualTo(type));
                    Assert.That(image.color, Is.EqualTo(new Color(0.2f, 0.4f, 0.8f, 0.6f)));
                    Assert.That(image.preserveAspect, Is.True);
                    Assert.That(image.fillAmount, Is.EqualTo(0.35f).Within(0.0001f));
                    Assert.That(image.material, Is.EqualTo(Graphic.defaultGraphicMaterial));
                }
                finally
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        [Test]
        public void EachComponentOwnsAndReusesItsModifiedMaterial()
        {
            GameObject firstObject = CreateImageObject(new Vector2(100f, 50f));
            GameObject secondObject = CreateImageObject(new Vector2(100f, 50f));
            try
            {
                RoundedCorners first = firstObject.AddComponent<RoundedCorners>();
                RoundedCorners second = secondObject.AddComponent<RoundedCorners>();

                Material firstMaterial = first.GetModifiedMaterial(Graphic.defaultGraphicMaterial);
                Material repeatedMaterial = first.GetModifiedMaterial(Graphic.defaultGraphicMaterial);
                Material secondMaterial = second.GetModifiedMaterial(Graphic.defaultGraphicMaterial);

                Assert.That(repeatedMaterial, Is.SameAs(firstMaterial));
                Assert.That(secondMaterial, Is.Not.SameAs(firstMaterial));

                first.enabled = false;
                Assert.That(firstMaterial == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void NegativeValuesAreClampedWithoutChangingTheImage()
        {
            GameObject gameObject = CreateImageObject(new Vector2(100f, 50f));
            try
            {
                Image image = gameObject.GetComponent<Image>();
                image.raycastTarget = false;
                RoundedCorners corners = gameObject.AddComponent<RoundedCorners>();

                corners.CornerRadius = -1f;
                corners.BorderWidth = -2f;
                corners.EdgeSoftness = -3f;

                Assert.That(corners.CornerRadius, Is.Zero);
                Assert.That(corners.BorderWidth, Is.Zero);
                Assert.That(corners.EdgeSoftness, Is.Zero);
                Assert.That(image.raycastTarget, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ExamplesPrefabCoversRequiredImageAndScrollRectCases()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExamplesPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            AssertExample(prefab, "01 Rounded Solid Button", Image.Type.Simple);
            AssertExample(prefab, "02 Rounded Transparent PNG", Image.Type.Simple);
            AssertExample(prefab, "03 Rounded Sliced Image", Image.Type.Sliced);
            AssertExample(prefab, "04 Rounded Tiled Image", Image.Type.Tiled);
            AssertExample(prefab, "05 Rounded Filled Progress Bar", Image.Type.Filled);

            Assert.That(
                prefab.transform.Find("01 Rounded Solid Button").GetComponent<Button>(),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("02 Rounded Transparent PNG").GetComponent<Image>().sprite,
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("05 Rounded Filled Progress Bar").GetComponent<Image>().fillAmount,
                Is.EqualTo(0.65f).Within(0.0001f));

            Transform hidden = prefab.transform.Find("06 Dynamically Hidden Background (Inactive)");
            Assert.That(hidden, Is.Not.Null);
            Assert.That(hidden.gameObject.activeSelf, Is.False);
            Assert.That(hidden.GetComponent<RoundedCorners>(), Is.Not.Null);

            Transform viewport = prefab.transform.Find("07 ScrollRect Viewport");
            Assert.That(viewport, Is.Not.Null);
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(viewport.GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(
                viewport.Find("Content/Rounded Item Inside ScrollRect").GetComponent<RoundedCorners>(),
                Is.Not.Null);
        }

        [Test]
        public void InstantiatedExamplesCreateRoundedMaterialsWithoutChangingSourceImages()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExamplesPrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                RoundedCorners[] corners = instance.GetComponentsInChildren<RoundedCorners>(true);
                Assert.That(corners.Length, Is.EqualTo(8));

                foreach (RoundedCorners roundedCorners in corners)
                {
                    Image image = roundedCorners.GetComponent<Image>();
                    Image.Type originalType = image.type;
                    Sprite originalSprite = image.sprite;

                    if (roundedCorners.gameObject.activeInHierarchy)
                    {
                        Material material = roundedCorners.GetModifiedMaterial(Graphic.defaultGraphicMaterial);
                        Assert.That(material.shader.name, Is.EqualTo("AlchemyTower/UI/Rounded Corners"));
                    }

                    Assert.That(image.type, Is.EqualTo(originalType));
                    Assert.That(image.sprite, Is.SameAs(originalSprite));
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static GameObject CreateImageObject(Vector2 size)
        {
            GameObject gameObject = new GameObject(
                "Rounded Corners Test",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.GetComponent<RectTransform>().sizeDelta = size;
            return gameObject;
        }

        private static void AssertExample(GameObject prefab, string path, Image.Type expectedType)
        {
            Transform example = prefab.transform.Find(path);
            Assert.That(example, Is.Not.Null, path);
            Assert.That(example.GetComponent<Image>().type, Is.EqualTo(expectedType), path);
            Assert.That(example.GetComponent<RoundedCorners>(), Is.Not.Null, path);
        }
    }
}
