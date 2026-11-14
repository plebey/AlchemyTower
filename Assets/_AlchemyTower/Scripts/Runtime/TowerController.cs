using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    public sealed class TowerController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TowerDefinitionSO towerDefinition;
        [SerializeField] private FloorView floorViewPrefab;
        [SerializeField] private LockedFloorView lockedFloorViewPrefab;

        [Header("Scene References")]
        [SerializeField] private RectTransform floorContainer;
        [SerializeField] private Text coinsText;
        [SerializeField] private TowerScrollNavigator scrollNavigator;

        private readonly List<FloorView> activeFloorViews = new List<FloorView>();
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
            for (int i = floorContainer.childCount - 1; i >= 0; i--)
            {
                GameObject oldView = floorContainer.GetChild(i).gameObject;
                oldView.SetActive(false);
                Destroy(oldView);
            }

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
                lockedFloorView.name = $"Floor {previewFloorNumber} — Locked";
                lockedFloorView.Bind(
                    previewModel.UnlockCostCoins,
                    TryUnlockPreviewFloor,
                    model.CanUnlockFloor(previewFloorNumber),
                    CanProcessFloorInput);
            }

            for (int floorNumber = highestUnlockedFloor; floorNumber >= 1; floorNumber--)
            {
                FloorView floorView = Instantiate(floorViewPrefab, floorContainer);
                floorView.name = $"Floor {floorNumber} — Active";
                floorView.Bind(
                    model.GetFloor(floorNumber),
                    towerDefinition.GetFloorDefinition(floorNumber).DisplayName,
                    CanProcessFloorInput);
                activeFloorViews.Add(floorView);
                if (floorNumber == focusFloorNumber)
                    focusedFloor = floorView.transform as RectTransform;
            }

            return focusedFloor;
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
