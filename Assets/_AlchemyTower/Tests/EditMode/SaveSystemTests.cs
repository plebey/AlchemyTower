using System;
using NUnit.Framework;

namespace AlchemyTower.Tests
{
    public sealed class SaveSystemTests
    {
        [Test]
        public void SaveLoad_RoundTripRestoresSnapshot()
        {
            MemorySaveStorage storage = new MemorySaveStorage();
            TowerModel source = CreateTower(initialCoins: 500);
            using (SaveService saves = new SaveService(source, storage))
            {
                Assert.That(source.TryUnlockFloor(2), Is.True);
                source.Tick(2.25f);
                Assert.That(source.GetFloor(1).TryUpgrade(), Is.True);
                Assert.That(saves.TrySave(), Is.True);
            }

            TowerModel restored = CreateTower();
            using (SaveService saves = new SaveService(restored, storage))
                Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.Loaded));

            Assert.That(restored.Coins, Is.EqualTo(source.Coins));
            Assert.That(restored.GetFloor(1).Level, Is.EqualTo(2));
            Assert.That(restored.GetFloor(1).ProgressSeconds, Is.EqualTo(2.25f).Within(0.0001f));
            Assert.That(restored.GetFloor(2).IsUnlocked, Is.True);
        }

        [Test]
        public void Load_RestoresSharedCoinBalanceExactlyOnce()
        {
            MemorySaveStorage storage = SaveTower(CreateProgressedTower(initialCoins: 777));
            TowerModel restored = CreateTower(initialCoins: 10);

            using (SaveService saves = new SaveService(restored, storage))
                Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.Loaded));

            Assert.That(restored.Coins, Is.EqualTo(777));
            Assert.That(restored.GetFloor(1).Coins, Is.EqualTo(777));
            Assert.That(restored.GetFloor(2).Coins, Is.EqualTo(777));
        }

        [Test]
        public void Load_RestoresFloorUnlockState()
        {
            TowerModel source = CreateTower(initialCoins: 200);
            Assert.That(source.TryUnlockFloor(2), Is.True);
            MemorySaveStorage storage = SaveTower(source);
            TowerModel restored = CreateTower();

            using (SaveService saves = new SaveService(restored, storage))
                Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.Loaded));

            Assert.That(restored.GetFloor(2).IsUnlocked, Is.True);
        }

        [Test]
        public void Load_RestoresIndependentLevelsAndAutomation()
        {
            TowerModel source = CreateTower(initialCoins: 10000);
            Assert.That(source.TryUnlockFloor(2), Is.True);
            Assert.That(source.GetFloor(1).TryUpgrade(), Is.True);
            UpgradeToLevelFive(source.GetFloor(2));
            Assert.That(source.GetFloor(2).TryBuyAutomation(), Is.True);
            MemorySaveStorage storage = SaveTower(source);
            TowerModel restored = CreateTower();

            using (SaveService saves = new SaveService(restored, storage))
                Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.Loaded));

            Assert.That(restored.GetFloor(1).Level, Is.EqualTo(2));
            Assert.That(restored.GetFloor(1).IsAutomated, Is.False);
            Assert.That(restored.GetFloor(2).Level, Is.EqualTo(5));
            Assert.That(restored.GetFloor(2).IsAutomated, Is.True);
        }

        [Test]
        public void Load_PreservesPartialBrewingProgress()
        {
            TowerModel source = CreateProgressedTower();
            MemorySaveStorage storage = SaveTower(source);
            TowerModel restored = CreateTower();

            using (SaveService saves = new SaveService(restored, storage))
                Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.Loaded));

            Assert.That(restored.GetFloor(1).Phase, Is.EqualTo(FloorPhase.Brewing));
            Assert.That(restored.GetFloor(1).ProgressSeconds, Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void Load_PreservesReadyPotionWithoutDuplicateReward()
        {
            TowerModel source = CreateTower(initialCoins: 30);
            MemorySaveStorage storage = new MemorySaveStorage();
            using (SaveService sourceSaves = new SaveService(source, storage))
            {
                source.Tick(5f);
                Assert.That(sourceSaves.TrySave(), Is.True);
            }
            TowerModel restored = CreateTower();

            using (SaveService saves = new SaveService(restored, storage))
                Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.Loaded));

            FloorModel floor = restored.GetFloor(1);
            Assert.That(restored.Coins, Is.EqualTo(30));
            Assert.That(floor.Phase, Is.EqualTo(FloorPhase.ReadyToCollect));
            Assert.That(floor.TryCollect(), Is.True);
            Assert.That(floor.TryCollect(), Is.False);
            Assert.That(restored.Coins, Is.EqualTo(40));
        }

        [Test]
        public void Load_WithoutSaveKeepsFreshConfigurationDefaults()
        {
            TowerModel tower = CreateTower();
            using SaveService saves = new SaveService(tower, new MemorySaveStorage());

            Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.NoSave));
            Assert.That(saves.SavingEnabled, Is.True);
            Assert.That(tower.Coins, Is.Zero);
            Assert.That(tower.GetFloor(1).IsUnlocked, Is.True);
            Assert.That(tower.GetFloor(2).IsUnlocked, Is.False);
        }

        [Test]
        public void Load_CorruptedJsonUsesSafeFallbackAndBlocksOverwrite()
        {
            MemorySaveStorage storage = new MemorySaveStorage { Data = "{broken json" };
            TowerModel tower = CreateTower();
            using SaveService saves = new SaveService(tower, storage);

            Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.CorruptedOrInvalid));
            Assert.That(saves.SavingEnabled, Is.False);
            Assert.That(tower.GetFloor(1).Level, Is.EqualTo(1));
            Assert.That(storage.Data, Is.EqualTo("{broken json"));
        }

        [Test]
        public void Load_UnsupportedFutureVersionBlocksOverwrite()
        {
            MemorySaveStorage storage = new MemorySaveStorage
            {
                Data = "{\"schemaVersion\":2,\"coinBalance\":\"0\",\"floors\":[]}"
            };
            using SaveService saves = new SaveService(CreateTower(), storage);

            Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.UnsupportedFutureVersion));
            Assert.That(saves.SavingEnabled, Is.False);
        }

        [Test]
        public void Load_OlderSaveMissingNewlyConfiguredFloorKeepsNewFloorDefault()
        {
            MemorySaveStorage storage = SaveTower(CreateProgressedTower());
            TowerModel expanded = CreateThreeFloorTower();

            using (SaveService saves = new SaveService(expanded, storage))
                Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.Loaded));

            FloorModel addedFloor = expanded.GetFloor(3);
            Assert.That(addedFloor.IsUnlocked, Is.False);
            Assert.That(addedFloor.Level, Is.EqualTo(1));
            Assert.That(addedFloor.ProgressSeconds, Is.Zero);
        }

        [Test]
        public void Load_UnknownRemovedFloorIdentifierIsIgnored()
        {
            GameSaveData data = ValidFirstFloorSave();
            data.coinBalance = "42";
            data.floors.Add(new FloorSaveData
            {
                floorId = "removed_floor",
                isUnlocked = true,
                level = 99,
                phase = FloorPhase.Brewing
            });
            using SaveService saves = new SaveService(
                CreateTower(),
                new MemorySaveStorage { Data = "present" },
                new FixedSaveSerializer(data));

            Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.Loaded));
        }

        [Test]
        public void Load_NonFiniteProgressIsRejected()
        {
            GameSaveData data = ValidFirstFloorSave();
            data.floors[0].brewingProgressSeconds = float.NaN;
            using SaveService saves = new SaveService(
                CreateTower(),
                new MemorySaveStorage { Data = "present" },
                new FixedSaveSerializer(data));

            Assert.That(saves.Load(), Is.EqualTo(SaveLoadStatus.CorruptedOrInvalid));
            Assert.That(saves.SavingEnabled, Is.False);
        }

        [Test]
        public void Tick_WhenDirtySavesPeriodicallyAndClearsDirtyState()
        {
            MemorySaveStorage storage = new MemorySaveStorage();
            TowerModel tower = CreateTower();
            using SaveService saves = new SaveService(tower, storage);
            tower.Tick(1f);

            Assert.That(saves.IsDirty, Is.True);
            saves.Tick(SaveService.PeriodicSaveIntervalSeconds);

            Assert.That(storage.SaveAttempts, Is.EqualTo(1));
            Assert.That(saves.IsDirty, Is.False);
            Assert.That(storage.Data, Is.Not.Empty);
        }

        [Test]
        public void FailedStorageWriteKeepsPendingStateAndBacksOff()
        {
            MemorySaveStorage storage = new MemorySaveStorage { ThrowOnSave = true };
            TowerModel tower = CreateTower(initialCoins: 100);
            using SaveService saves = new SaveService(tower, storage);
            Assert.That(tower.GetFloor(1).TryUpgrade(), Is.True);

            saves.Tick(SaveService.ImportantSaveDelaySeconds);

            Assert.That(storage.SaveAttempts, Is.EqualTo(1));
            Assert.That(saves.IsDirty, Is.True);
            Assert.That(saves.LastError, Is.Not.Empty);
            saves.Tick(0.1f);
            Assert.That(storage.SaveAttempts, Is.EqualTo(1));
        }

        private static MemorySaveStorage SaveTower(TowerModel tower)
        {
            MemorySaveStorage storage = new MemorySaveStorage();
            using SaveService saves = new SaveService(tower, storage);
            tower.Tick(0.00001f);
            Assert.That(saves.TrySave(), Is.True);
            return storage;
        }

        private static TowerModel CreateProgressedTower(int initialCoins = 0)
        {
            TowerModel tower = CreateTower(initialCoins);
            tower.Tick(2.5f);
            return tower;
        }

        private static void UpgradeToLevelFive(FloorModel floor)
        {
            while (floor.Level < 5)
                Assert.That(floor.TryUpgrade(), Is.True);
        }

        private static TowerModel CreateTower(int initialCoins = 0)
        {
            return new TowerModel(new[]
            {
                Rules("first_floor", 1),
                Rules("second_floor", 2, 150)
            }, initialCoins);
        }

        private static TowerModel CreateThreeFloorTower()
        {
            return new TowerModel(new[]
            {
                Rules("first_floor", 1),
                Rules("second_floor", 2, 150),
                Rules("third_floor", 3, 2000)
            });
        }

        private static FloorRules Rules(string id, int number, int unlockCost = 0)
        {
            return new FloorRules(id, number == 1 ? 5f : 6f, 0.35f, 10, 30,
                1.10m, 1.15m, number, unlockCost);
        }

        private static GameSaveData ValidFirstFloorSave()
        {
            GameSaveData data = new GameSaveData { coinBalance = "0" };
            data.floors.Add(new FloorSaveData
            {
                floorId = "first_floor",
                isUnlocked = true,
                level = 1,
                phase = FloorPhase.Brewing
            });
            return data;
        }

        private sealed class MemorySaveStorage : ISaveStorage
        {
            public string Data;
            public bool ThrowOnSave;
            public int SaveAttempts;

            public bool Exists() => Data != null;
            public string Load() => Data;

            public void Save(string data)
            {
                SaveAttempts++;
                if (ThrowOnSave)
                    throw new InvalidOperationException("Simulated storage failure.");
                Data = data;
            }

            public void Delete() => Data = null;
        }

        private sealed class FixedSaveSerializer : ISaveSerializer
        {
            private readonly GameSaveData data;

            public FixedSaveSerializer(GameSaveData data)
            {
                this.data = data;
            }

            public string Serialize(GameSaveData value) => throw new NotSupportedException();
            public GameSaveData Deserialize(string json) => data;
        }
    }
}
