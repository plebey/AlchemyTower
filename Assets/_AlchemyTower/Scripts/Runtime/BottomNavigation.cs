using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AlchemyTower
{
    [DisallowMultipleComponent]
    public sealed class BottomNavigation : MonoBehaviour
    {
        [Serializable]
        public sealed class Item
        {
            [Tooltip("Editable identifier for future routing. It is not coupled to gameplay logic.")]
            [SerializeField] private string id;
            [SerializeField] private string label;
            // Kept hidden only so older Prefab data remains serialization-compatible.
            // The Icon Image's Source Image is the authored source of truth.
            [SerializeField, HideInInspector] private Sprite icon;
            [SerializeField] private bool available = true;
            [Tooltip("Enable only for the item that displays the existing Tower Viewport.")]
            [SerializeField] private bool showsTowerScreen;
            [SerializeField] private Button button;
            [SerializeField] private Image iconImage;
            [SerializeField] private Text labelText;
            [SerializeField] private Image selectionBackground;
            [Tooltip("Leave empty for the tower tab. Future tabs reference their own page here.")]
            [SerializeField] private GameObject page;

            public string Id => id;
            public string Label => label;
            public Sprite Icon => icon;
            public bool Available => available;
            public bool ShowsTowerScreen => showsTowerScreen;
            public Button Button => button;
            public Image IconImage => iconImage;
            public Text LabelText => labelText;
            public Image SelectionBackground => selectionBackground;
            public GameObject Page => page;
        }

        [Serializable]
        public struct VisualStyle
        {
            public Color iconColor;
            public Color labelColor;
            public Color selectionColor;
        }

        [Header("Layout References")]
        [SerializeField] private RectTransform navigationBar;
        [SerializeField] private RectTransform pageContainer;
        [Tooltip("Assigned by the safe-area shell. This is the existing Tower Viewport, not a recreated screen.")]
        [SerializeField] private GameObject towerScreen;
        [SerializeField] private ShopView shopView;

        [Header("Editable Items (hierarchy order follows this list)")]
        [SerializeField] private Item[] items = Array.Empty<Item>();
        [SerializeField, Min(0)] private int initiallySelectedIndex = 1;

        [Header("Visual States")]
        [SerializeField] private VisualStyle normalStyle = new VisualStyle
        {
            iconColor = new Color(0.66f, 0.61f, 0.75f, 1f),
            labelColor = new Color(0.75f, 0.70f, 0.82f, 1f),
            selectionColor = new Color(0.30f, 0.18f, 0.43f, 0f)
        };
        [SerializeField] private VisualStyle selectedStyle = new VisualStyle
        {
            iconColor = new Color(0.96f, 0.76f, 0.28f, 1f),
            labelColor = new Color(1f, 0.91f, 0.63f, 1f),
            selectionColor = new Color(0.39f, 0.20f, 0.54f, 0.9f)
        };
        [SerializeField] private VisualStyle unavailableStyle = new VisualStyle
        {
            iconColor = new Color(0.34f, 0.31f, 0.38f, 1f),
            labelColor = new Color(0.40f, 0.37f, 0.44f, 1f),
            selectionColor = new Color(0.16f, 0.13f, 0.20f, 0f)
        };

        private UnityAction[] clickHandlers = Array.Empty<UnityAction>();
        private int selectedIndex = -1;

        public RectTransform NavigationBar => navigationBar;
        public ShopView ShopView => shopView;
        public int SelectedIndex => selectedIndex;

        private void Awake()
        {
            ApplyAuthoredContent();
            RegisterButtons();
            Select(FindInitialSelection());
        }

        private void OnDestroy()
        {
            UnregisterButtons();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            initiallySelectedIndex = Mathf.Max(0, initiallySelectedIndex);
            ApplyAuthoredContent();
            if (!Application.isPlaying)
                PreviewSelection(FindInitialSelection());
        }
#endif

        public void Select(int index)
        {
            if (!IsSelectable(index))
                return;

            selectedIndex = index;
            ApplySelection(index, updatePages: true);
        }

        public void SetTowerScreen(GameObject screen)
        {
            towerScreen = screen;
            ApplyTowerScreenVisibility(selectedIndex < 0 ? FindInitialSelection() : selectedIndex);
        }

        public void SetPageAreaInsets(float topInset, float bottomInset)
        {
            if (pageContainer == null)
                return;

            pageContainer.anchorMin = Vector2.zero;
            pageContainer.anchorMax = Vector2.one;
            pageContainer.offsetMin = new Vector2(0f, Mathf.Max(0f, bottomInset));
            pageContainer.offsetMax = new Vector2(0f, -Mathf.Max(0f, topInset));
        }

        private void RegisterButtons()
        {
            UnregisterButtons();
            clickHandlers = new UnityAction[items?.Length ?? 0];
            for (int i = 0; i < clickHandlers.Length; i++)
            {
                Item item = items[i];
                if (item?.Button == null)
                    continue;

                int capturedIndex = i;
                UnityAction handler = () => Select(capturedIndex);
                clickHandlers[i] = handler;
                item.Button.onClick.AddListener(handler);
            }
        }

        private void UnregisterButtons()
        {
            if (items == null || clickHandlers == null)
                return;

            int count = Mathf.Min(items.Length, clickHandlers.Length);
            for (int i = 0; i < count; i++)
            {
                if (items[i]?.Button != null && clickHandlers[i] != null)
                    items[i].Button.onClick.RemoveListener(clickHandlers[i]);
            }

            clickHandlers = Array.Empty<UnityAction>();
        }

        private void ApplyAuthoredContent()
        {
            if (items == null)
                return;

            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                if (item == null)
                    continue;

                if (item.LabelText != null)
                    item.LabelText.text = item.Label ?? string.Empty;
                if (item.Button != null)
                    item.Button.interactable = item.Available;
                if (item.Button != null && item.Button.transform.parent != null)
                    item.Button.transform.SetSiblingIndex(i);
            }
        }

        private int FindInitialSelection()
        {
            if (IsSelectable(initiallySelectedIndex))
                return initiallySelectedIndex;

            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (IsSelectable(i))
                        return i;
                }
            }

            return -1;
        }

        private bool IsSelectable(int index)
        {
            return items != null
                && index >= 0
                && index < items.Length
                && items[index] != null
                && items[index].Available;
        }

        private void PreviewSelection(int index)
        {
            selectedIndex = index;
            ApplySelection(index, updatePages: true);
        }

        private void ApplySelection(int index, bool updatePages)
        {
            if (items == null)
                return;

            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                if (item == null)
                    continue;

                VisualStyle style = !item.Available
                    ? unavailableStyle
                    : i == index ? selectedStyle : normalStyle;

                if (item.IconImage != null)
                    item.IconImage.color = style.iconColor;
                if (item.LabelText != null)
                    item.LabelText.color = style.labelColor;
                if (item.SelectionBackground != null)
                    item.SelectionBackground.color = style.selectionColor;
                if (updatePages && item.Page != null)
                    item.Page.SetActive(i == index);
            }

            if (updatePages)
                ApplyTowerScreenVisibility(index);
        }

        private void ApplyTowerScreenVisibility(int selectedItemIndex)
        {
            if (towerScreen != null)
            {
                bool showTower = IsSelectable(selectedItemIndex)
                    && items[selectedItemIndex].ShowsTowerScreen;
                towerScreen.SetActive(showTower);
            }
        }
    }
}
