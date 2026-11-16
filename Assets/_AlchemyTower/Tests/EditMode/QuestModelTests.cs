using NUnit.Framework;

namespace AlchemyTower.Tests
{
    public sealed class QuestModelTests
    {
        [Test]
        public void FreshGame_HasThreeInProgressQuestsWithModelDerivedProgress()
        {
            TowerModel tower = CreateTower();

            Assert.That(tower.Quests.Quests.Count, Is.EqualTo(3));
            AssertQuest(tower, QuestBook.FirstStepsId, 1, 3, QuestStatus.InProgress);
            AssertQuest(tower, QuestBook.TowerGrowsId, 0, 1, QuestStatus.InProgress);
            AssertQuest(tower, QuestBook.FirstAssistantId, 0, 1, QuestStatus.InProgress);
            Assert.That(tower.Quests.GetQuest(QuestBook.FirstStepsId).RewardCoins, Is.EqualTo(50));
            Assert.That(tower.Quests.GetQuest(QuestBook.TowerGrowsId).RewardCoins, Is.EqualTo(100));
            Assert.That(tower.Quests.GetQuest(QuestBook.FirstAssistantId).RewardCoins, Is.EqualTo(150));
        }

        [Test]
        public void ConditionsReadCurrentFloorStateAndNeverAwardAutomatically()
        {
            TowerModel tower = CreateTower(initialCoins: 10000);
            int initialCoins = tower.Coins;

            Assert.That(tower.GetFloor(1).TryUpgrade(), Is.True);
            Assert.That(tower.GetFloor(1).TryUpgrade(), Is.True);
            int afterUpgrades = tower.Coins;
            AssertQuest(tower, QuestBook.FirstStepsId, 3, 3, QuestStatus.RewardAvailable);
            Assert.That(tower.Coins, Is.EqualTo(afterUpgrades));

            Assert.That(tower.TryUnlockFloor(2), Is.True);
            AssertQuest(tower, QuestBook.TowerGrowsId, 1, 1, QuestStatus.RewardAvailable);

            while (tower.GetFloor(1).Level < tower.GetFloor(1).AutomationRequiredLevel)
                Assert.That(tower.GetFloor(1).TryUpgrade(), Is.True);
            Assert.That(tower.GetFloor(1).TryBuyAutomation(), Is.True);
            AssertQuest(tower, QuestBook.FirstAssistantId, 1, 1, QuestStatus.RewardAvailable);
            Assert.That(tower.Coins, Is.LessThan(initialCoins));
        }

        [Test]
        public void ClaimReward_AddsApprovedAmountExactlyOnceAndMarksImportantChange()
        {
            TowerModel tower = CreateTower(initialCoins: 1000);
            bool importantChange = false;
            tower.StateChanged += important => importantChange |= important;
            Assert.That(tower.GetFloor(1).TryUpgrade(), Is.True);
            Assert.That(tower.GetFloor(1).TryUpgrade(), Is.True);
            QuestModel quest = tower.Quests.GetQuest(QuestBook.FirstStepsId);
            int beforeClaim = tower.Coins;
            importantChange = false;

            Assert.That(quest.TryClaimReward(), Is.True);
            Assert.That(tower.Coins, Is.EqualTo(beforeClaim + 50));
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.RewardClaimed));
            Assert.That(importantChange, Is.True);
            Assert.That(quest.TryClaimReward(), Is.False);
            Assert.That(tower.Coins, Is.EqualTo(beforeClaim + 50));
        }

        [Test]
        public void ClaimReward_FailsWhileConditionIsIncomplete()
        {
            TowerModel tower = CreateTower();

            Assert.That(tower.Quests.GetQuest(QuestBook.FirstStepsId).TryClaimReward(), Is.False);
            Assert.That(tower.Coins, Is.Zero);
        }

        private static void AssertQuest(TowerModel tower, string id, int progress, int target, QuestStatus status)
        {
            QuestModel quest = tower.Quests.GetQuest(id);
            Assert.That(quest.CurrentProgress, Is.EqualTo(progress));
            Assert.That(quest.Target, Is.EqualTo(target));
            Assert.That(quest.Status, Is.EqualTo(status));
        }

        private static TowerModel CreateTower(int initialCoins = 0)
        {
            return new TowerModel(new[]
            {
                Rules(QuestBook.FirstFloorId, 1),
                Rules(QuestBook.SecondFloorId, 2, 150)
            }, initialCoins);
        }

        private static FloorRules Rules(string id, int number, int unlockCost = 0)
        {
            return new FloorRules(id, 5f, 0.35f, 10, 30, 1.10m, 1.15m,
                number, unlockCost, automationRequiredLevel: 5);
        }
    }
}
