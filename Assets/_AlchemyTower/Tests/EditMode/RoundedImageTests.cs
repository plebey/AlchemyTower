using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower.Tests
{
    public sealed class RoundedImageTests
    {
        [Test]
        public void MeshClampsRadiusAndKeepsBorderWidthInLocalUnits()
        {
            GameObject gameObject = new GameObject("Rounded Image Test", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                RectTransform rect = gameObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(100f, 40f);

                RoundedImage image = gameObject.AddComponent<RoundedImage>();
                image.CornerRadius = 100f;
                image.BorderWidth = 10f;

                using VertexHelper vertices = Populate(image);
                Assert.That(vertices.currentVertCount, Is.EqualTo(109));

                UIVertex outer = default;
                UIVertex inner = default;
                vertices.PopulateUIVertex(ref outer, 0);
                vertices.PopulateUIVertex(ref inner, 1);

                Assert.That(outer.position.x, Is.EqualTo(30f).Within(0.001f));
                Assert.That(outer.position.y, Is.EqualTo(-20f).Within(0.001f));
                Assert.That(inner.position.x, Is.EqualTo(30f).Within(0.001f));
                Assert.That(inner.position.y, Is.EqualTo(-10f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void MeshUsesImageColorForFillAndIndependentBorderColor()
        {
            GameObject gameObject = new GameObject("Rounded Image Color Test", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 60f);
                RoundedImage image = gameObject.AddComponent<RoundedImage>();
                image.color = new Color(1f, 0f, 0f, 0.5f);
                image.BorderColor = new Color(0f, 0f, 1f, 0.25f);
                image.CornerRadius = 12f;
                image.BorderWidth = 4f;

                using VertexHelper vertices = Populate(image);
                UIVertex border = default;
                UIVertex fillCenter = default;
                vertices.PopulateUIVertex(ref border, 0);
                vertices.PopulateUIVertex(ref fillCenter, 72);

                Assert.That((Color32)border.color, Is.EqualTo((Color32)image.BorderColor));
                Assert.That((Color32)fillCenter.color, Is.EqualTo((Color32)image.color));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NegativeAuthoredValuesAreClampedToZero()
        {
            GameObject gameObject = new GameObject("Rounded Image Clamp Test", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                RoundedImage image = gameObject.AddComponent<RoundedImage>();
                image.CornerRadius = -5f;
                image.BorderWidth = -3f;

                Assert.That(image.CornerRadius, Is.Zero);
                Assert.That(image.BorderWidth, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static VertexHelper Populate(RoundedImage image)
        {
            VertexHelper vertices = new VertexHelper();
            MethodInfo populate = typeof(RoundedImage).GetMethod(
                "OnPopulateMesh",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(VertexHelper) },
                modifiers: null);
            Assert.That(populate, Is.Not.Null);
            populate.Invoke(image, new object[] { vertices });
            return vertices;
        }
    }
}
