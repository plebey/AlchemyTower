using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    [DisallowMultipleComponent]
    public sealed class QuestCardView : MonoBehaviour
    {
        [Header("Authored References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text rewardAndStateText;
        [SerializeField] private Button claimButton;
        [SerializeField] private Text claimButtonText;

        private QuestModel quest;
        private Action rewardClaimed;

        public Image IconImage => iconImage;

        private void Awake()
        {
            if (claimButton != null)
                claimButton.onClick.AddListener(OnClaimClicked);
        }

        private void OnDestroy()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(OnClaimClicked);
        }

        public void Bind(QuestModel questModel, Action onRewardClaimed)
        {
            quest = questModel ?? throw new ArgumentNullException(nameof(questModel));
            rewardClaimed = onRewardClaimed;
            Refresh();
        }

        public void Refresh()
        {
            if (quest == null || titleText == null || descriptionText == null || progressText == null
                || rewardAndStateText == null || claimButton == null || claimButtonText == null)
            {
                return;
            }

            QuestStatus status = quest.Status;
            titleText.text = quest.Title;
            descriptionText.text = quest.Description;
            progressText.text = $"Прогресс: {quest.CurrentProgress}/{quest.Target}";
            string reward = quest.RewardCoins.ToString("N0", CultureInfo.InvariantCulture);
            rewardAndStateText.text = status switch
            {
                QuestStatus.InProgress => $"Награда: {reward} монет\nВ процессе",
                QuestStatus.RewardClaimed => $"Награда: {reward} монет\nПолучено",
                _ => $"Награда: {reward} монет"
            };
            claimButtonText.text = $"Получить\n{reward} монет";
            claimButton.gameObject.SetActive(status == QuestStatus.RewardAvailable);
        }

        private void OnClaimClicked()
        {
            if (quest != null && quest.TryClaimReward())
                rewardClaimed?.Invoke();
            Refresh();
        }
    }
}
