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
        [SerializeField] private OfflineProgressConfig offlineProgressConfig;
        [SerializeField] private OfflineRewardPopup offlineRewardPopupPrefab;
        [SerializeField] private FloorPrefabEntry[] floorPrefabsById = Array.Empty<FloorPrefabEntry>();

        [Header("Scene References")]
        [SerializeField] private RectTransform floorContainer;
        [SerializeField] private TopHUD topHUD;
        [SerializeField] private TowerScrollNavigator scrollNavigator;
        [SerializeField] private BottomNavigation bottomNavigation;
        [SerializeField] private RectTransform popupParent;

        private readonly List<FloorView> activeFloorViews = new List<FloorView>();
        private readonly List<GameObject> spawnedFloorObjects = new List<GameObject>();
        private TowerModel model;
        private SaveService saveService;
        private LockedFloorView lockedFloorView;
        private int previewFloorNumber;
        private OfflineProgressRules offlineProgressRules;
        private OfflineRewardPopup offlineRewardPopup;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private bool suspensionActive;
        private DateTime suspensionStartedUtc;

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
            offlineProgressRules = offlineProgressConfig.CreateRules();
            SaveLoadStatus loadStatus = saveService.Load();
            ReportLoadResult(loadStatus);
            OfflineProgressResult offlineResult = ProcessOfflineProgress(loadStatus, DateTime.UtcNow);
            bottomNavigation.ShopView.Bind(model);
            bottomNavigation.QuestBookView.Bind(model, OnQuestRewardClaimed);
            InitializeScrollNavigator();
            RebuildFloorViews();
            RefreshViews();
            if (offlineResult.ShouldShowPopup(offlineProgressRules))
                ShowOfflineRewardPopup(offlineResult);
        }

        private void Update()
        {
            if (suspensionActive)
                return;

            model.Tick(Time.deltaTime);
            saveService.Tick(Time.deltaTime);
            RefreshViews();
        }

        private void OnApplicationPause(bool isPaused)
        {
            applicationPaused = isPaused;
            if (isPaused)
                BeginSuspension();
            else
                TryResumeFromSuspension();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            applicationFocused = hasFocus;
            if (!hasFocus)
                BeginSuspension();
            else
                TryResumeFromSuspension();
        }

        private void OnApplicationQuit()
        {
            SaveCheckpoint();
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

            LayoutRebuilder.MarkLayoutForRebuild(floorContainer);
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
            topHUD.SetCoinBalance(model.Coins);
            bottomNavigation.ShopView.Refresh();
            bottomNavigation.QuestBookView.Refresh();
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
                && topHUD != null
                && bottomNavigation != null
                && bottomNavigation.ShopView != null
                && bottomNavigation.QuestBookView != null;
            valid = valid
                && offlineProgressConfig != null
                && offlineRewardPopupPrefab != null;
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

        private void BeginSuspension()
        {
            if (suspensionActive)
                return;

            suspensionActive = true;
            suspensionStartedUtc = DateTime.UtcNow;
            SaveCheckpoint(suspensionStartedUtc);
        }

        private void TryResumeFromSuspension()
        {
            if (!suspensionActive || applicationPaused || !applicationFocused)
                return;

            OfflineProgressResult result = ProcessOfflineProgress(
                SaveLoadStatus.Loaded, DateTime.UtcNow, suspensionStartedUtc);
            suspensionActive = false;
            RefreshViews();
            if (result.ShouldShowPopup(offlineProgressRules))
                ShowOfflineRewardPopup(result);
        }

        private OfflineProgressResult ProcessOfflineProgress(SaveLoadStatus loadStatus, DateTime nowUtc,
            DateTime? absenceStartOverrideUtc = null)
        {
            if (saveService == null || !saveService.SavingEnabled)
                return default;

            if (loadStatus != SaveLoadStatus.Loaded || !saveService.HasLastAccountedUtc)
            {
                SaveCheckpoint(nowUtc);
                return default;
            }

            TowerSnapshot before = model.CaptureSnapshot();
            DateTime absenceStartUtc = saveService.LastAccountedUtc;
            if (absenceStartOverrideUtc.HasValue && absenceStartOverrideUtc.Value > absenceStartUtc)
                absenceStartUtc = absenceStartOverrideUtc.Value;
            double absenceSeconds = (nowUtc - absenceStartUtc).TotalSeconds;
            OfflineProgressResult result = OfflineProgressCalculator.Apply(
                model, offlineProgressRules, absenceSeconds);
            if (saveService.TrySaveCheckpoint(nowUtc))
                return result;

            model.Restore(before);
            Debug.LogWarning($"Alchemy Tower offline progress was rolled back because saving failed: "
                + saveService.LastError, this);
            return default;
        }

        private void SaveCheckpoint()
        {
            SaveCheckpoint(DateTime.UtcNow);
        }

        private void SaveCheckpoint(DateTime timestampUtc)
        {
            if (saveService != null && saveService.SavingEnabled
                && !saveService.TrySaveCheckpoint(timestampUtc))
            {
                Debug.LogWarning($"Alchemy Tower save failed: {saveService.LastError}", this);
            }
        }

        private void ShowOfflineRewardPopup(OfflineProgressResult result)
        {
            if (offlineRewardPopup == null)
            {
                Transform parent = popupParent != null ? popupParent : transform;
                offlineRewardPopup = Instantiate(offlineRewardPopupPrefab, parent);
            }

            offlineRewardPopup.Show(result, topHUD.FormatCoinAmount);
        }

        private void OnQuestRewardClaimed()
        {
            RefreshViews();
            if (saveService != null && saveService.IsDirty && !saveService.TrySave())
                Debug.LogWarning($"Alchemy Tower quest reward save failed: {saveService.LastError}", this);
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
