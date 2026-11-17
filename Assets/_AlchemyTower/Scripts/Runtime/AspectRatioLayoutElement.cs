using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlchemyTower
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Layout/Aspect Ratio Layout Element", 141)]
    public sealed class AspectRatioLayoutElement : UIBehaviour, ILayoutElement
    {
        private const float MinimumAspectRatio = 0.0001f;

        [SerializeField, Min(MinimumAspectRatio)]
        [Tooltip("Width divided by height. A value of 2 means the element is twice as wide as it is tall.")]
        private float aspectRatio = 2f;

        private RectTransform cachedRectTransform;
        private float observedWidth = float.NaN;

        public float AspectRatio
        {
            get => aspectRatio;
            set
            {
                float clamped = Mathf.Max(MinimumAspectRatio, value);
                if (Mathf.Approximately(aspectRatio, clamped))
                    return;

                aspectRatio = clamped;
                SetLayoutDirty();
            }
        }

        private RectTransform RectTransform
        {
            get
            {
                if (cachedRectTransform == null)
                    cachedRectTransform = transform as RectTransform;
                return cachedRectTransform;
            }
        }

        public float minWidth => -1f;
        public float preferredWidth => -1f;
        public float flexibleWidth => -1f;
        public float minHeight => CalculatedHeight;
        public float preferredHeight => CalculatedHeight;
        public float flexibleHeight => 0f;
        public int layoutPriority => 1;

        private float CalculatedHeight
        {
            get
            {
                RectTransform rectTransform = RectTransform;
                float width = rectTransform != null ? Mathf.Max(0f, rectTransform.rect.width) : 0f;
                return width / Mathf.Max(MinimumAspectRatio, aspectRatio);
            }
        }

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            observedWidth = CurrentWidth;
            SetLayoutDirty();
        }

        protected override void OnDisable()
        {
            SetLayoutDirty();
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            float width = CurrentWidth;
            if (Mathf.Approximately(observedWidth, width))
                return;

            observedWidth = width;
            SetLayoutDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            aspectRatio = Mathf.Max(MinimumAspectRatio, aspectRatio);
            base.OnValidate();
            SetLayoutDirty();
        }
#endif

        private float CurrentWidth
        {
            get
            {
                RectTransform rectTransform = RectTransform;
                return rectTransform != null ? rectTransform.rect.width : 0f;
            }
        }

        private void SetLayoutDirty()
        {
            if (!IsActive())
                return;

            RectTransform rectTransform = RectTransform;
            if (rectTransform != null)
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }
    }
}
