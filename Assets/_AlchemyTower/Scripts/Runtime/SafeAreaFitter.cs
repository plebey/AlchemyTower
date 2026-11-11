using UnityEngine;

namespace AlchemyTower
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            ApplySafeArea(force: true);
        }

        private void Update()
        {
            ApplySafeArea(force: false);
        }

        private void ApplySafeArea(bool force)
        {
            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
                return;

            CalculateAnchors(safeArea, screenSize.x, screenSize.y, out Vector2 anchorMin, out Vector2 anchorMax);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
        }

        public static void CalculateAnchors(
            Rect safeArea,
            float screenWidth,
            float screenHeight,
            out Vector2 anchorMin,
            out Vector2 anchorMax)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }

            anchorMin = new Vector2(
                Mathf.Clamp01(safeArea.xMin / screenWidth),
                Mathf.Clamp01(safeArea.yMin / screenHeight));
            anchorMax = new Vector2(
                Mathf.Clamp01(safeArea.xMax / screenWidth),
                Mathf.Clamp01(safeArea.yMax / screenHeight));
        }
    }
}
