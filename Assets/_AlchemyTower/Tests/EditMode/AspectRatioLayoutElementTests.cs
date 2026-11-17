using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower.Tests
{
    public sealed class AspectRatioLayoutElementTests
    {
        [Test]
        public void PreferredHeightUsesActualWidthAndAuthoredAspectRatio()
        {
            GameObject gameObject = new GameObject("Aspect Layout Test", typeof(RectTransform));
            try
            {
                RectTransform rect = gameObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(800f, 123f);

                AspectRatioLayoutElement element = gameObject.AddComponent<AspectRatioLayoutElement>();
                element.AspectRatio = 2f;

                Assert.That(LayoutUtility.GetMinHeight(rect), Is.EqualTo(400f).Within(0.001f));
                Assert.That(LayoutUtility.GetPreferredHeight(rect), Is.EqualTo(400f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PreferredHeightUpdatesAfterWidthAndRatioChange()
        {
            GameObject gameObject = new GameObject("Responsive Aspect Layout Test", typeof(RectTransform));
            try
            {
                RectTransform rect = gameObject.GetComponent<RectTransform>();
                AspectRatioLayoutElement element = gameObject.AddComponent<AspectRatioLayoutElement>();

                rect.sizeDelta = new Vector2(600f, 10f);
                element.AspectRatio = 1.5f;
                Assert.That(LayoutUtility.GetPreferredHeight(rect), Is.EqualTo(400f).Within(0.001f));

                rect.sizeDelta = new Vector2(300f, 10f);
                Assert.That(LayoutUtility.GetPreferredHeight(rect), Is.EqualTo(200f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InvalidRatioIsClampedToPositiveValue()
        {
            GameObject gameObject = new GameObject("Aspect Clamp Test", typeof(RectTransform));
            try
            {
                AspectRatioLayoutElement element = gameObject.AddComponent<AspectRatioLayoutElement>();
                element.AspectRatio = 0f;

                Assert.That(element.AspectRatio, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
