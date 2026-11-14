using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    public sealed class TowerController : MonoBehaviour
    {
        [Serializable]
        private sealed class FloorPrefabEntry
        {
            [SerializeField] private string floorId;
            [SerializeField] private FloorView prefab;

            public string FloorId => floorId;
            public FloorView Prefab => prefab;
        }

        [Header("Configuration")]
        [SerializeField] private TowerDefinitionSO towerDefinition;
        [SerializeField] private FloorView floorViewPrefab;
        [SerializeField] private LockedFloorView lockedFloorViewPrefab;
        [SerializeField] private FloorPrefabEntry[] floorPrefabsById = Array.Empty<FloorPrefabEntry>();

        [Header("Scene References")]
        [SerializeField] private RectTransform floorContainer;
        [SerializeField] private Text coinsText;
        [SerializeField] private TowerScrollNavigator scrollNavigator;

        private readonly List<FloorView> activeFloorViews = new List<FloorView>();
        private readonly List<GameObject> spawnedFloorObjects = new List<GameObject>();
        private TowerModel model;
        private SaveService saveService;
        private LockedFloorView lockedFloorView;
        private int previewFloorNumber;

        public TowerModel Model => model;

        public void DisableSavingForSession()
        {
            saveService?.DisableSavingForSession();
        }

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            model = towerDefinition.CreateModel();
            saveService = new SaveService(model, new PlayerPrefsSaveStorage());
            ReportLoadResult(saveService.Load());
            InitializeScrollNavigator();
            RebuildFloorViews();
            RefreshViews();
        }

        private void Update()
        {
            model.Tick(Time.deltaTime);
            saveService.Tick(Time.deltaTime);
            RefreshViews();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
                FlushPendingSave();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                FlushPendingSave();
        }

        private void OnApplicationQuit()
        {
            FlushPendingSave();
        }

        private void OnDestroy()
        {
            saveService?.Dispose();
        }

        private void TryUnlockPreviewFloor()
        {
            int unlockedFloorNumber = previewFloorNumber;
            if (unlockedFloorNumber == 0 || !model.TryUnlockFloor(unlockedFloorNumber))
                return;

            RectTransform unlockedFloor = RebuildFloorViews(unlockedFloorNumber);
            RefreshViews();
            scrollNavigator.FocusOn(unlockedFloor);
        }

        private RectTransform RebuildFloorViews(int focusFloorNumber = 0)
        {
            for (int i = 0; i < spawnedFloorObjects.Count; i++)
            {
                GameObject oldView = spawnedFloorObjects[i];
                if (oldView == null)
                    continue;

                oldView.SetActive(false);
                Destroy(oldView);
            }

            spawnedFloorObjects.Clear();
            activeFloorViews.Clear();
            lockedFloorView = null;
            RectTransform focusedFloor = null;

            int highestUnlockedFloor = 0;
            for (int floorNumber = 1; floorNumber <= model.Floors.Count; floorNumber++)
            {
                if (!model.GetFloor(floorNumber).IsUnlocked)
                    break;
                highestUnlockedFloor = floorNumber;
            }

            previewFloorNumber = highestUnlockedFloor < model.Floors.Count
                ? highestUnlockedFloor + 1
                : 0;

            if (previewFloorNumber > 0)
            {
                FloorModel previewModel = model.GetFloor(previewFloorNumber);
                lockedFloorView = Instantiate(lockedFloorViewPrefab, floorContainer);
                spawnedFloorObjects.Add(lockedFloorView.gameObject);
                lockedFloorView.name = $"Floor {previewFloorNumber} — Locked";
                lockedFloorView.Bind(
                    previewModel.UnlockCostCoins,
                    TryUnlockPreviewFloor,
                    model.CanUnlockFloor(previewFloorNumber),
                    CanProcessFloorInput);
            }

            for (int floorNumber = highestUnlockedFloor; floorNumber >= 1; floorNumber--)
            {
                FloorDefinitionSO floorDefinition = towerDefinition.GetFloorDefinition(floorNumber);
                FloorView floorView = Instantiate(GetFloorViewPrefab(floorDefinition.FloorId), floorContainer);
                spawnedFloorObjects.Add(floorView.gameObject);
                floorView.name = $"Floor {floorNumber} — Active";
                floorView.Bind(
                    model.GetFloor(floorNumber),
                    floorDefinition.DisplayName,
                    CanProcessFloorInput);
                activeFloorViews.Add(floorView);
                if (floorNumber == focusFloorNumber)
                    focusedFloor = floorView.transform as RectTransform;
            }

            return focusedFloor;
        }

        private FloorView GetFloorViewPrefab(string floorId)
        {
            if (floorPrefabsById != null)
            {
                for (int i = 0; i < floorPrefabsById.Length; i++)
                {
                    FloorPrefabEntry entry = floorPrefabsById[i];
                    if (entry != null
                        && entry.Prefab != null
                        && string.Equals(entry.FloorId, floorId, StringComparison.Ordinal))
                    {
                        return entry.Prefab;
                    }
                }
            }

            return floorViewPrefab;
        }

        private void RefreshViews()
        {
            coinsText.text = $"Coins: {model.Coins}";
            for (int i = 0; i < activeFloorViews.Count; i++)
                activeFloorViews[i].Refresh();

            if (lockedFloorView != null)
                lockedFloorView.SetAffordable(model.CanUnlockFloor(previewFloorNumber));
        }

        private bool ValidateReferences()
        {
            bool valid = towerDefinition != null
                && floorViewPrefab != null
                && lockedFloorViewPrefab != null
                && floorContainer != null
                && coinsText != null;
            if (!valid)
                Debug.LogError("TowerController is missing required references.", this);
            return valid;
        }

        private void InitializeScrollNavigator()
        {
            ScrollRect scrollRect = floorContainer.GetComponentInParent<ScrollRect>();
            if (scrollNavigator == null && scrollRect != null)
                scrollNavigator = scrollRect.GetComponent<TowerScrollNavigator>();
            if (scrollNavigator == null && scrollRect != null)
                scrollNavigator = scrollRect.gameObject.AddComponent<TowerScrollNavigator>();

            if (scrollNavigator == null || scrollRect == null)
                throw new InvalidOperationException("TowerController requires a parent ScrollRect.");

            scrollNavigator.Initialize(scrollRect);
        }

        private bool CanProcessFloorInput()
        {
            return scrollNavigator == null || scrollNavigator.CanProcessClick;
        }

        private void FlushPendingSave()
        {
            if (saveService != null && saveService.IsDirty && !saveService.TrySave())
                Debug.LogWarning($"Alchemy Tower save failed: {saveService.LastError}", this);
        }

        private void ReportLoadResult(SaveLoadStatus status)
        {
            if (status == SaveLoadStatus.NoSave || status == SaveLoadStatus.Loaded)
                return;

            Debug.LogWarning(
                $"Alchemy Tower save was not loaded ({status}). A fresh in-memory game will be used "
                + $"without overwriting the existing save. {saveService.LastError}",
                this);
        }
    }
}
