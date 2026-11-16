using UnityEngine;

namespace AlchemyTower
{
    [DisallowMultipleComponent]
    public sealed class ShopView : MonoBehaviour
    {
        [Header("Editable Card Prefabs")]
        [SerializeField] private ShopItemView alchemicalGlovesCardPrefab;
        [SerializeField] private ShopItemView goldenSealCardPrefab;

        private ShopItemView glovesCard;
        private ShopItemView sealCard;
        private TowerModel tower;

        public void Bind(TowerModel towerModel)
        {
            tower = towerModel;
            EnsureCards();
            if (glovesCard == null || sealCard == null)
                return;
            float baseTapBoostSeconds = tower.GetFloor(1).BaseTapBoostSeconds;
            glovesCard.Bind(tower.Shop, baseTapBoostSeconds);
            sealCard.Bind(tower.Shop, baseTapBoostSeconds);
        }

        public void Refresh()
        {
            glovesCard?.Refresh();
            sealCard?.Refresh();
        }

        private void EnsureCards()
        {
            if (glovesCard != null && sealCard != null)
                return;
            if (alchemicalGlovesCardPrefab == null || goldenSealCardPrefab == null)
            {
                Debug.LogError("ShopView is missing one or more editable card Prefabs.", this);
                enabled = false;
                return;
            }

            glovesCard = Instantiate(alchemicalGlovesCardPrefab, transform);
            glovesCard.name = "Alchemical Gloves Card";
            sealCard = Instantiate(goldenSealCardPrefab, transform);
            sealCard.name = "Golden Seal Card";
        }
    }
}
