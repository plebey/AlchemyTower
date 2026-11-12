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

        private readonly List<FloorView> activeFloorViews = new List<FloorView>();
        private TowerModel model;
        private LockedFloorView lockedFloorView;
        private int previewFloorNumber;

        public TowerModel Model => model;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            model = towerDefinition.CreateModel();
            RebuildFloorViews();
            RefreshViews();
        }

        private void Update()
        {
            model.Tick(Time.deltaTime);
            RefreshViews();
        }

        private void TryUnlockPreviewFloor()
        {
            if (previewFloorNumber == 0 || !model.TryUnlockFloor(previewFloorNumber))
                return;

            RebuildFloorViews();
            RefreshViews();
        }

        private void RebuildFloorViews()
        {
            for (int i = floorContainer.childCount - 1; i >= 0; i--)
            {
                GameObject oldView = floorContainer.GetChild(i).gameObject;
                oldView.SetActive(false);
                Destroy(oldView);
            }

            activeFloorViews.Clear();
            lockedFloorView = null;

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
                    model.CanUnlockFloor(previewFloorNumber));
            }

            for (int floorNumber = highestUnlockedFloor; floorNumber >= 1; floorNumber--)
            {
                FloorView floorView = Instantiate(floorViewPrefab, floorContainer);
                floorView.name = $"Floor {floorNumber} — Active";
                floorView.Bind(
                    model.GetFloor(floorNumber),
                    towerDefinition.GetFloorDefinition(floorNumber).DisplayName);
                activeFloorViews.Add(floorView);
            }
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
    }
}
