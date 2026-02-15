// BattlerUnityClient/GameState/BattleManager.cs
// Manages the client-side battle state, army composition, and unit catalog.
// Acts as the bridge between networking and rendering.

using System.Collections.Generic;
using UnityEngine;

namespace BattlerUnityClient
{
    /// <summary>
    /// Manages the client-side battle state.
    /// Provides the unit type catalog for the army builder
    /// and tracks live battle state from the server.
    /// </summary>
    public class BattleManager
    {
        /// <summary>All available unit types (mirrors server's DefaultUnitTypes).</summary>
        public List<UnitTypeInfo> UnitCatalog { get; private set; }

        /// <summary>Player 1's army composition for the army builder.</summary>
        public List<ArmyUnitPlacement> Army1 { get; private set; } = new List<ArmyUnitPlacement>();

        /// <summary>Player 2's army composition.</summary>
        public List<ArmyUnitPlacement> Army2 { get; private set; } = new List<ArmyUnitPlacement>();

        /// <summary>The latest battle state from the server.</summary>
        public BattleStateData CurrentState { get; set; }

        /// <summary>Total budget for army building.</summary>
        public int ArmyBudget { get; set; } = 2000;

        /// <summary>Current spending.</summary>
        public int ArmySpent { get; private set; }

        public BattleManager()
        {
            InitializeUnitCatalog();
        }

        /// <summary>
        /// Initializes the unit catalog to match the server's DefaultUnitTypes.
        /// </summary>
        private void InitializeUnitCatalog()
        {
            UnitCatalog = new List<UnitTypeInfo>
            {
                new UnitTypeInfo
                {
                    name = "crawler", displayName = "Crawler",
                    health = 50, speed = 3.0f,
                    attackDamage = 10, attackRange = 1.5f, attackCooldown = 0.8f,
                    projectileSpeed = 0, splashRadius = 0, cost = 100,
                    description = "Fast melee unit. Cheap and numerous."
                },
                new UnitTypeInfo
                {
                    name = "archer", displayName = "Archer",
                    health = 30, speed = 2.0f,
                    attackDamage = 15, attackRange = 8.0f, attackCooldown = 1.5f,
                    projectileSpeed = 15.0f, splashRadius = 0, cost = 150,
                    description = "Ranged unit. Fragile but long reach."
                },
                new UnitTypeInfo
                {
                    name = "tank", displayName = "Tank",
                    health = 200, speed = 1.0f,
                    attackDamage = 40, attackRange = 5.0f, attackCooldown = 2.0f,
                    projectileSpeed = 20.0f, splashRadius = 0, cost = 300,
                    description = "Heavy armored unit. Slow but durable."
                },
                new UnitTypeInfo
                {
                    name = "artillery", displayName = "Artillery",
                    health = 60, speed = 0.5f,
                    attackDamage = 80, attackRange = 15.0f, attackCooldown = 4.0f,
                    projectileSpeed = 10.0f, splashRadius = 3.0f, cost = 400,
                    description = "Long-range splash damage. Very slow."
                },
                new UnitTypeInfo
                {
                    name = "scout", displayName = "Scout",
                    health = 20, speed = 5.0f,
                    attackDamage = 5, attackRange = 2.0f, attackCooldown = 0.5f,
                    projectileSpeed = 0, splashRadius = 0, cost = 50,
                    description = "Extremely fast but weak. Good for flanking."
                },
                new UnitTypeInfo
                {
                    name = "healer", displayName = "Healer",
                    health = 40, speed = 2.0f,
                    attackDamage = -10, attackRange = 6.0f, attackCooldown = 2.0f,
                    projectileSpeed = 0, splashRadius = 2.0f, cost = 250,
                    description = "Heals nearby friendly units."
                },
            };
        }

        /// <summary>
        /// Adds a unit to the player's army. Returns false if over budget.
        /// </summary>
        public bool AddUnitToArmy(string typeName, float x, float y)
        {
            var unitType = UnitCatalog.Find(u => u.name == typeName);
            if (unitType == null) return false;

            if (ArmySpent + unitType.cost > ArmyBudget) return false;

            Army1.Add(new ArmyUnitPlacement { type_name = typeName, x = x, y = y });
            ArmySpent += unitType.cost;
            return true;
        }

        /// <summary>
        /// Removes the last added unit from the army.
        /// </summary>
        public void RemoveLastUnit()
        {
            if (Army1.Count == 0) return;
            var last = Army1[Army1.Count - 1];
            var unitType = UnitCatalog.Find(u => u.name == last.type_name);
            Army1.RemoveAt(Army1.Count - 1);
            if (unitType != null) ArmySpent -= unitType.cost;
        }

        /// <summary>
        /// Clears the player's army.
        /// </summary>
        public void ClearArmy()
        {
            Army1.Clear();
            ArmySpent = 0;
        }

        /// <summary>
        /// Gets unit type info by name.
        /// </summary>
        public UnitTypeInfo GetUnitType(string name)
        {
            return UnitCatalog.Find(u => u.name == name);
        }

        /// <summary>
        /// Generates a random army for NPC/demo purposes.
        /// </summary>
        public List<ArmyUnitPlacement> GenerateRandomArmy(float xMin, float xMax, float yMin, float yMax, int budget)
        {
            var army = new List<ArmyUnitPlacement>();
            int spent = 0;
            int maxAttempts = 200;

            for (int i = 0; i < maxAttempts && spent < budget; i++)
            {
                var unitType = UnitCatalog[Random.Range(0, UnitCatalog.Count)];
                if (spent + unitType.cost > budget) continue;

                army.Add(new ArmyUnitPlacement
                {
                    type_name = unitType.name,
                    x = Random.Range(xMin, xMax),
                    y = Random.Range(yMin, yMax)
                });
                spent += unitType.cost;
            }

            return army;
        }
    }
}
