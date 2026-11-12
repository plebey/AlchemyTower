using System;
using UnityEngine;

namespace AlchemyTower
{
    [CreateAssetMenu(fileName = "TowerDefinition", menuName = "Alchemy Tower/Tower Definition")]
    public sealed class TowerDefinitionSO : ScriptableObject
    {
        [SerializeField] private FloorDefinitionSO[] floors = Array.Empty<FloorDefinitionSO>();

        public int FloorCount => floors?.Length ?? 0;

        public FloorDefinitionSO GetFloorDefinition(int floorNumber)
        {
            if (floors == null || floorNumber < 1 || floorNumber > floors.Length)
                throw new ArgumentOutOfRangeException(nameof(floorNumber));

            return floors[floorNumber - 1];
        }

        public TowerModel CreateModel(int initialCoins = 0)
        {
            if (floors == null || floors.Length == 0)
                throw new InvalidOperationException("Tower definition must contain at least one floor.");

            FloorRules[] rules = new FloorRules[floors.Length];
            for (int i = 0; i < floors.Length; i++)
            {
                if (floors[i] == null)
                    throw new InvalidOperationException($"Tower floor entry {i + 1} is missing a definition.");

                rules[i] = floors[i].CreateRules();
            }

            return new TowerModel(rules, initialCoins);
        }
    }
}
