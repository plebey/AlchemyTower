using System;
using System.Collections.Generic;

namespace AlchemyTower
{
    public enum QuestStatus
    {
        InProgress,
        RewardAvailable,
        RewardClaimed
    }

    public enum QuestConditionKind
    {
        FloorLevel,
        FloorUnlocked,
        FloorAutomated
    }

    public readonly struct QuestDefinition
    {
        public QuestDefinition(string id, string title, string description, QuestConditionKind condition,
            string floorId, int target, int rewardCoins)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Quest ID must not be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Quest title must not be empty.", nameof(title));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Quest description must not be empty.", nameof(description));
            if (string.IsNullOrWhiteSpace(floorId))
                throw new ArgumentException("Floor ID must not be empty.", nameof(floorId));
            if (target < 1)
                throw new ArgumentOutOfRangeException(nameof(target));
            if (rewardCoins <= 0)
                throw new ArgumentOutOfRangeException(nameof(rewardCoins));

            Id = id;
            Title = title;
            Description = description;
            Condition = condition;
            FloorId = floorId;
            Target = target;
            RewardCoins = rewardCoins;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public QuestConditionKind Condition { get; }
        public string FloorId { get; }
        public int Target { get; }
        public int RewardCoins { get; }
    }

    public sealed class QuestModel
    {
        private readonly QuestBook owner;

        internal QuestModel(QuestDefinition definition, QuestBook owner)
        {
            Definition = definition;
            this.owner = owner;
        }

        public QuestDefinition Definition { get; }
        public string Id => Definition.Id;
        public string Title => Definition.Title;
        public string Description => Definition.Description;
        public int RewardCoins => Definition.RewardCoins;
        public int Target => Definition.Target;
        public int CurrentProgress => owner.GetProgress(Definition);
        public QuestStatus Status => owner.GetStatus(Definition);

        public bool TryClaimReward() => owner.TryClaimReward(Id);
    }

    public sealed class QuestBook
    {
        public const string FirstStepsId = "quest_first_steps";
        public const string TowerGrowsId = "quest_tower_grows";
        public const string FirstAssistantId = "quest_first_assistant";
        public const string FirstFloorId = "first_floor";
        public const string SecondFloorId = "second_floor";

        private readonly TowerModel tower;
        private readonly CoinWallet wallet;
        private readonly Action<bool> stateChanged;
        private readonly QuestModel[] quests;
        private readonly HashSet<string> claimedQuestIds = new HashSet<string>(StringComparer.Ordinal);

        internal QuestBook(TowerModel tower, CoinWallet wallet, Action<bool> stateChanged)
        {
            this.tower = tower ?? throw new ArgumentNullException(nameof(tower));
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.stateChanged = stateChanged;

            QuestDefinition[] definitions =
            {
                new QuestDefinition(FirstStepsId, "Первые успехи", "Повысьте первый этаж до 3-го уровня.",
                    QuestConditionKind.FloorLevel, FirstFloorId, 3, 50),
                new QuestDefinition(TowerGrowsId, "Башня растёт", "Откройте второй этаж.",
                    QuestConditionKind.FloorUnlocked, SecondFloorId, 1, 100),
                new QuestDefinition(FirstAssistantId, "Первый помощник", "Купите автоматизацию первого этажа.",
                    QuestConditionKind.FloorAutomated, FirstFloorId, 1, 150)
            };

            quests = new QuestModel[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
                quests[i] = new QuestModel(definitions[i], this);
        }

        public IReadOnlyList<QuestModel> Quests => quests;

        public QuestModel GetQuest(string questId)
        {
            for (int i = 0; i < quests.Length; i++)
            {
                if (string.Equals(quests[i].Id, questId, StringComparison.Ordinal))
                    return quests[i];
            }

            throw new ArgumentException($"Unknown quest ID '{questId}'.", nameof(questId));
        }

        public bool TryClaimReward(string questId)
        {
            QuestModel quest;
            try
            {
                quest = GetQuest(questId);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (quest.Status != QuestStatus.RewardAvailable)
                return false;

            // Record the stable ID before notifying persistence through the shared wallet.
            claimedQuestIds.Add(quest.Id);
            wallet.Add(quest.RewardCoins);
            stateChanged?.Invoke(true);
            return true;
        }

        internal QuestStatus GetStatus(QuestDefinition definition)
        {
            if (claimedQuestIds.Contains(definition.Id))
                return QuestStatus.RewardClaimed;

            return GetProgress(definition) >= definition.Target
                ? QuestStatus.RewardAvailable
                : QuestStatus.InProgress;
        }

        internal int GetProgress(QuestDefinition definition)
        {
            FloorModel floor = FindFloor(definition.FloorId);
            if (floor == null)
                return 0;

            int progress = definition.Condition switch
            {
                QuestConditionKind.FloorLevel => floor.IsUnlocked ? floor.Level : 0,
                QuestConditionKind.FloorUnlocked => floor.IsUnlocked ? 1 : 0,
                QuestConditionKind.FloorAutomated => floor.IsAutomated ? 1 : 0,
                _ => 0
            };
            return Math.Min(progress, definition.Target);
        }

        internal IReadOnlyList<string> CaptureClaimedQuestIds()
        {
            string[] result = new string[claimedQuestIds.Count];
            claimedQuestIds.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        internal void RestoreClaimedQuestIds(IReadOnlyList<string> questIds)
        {
            claimedQuestIds.Clear();
            if (questIds == null)
                return;

            for (int i = 0; i < questIds.Count; i++)
                claimedQuestIds.Add(questIds[i]);
        }

        private FloorModel FindFloor(string floorId)
        {
            for (int i = 0; i < tower.Floors.Count; i++)
            {
                if (string.Equals(tower.Floors[i].FloorId, floorId, StringComparison.Ordinal))
                    return tower.Floors[i];
            }

            return null;
        }
    }
}
