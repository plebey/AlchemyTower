using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AlchemyTower
{
    public interface ISaveStorage
    {
        bool Exists();
        string Load();
        void Save(string data);
        void Delete();
    }

    public sealed class PlayerPrefsSaveStorage : ISaveStorage
    {
        public const string SaveKey = "AlchemyTower.Save.v1";

        public bool Exists() => PlayerPrefs.HasKey(SaveKey);
        public string Load() => PlayerPrefs.GetString(SaveKey);

        public void Save(string data)
        {
            PlayerPrefs.SetString(SaveKey, data);
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }

    public interface ISaveSerializer
    {
        string Serialize(GameSaveData data);
        GameSaveData Deserialize(string json);
    }

    public sealed class UnityJsonSaveSerializer : ISaveSerializer
    {
        public string Serialize(GameSaveData data) => JsonUtility.ToJson(data, true);
        public GameSaveData Deserialize(string json) => JsonUtility.FromJson<GameSaveData>(json);
    }

    public enum SaveLoadStatus
    {
        NoSave,
        Loaded,
        CorruptedOrInvalid,
        UnsupportedFutureVersion,
        StorageFailure
    }

    public sealed class SaveService : IDisposable
    {
        public const int CurrentSchemaVersion = 1;
        public const float PeriodicSaveIntervalSeconds = 15f;
        public const float ImportantSaveDelaySeconds = 0.5f;
        private const int MaxSupportedLevel = 10000;

        private readonly TowerModel model;
        private readonly ISaveStorage storage;
        private readonly ISaveSerializer serializer;
        private float dirtySeconds;
        private float importantDirtySeconds;
        private bool importantDirty;
        private bool savingEnabled = true;

        public SaveService(TowerModel model, ISaveStorage storage, ISaveSerializer serializer = null)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.serializer = serializer ?? new UnityJsonSaveSerializer();
            model.StateChanged += OnStateChanged;
        }

        public bool IsDirty { get; private set; }
        public bool SavingEnabled => savingEnabled;
        public string LastError { get; private set; }

        public SaveLoadStatus Load()
        {
            try
            {
                if (!storage.Exists())
                    return SaveLoadStatus.NoSave;

                GameSaveData data;
                try
                {
                    data = serializer.Deserialize(storage.Load());
                }
                catch (Exception exception)
                {
                    return BlockSaving(SaveLoadStatus.CorruptedOrInvalid, exception.Message);
                }

                if (data == null)
                    return BlockSaving(SaveLoadStatus.CorruptedOrInvalid, "Save JSON did not contain an object.");
                if (data.schemaVersion > CurrentSchemaVersion)
                    return BlockSaving(SaveLoadStatus.UnsupportedFutureVersion,
                        $"Save schema {data.schemaVersion} is newer than supported schema {CurrentSchemaVersion}.");
                if (!TryMigrateToCurrentVersion(data, out string migrationError))
                    return BlockSaving(SaveLoadStatus.CorruptedOrInvalid, migrationError);
                if (!TryCreateValidatedSnapshot(data, out TowerSnapshot snapshot, out string error))
                    return BlockSaving(SaveLoadStatus.CorruptedOrInvalid, error);

                model.Restore(snapshot);
                LastError = null;
                return SaveLoadStatus.Loaded;
            }
            catch (Exception exception)
            {
                return BlockSaving(SaveLoadStatus.StorageFailure, exception.Message);
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsDirty || !savingEnabled || !IsFinite(deltaSeconds) || deltaSeconds <= 0f)
                return;

            dirtySeconds += deltaSeconds;
            if (importantDirty)
                importantDirtySeconds += deltaSeconds;
            if (dirtySeconds >= PeriodicSaveIntervalSeconds
                || importantDirty && importantDirtySeconds >= ImportantSaveDelaySeconds)
                TrySave();
        }

        public bool TrySave()
        {
            if (!IsDirty || !savingEnabled)
                return false;

            try
            {
                storage.Save(serializer.Serialize(CreateSaveData(model.CaptureSnapshot())));
                IsDirty = false;
                importantDirty = false;
                dirtySeconds = 0f;
                importantDirtySeconds = 0f;
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                // Keep the dirty flags, but back off instead of retrying every frame.
                dirtySeconds = 0f;
                importantDirtySeconds = 0f;
                return false;
            }
        }

        public void DisableSavingForSession()
        {
            savingEnabled = false;
            IsDirty = false;
        }

        public void Dispose() => model.StateChanged -= OnStateChanged;

        private void OnStateChanged(bool important)
        {
            if (!savingEnabled)
                return;
            if (!IsDirty)
            {
                IsDirty = true;
                dirtySeconds = 0f;
            }
            if (important)
            {
                importantDirty = true;
                importantDirtySeconds = 0f;
            }
        }

        private SaveLoadStatus BlockSaving(SaveLoadStatus status, string error)
        {
            savingEnabled = false;
            LastError = error;
            return status;
        }

        private static bool TryMigrateToCurrentVersion(GameSaveData data, out string error)
        {
            // Add explicit older-version cases here when a schema after v1 is introduced.
            if (data.schemaVersion == CurrentSchemaVersion)
            {
                error = null;
                return true;
            }

            error = $"Save schema {data.schemaVersion} is invalid. No migration is available.";
            return false;
        }

        private static GameSaveData CreateSaveData(TowerSnapshot snapshot)
        {
            GameSaveData data = new GameSaveData
            {
                schemaVersion = CurrentSchemaVersion,
                coinBalance = snapshot.Coins.ToString(CultureInfo.InvariantCulture)
            };
            for (int i = 0; i < snapshot.Floors.Count; i++)
            {
                FloorSnapshot floor = snapshot.Floors[i];
                data.floors.Add(new FloorSaveData
                {
                    floorId = floor.FloorId,
                    isUnlocked = floor.IsUnlocked,
                    level = floor.Level,
                    automationPurchased = floor.IsAutomated,
                    brewingProgressSeconds = floor.ProgressSeconds,
                    phase = floor.Phase
                });
            }
            return data;
        }

        private bool TryCreateValidatedSnapshot(GameSaveData data, out TowerSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (!int.TryParse(data.coinBalance, NumberStyles.None, CultureInfo.InvariantCulture, out int coins)
                || coins < 0)
            {
                error = "Coin balance is not a supported non-negative integer.";
                return false;
            }

            Dictionary<string, FloorSaveData> savedById = new Dictionary<string, FloorSaveData>(StringComparer.Ordinal);
            if (data.floors == null)
            {
                error = "Floor list is missing.";
                return false;
            }
            for (int i = 0; i < data.floors.Count; i++)
            {
                FloorSaveData floor = data.floors[i];
                if (floor == null || string.IsNullOrWhiteSpace(floor.floorId)
                    || !savedById.TryAdd(floor.floorId, floor))
                {
                    error = "Floor identifiers must be non-empty and unique.";
                    return false;
                }
            }

            List<FloorSnapshot> floors = new List<FloorSnapshot>();
            bool precedingUnlocked = true;
            for (int i = 0; i < model.Floors.Count; i++)
            {
                FloorModel configured = model.Floors[i];
                if (!savedById.TryGetValue(configured.FloorId, out FloorSaveData saved))
                {
                    precedingUnlocked = configured.IsUnlocked;
                    continue; // A newly configured floor keeps its model default.
                }

                if (saved.level < 1 || saved.level > MaxSupportedLevel
                    || !IsFinite(saved.brewingProgressSeconds)
                    || saved.brewingProgressSeconds < 0f
                    || saved.brewingProgressSeconds > configured.BrewDurationSeconds
                    || !Enum.IsDefined(typeof(FloorPhase), saved.phase)
                    || saved.isUnlocked && !precedingUnlocked
                    || saved.automationPurchased && (!saved.isUnlocked || saved.level < configured.AutomationRequiredLevel)
                    || !saved.isUnlocked && (saved.level != 1 || saved.automationPurchased
                        || saved.brewingProgressSeconds != 0f || saved.phase != FloorPhase.Brewing)
                    || saved.phase == FloorPhase.ReadyToCollect
                        && (saved.automationPurchased || saved.brewingProgressSeconds != configured.BrewDurationSeconds)
                    || saved.phase == FloorPhase.Brewing
                        && saved.brewingProgressSeconds >= configured.BrewDurationSeconds)
                {
                    error = $"Floor '{configured.FloorId}' contains an invalid state combination.";
                    return false;
                }

                floors.Add(new FloorSnapshot(configured.FloorId, saved.isUnlocked, saved.level,
                    saved.automationPurchased, saved.brewingProgressSeconds, saved.phase));
                precedingUnlocked = saved.isUnlocked;
            }

            if (floors.Count == 0
                || !floors[0].IsUnlocked
                || floors[0].FloorId != model.Floors[0].FloorId)
            {
                error = "The first configured floor must be present and unlocked.";
                return false;
            }

            snapshot = new TowerSnapshot(coins, floors);
            error = null;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
