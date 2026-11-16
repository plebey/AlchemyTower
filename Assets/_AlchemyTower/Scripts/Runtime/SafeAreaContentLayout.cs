using UnityEngine;

namespace AlchemyTower
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaContentLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform topHUD;
        [SerializeField] private RectTransform towerViewport;
        [SerializeField] private BottomNavigation bottomNavigation;
        [SerializeField, Min(0f)] private float topGap = 12f;
        [SerializeField, Min(0f)] private float bottomGap = 12f;

        private Vector2 lastSafeAreaSize;
        private float lastTopHeight = -1f;
        private float lastBottomHeight = -1f;

        private void OnEnable()
        {
            ConfigureScreens();
            ApplyLayout(force: true);
        }

        private void LateUpdate()
        {
            ApplyLayout(force: false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            topGap = Mathf.Max(0f, topGap);
            bottomGap = Mathf.Max(0f, bottomGap);
            ApplyLayout(force: true);
        }
#endif

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout(force: true);
        }

        private void ApplyLayout(bool force)
        {
            if (topHUD == null || towerViewport == null || bottomNavigation == null
                || bottomNavigation.NavigationBar == null)
                return;

            RectTransform safeArea = transform as RectTransform;
            float topHeight = GetRenderedHeight(topHUD);
            float bottomHeight = GetRenderedHeight(bottomNavigation.NavigationBar);
            Vector2 safeAreaSize = safeArea != null ? safeArea.rect.size : Vector2.zero;
            if (!force
                && safeAreaSize == lastSafeAreaSize
                && Mathf.Approximately(topHeight, lastTopHeight)
                && Mathf.Approximately(bottomHeight, lastBottomHeight))
                return;

            CalculateInsets(topHeight, bottomHeight, topGap, bottomGap, out float topInset, out float bottomInset);

            Vector2 min = towerViewport.offsetMin;
            Vector2 max = towerViewport.offsetMax;
            towerViewport.anchorMin = new Vector2(towerViewport.anchorMin.x, 0f);
            towerViewport.anchorMax = new Vector2(towerViewport.anchorMax.x, 1f);
            towerViewport.offsetMin = new Vector2(min.x, bottomInset);
            towerViewport.offsetMax = new Vector2(max.x, -topInset);
            bottomNavigation.SetPageAreaInsets(topInset, bottomInset);

            lastSafeAreaSize = safeAreaSize;
            lastTopHeight = topHeight;
            lastBottomHeight = bottomHeight;
        }

        private void ConfigureScreens()
        {
            if (bottomNavigation != null && towerViewport != null)
                bottomNavigation.SetTowerScreen(towerViewport.gameObject);
        }

        private static float GetRenderedHeight(RectTransform target)
        {
            float height = target.rect.height;
            if (height > 0f)
                return height;
            return Mathf.Max(0f, target.sizeDelta.y);
        }

        public static void CalculateInsets(
            float topHeight,
            float bottomHeight,
            float topGap,
            float bottomGap,
            out float topInset,
            out float bottomInset)
        {
            topInset = Mathf.Max(0f, topHeight) + Mathf.Max(0f, topGap);
            bottomInset = Mathf.Max(0f, bottomHeight) + Mathf.Max(0f, bottomGap);
        }
    }
}
