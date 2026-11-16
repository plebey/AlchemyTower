using System;
using System.Collections.Generic;

namespace AlchemyTower
{
    [Serializable]
    public sealed class GameSaveData
    {
        public int schemaVersion = SaveService.CurrentSchemaVersion;
        // A decimal string keeps v1 inspectable while leaving room for a future big-number implementation.
        public string coinBalance = "0";
        public List<FloorSaveData> floors = new List<FloorSaveData>();
        public ShopSaveData shop = new ShopSaveData();
        public List<string> claimedQuestIds = new List<string>();
    }

    [Serializable]
    public sealed class FloorSaveData
    {
        public string floorId;
        public bool isUnlocked;
        public int level = 1;
        public bool automationPurchased;
        public float brewingProgressSeconds;
        public FloorPhase phase;
    }

    [Serializable]
    public sealed class ShopSaveData
    {
        public int alchemicalGlovesLevel;
        public int goldenSealLevel;
    }
}
