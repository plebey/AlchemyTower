using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlchemyTower
{
    [RequireComponent(typeof(ScrollRect))]
    public sealed class TowerScrollNavigator : MonoBehaviour,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        [SerializeField, Min(0.01f)] private float focusDurationSeconds = 0.5f;

        private ScrollRect scrollRect;
        private Coroutine focusCoroutine;
        private bool isDragging;
        private int suppressClicksThroughFrame = -1;

        public bool CanProcessClick => !isDragging && Time.frameCount > suppressClicksThroughFrame;

        private void Awake()
        {
            Initialize(GetComponent<ScrollRect>());
        }

        public void Initialize(ScrollRect targetScrollRect)
        {
            scrollRect = targetScrollRect;
            if (scrollRect == null)
                return;

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        public void FocusOn(RectTransform target)
        {
            if (target == null || scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
                return;

            CancelAutomaticScroll();
            focusCoroutine = StartCoroutine(FocusAfterLayout(target));
        }

        public void CancelAutomaticScroll()
        {
            if (focusCoroutine == null)
                return;

            StopCoroutine(focusCoroutine);
            focusCoroutine = null;
            scrollRect.StopMovement();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            CancelAutomaticScroll();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            CancelAutomaticScroll();
            isDragging = true;
            SuppressClicksPastCurrentFrame();
        }

        public void OnDrag(PointerEventData eventData)
        {
            SuppressClicksPastCurrentFrame();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            SuppressClicksPastCurrentFrame();
        }

        public void OnScroll(PointerEventData eventData)
        {
            CancelAutomaticScroll();
            SuppressClicksPastCurrentFrame();
        }

        private IEnumerator FocusAfterLayout(RectTransform target)
        {
            // Rebuilding replaces the locked preview and destroys old views at frame end.
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            Canvas.ForceUpdateCanvases();

            if (target == null)
            {
                focusCoroutine = null;
                yield break;
            }

            float contentHeight = scrollRect.content.rect.height;
            float viewportHeight = scrollRect.viewport.rect.height;
            Vector3 targetWorldCenter = target.TransformPoint(target.rect.center);
            float targetCenterInContent = scrollRect.content.InverseTransformPoint(targetWorldCenter).y;
            float targetCenterFromBottom = targetCenterInContent - scrollRect.content.rect.yMin;
            float destination = CalculateTargetNormalizedPosition(
                contentHeight,
                viewportHeight,
                targetCenterFromBottom);

            float start = scrollRect.verticalNormalizedPosition;
            float duration = Mathf.Max(0.01f, focusDurationSeconds);
            float elapsed = 0f;
            scrollRect.StopMovement();

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                scrollRect.velocity = Vector2.zero;
                scrollRect.verticalNormalizedPosition = Mathf.LerpUnclamped(start, destination, eased);
                yield return null;
            }

            scrollRect.verticalNormalizedPosition = destination;
            scrollRect.StopMovement();
            focusCoroutine = null;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled || scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
                return;

            if (scrollRect.content.rect.height <= scrollRect.viewport.rect.height)
            {
                scrollRect.StopMovement();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void OnDisable()
        {
            CancelAutomaticScroll();
            isDragging = false;
        }

        private void SuppressClicksPastCurrentFrame()
        {
            suppressClicksThroughFrame = Mathf.Max(suppressClicksThroughFrame, Time.frameCount + 1);
        }

        public static float CalculateTargetNormalizedPosition(
            float contentHeight,
            float viewportHeight,
            float targetCenterFromBottom)
        {
            float scrollableHeight = contentHeight - viewportHeight;
            if (scrollableHeight <= 0f)
                return 0f;

            float desiredViewportBottom = targetCenterFromBottom - viewportHeight * 0.5f;
            return Mathf.Clamp01(desiredViewportBottom / scrollableHeight);
        }
    }
}
