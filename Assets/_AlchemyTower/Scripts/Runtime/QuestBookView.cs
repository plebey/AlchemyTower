using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlchemyTower
{
    [DisallowMultipleComponent]
    public sealed class QuestBookView : MonoBehaviour
    {
        [Header("Editable Card Prefab")]
        [SerializeField] private QuestCardView questCardPrefab;

        private readonly List<QuestCardView> cards = new List<QuestCardView>();
        private TowerModel tower;
        private Action rewardClaimed;

        public QuestCardView QuestCardPrefab => questCardPrefab;

        public void Bind(TowerModel towerModel, Action onRewardClaimed)
        {
            tower = towerModel ?? throw new ArgumentNullException(nameof(towerModel));
            rewardClaimed = onRewardClaimed;
            EnsureCards();
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < cards.Count; i++)
                cards[i].Refresh();
        }

        private void EnsureCards()
        {
            if (cards.Count > 0)
                return;
            if (questCardPrefab == null)
            {
                Debug.LogError("QuestBookView is missing the editable QuestCard Prefab.", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < tower.Quests.Quests.Count; i++)
            {
                QuestCardView card = Instantiate(questCardPrefab, transform);
                card.name = $"Quest Card — {tower.Quests.Quests[i].Id}";
                card.Bind(tower.Quests.Quests[i], OnRewardClaimed);
                cards.Add(card);
            }
        }

        private void OnRewardClaimed()
        {
            Refresh();
            rewardClaimed?.Invoke();
        }
    }
}
