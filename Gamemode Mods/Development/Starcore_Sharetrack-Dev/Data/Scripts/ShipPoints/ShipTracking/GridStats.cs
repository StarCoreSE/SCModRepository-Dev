using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoreSystems.Api;
using DefenseShields;
using klime.PointCheck;
using Sandbox.Game.Entities;
using Sandbox.Game.GUI.DebugInputComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace SCModRepository_Dev.Gamemode_Mods.Development.Starcore_Sharetrack_Dev.Data.Scripts.ShipPoints
{
    internal class GridStats // TODO convert this to be event-driven. OnBlockPlace, etc. Keep a queue.
    {
        private ShieldApi ShieldApi => PointCheck.I.ShieldApi;
        private WcApi WcApi => PointCheck.I.WcApi;

        private readonly HashSet<IMySlimBlock> _slimBlocks = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMyCubeBlock> _fatBlocks = new HashSet<IMyCubeBlock>();

        #region Public Methods

        public GridStats(IMyCubeGrid grid)
        {
            Grid = grid;

            Grid.OnBlockAdded += OnBlockAdd;
            Grid.OnBlockRemoved += OnBlockRemove;
        }

        public void Close()
        {
            Grid.OnBlockAdded -= OnBlockAdd;
            Grid.OnBlockRemoved -= OnBlockRemove;

            _slimBlocks.Clear();
            _fatBlocks.Clear();
        }

        public void Update()
        {
            BattlePoints = 0;
            OffensivePoints = 0;
            PowerPoints = 0;
            MovementPoints = 0;
            PointDefensePoints = 0;

            // Setting battlepoints first so that calcs can do calc stuff
            foreach (var block in _fatBlocks) // If slimblock points become necessary in the future, change this to _slimBlock
                CalculateCost(block);

            UpdateGlobalStats();
            UpdateShieldStats();
            UpdateWeaponStats();
        }

        #endregion

        #region Public Fields

        public readonly IMyCubeGrid Grid;

        // Global Stats
        public int BlockCount { get; private set; } = 0;
        public int HeavyArmorCount { get; private set; } = 0;
        public int PCU { get; private set; } = 0;
        public readonly Dictionary<string, int> BlockCounts = new Dictionary<string, int>();
        public readonly Dictionary<string, int> SpecialBlockCounts = new Dictionary<string, int>();
        public float TotalThrust { get; private set; } = 0;
        public float TotalTorque { get; private set; } = 0;
        public float TotalPower { get; private set; } = 0;

        // BattlePoint Stats
        public int BattlePoints { get; private set; } = 0;
        public int OffensivePoints { get; private set; } = 0;
        public int PowerPoints { get; private set; } = 0;
        public int MovementPoints { get; private set; } = 0;
        public int PointDefensePoints { get; private set; } = 0;

        // Shield Stats
        public float OriginalMaxShieldHealth { get; private set; } = -1;
        public float MaxShieldHealth { get; private set; } = -1;
        public float CurrentShieldPercent { get; private set; } = -1;

        // Weapon Stats
        public readonly Dictionary<string, int> WeaponCounts = new Dictionary<string, int>();

        #endregion

        #region Private Actions

        private void OnBlockAdd(IMySlimBlock block)
        {
            if (block == null)
                return;

            _slimBlocks.Add(block);
            if (block.FatBlock != null)
                _fatBlocks.Add(block.FatBlock);
        }

        private void OnBlockRemove(IMySlimBlock block)
        {
            if (block == null)
                return;

            _slimBlocks.Remove(block);
            if (block.FatBlock != null)
                _fatBlocks.Remove(block.FatBlock);
        }

        #endregion

        #region Private Fields

        // TODO

        #endregion

        #region Private Methods


        private void UpdateGlobalStats()
        {
            BlockCounts.Clear();

            TotalThrust = 0;
            TotalTorque = 0;
            TotalPower = 0;

            foreach (var block in _fatBlocks)
            {
                if (block is IMyThrust)
                    TotalThrust += ((IMyThrust)block).MaxEffectiveThrust;

                else if (block is IMyGyro)
                    TotalTorque += ((IMyGyro)block).GyroPower;

                else if (block is IMyPowerProducer)
                    TotalPower += ((IMyPowerProducer)block).CurrentOutput;

                else if (!WcApi.HasCoreWeapon((MyEntity)block))
                {
                    string blockDisplayName = block.DefinitionDisplayNameText;
                    float ignored = 0;
                    ShipTracker.ClimbingCostRename(ref blockDisplayName, ref ignored);
                    if (!SpecialBlockCounts.ContainsKey(blockDisplayName))
                        SpecialBlockCounts.Add(blockDisplayName, 0);
                    SpecialBlockCounts[blockDisplayName]++;
                }
                    
            }

            BlockCount = ((MyCubeGrid)Grid).BlocksCount;
            PCU = ((MyCubeGrid)Grid).BlocksPCU;
            HeavyArmorCount = 0;
            foreach (var slimBlock in _slimBlocks)
            {
                if (slimBlock.FatBlock != null)
                    continue;

                string subtype = slimBlock.BlockDefinition.Id.SubtypeName.ToLower();
                if (subtype.Contains("heavy"))
                    HeavyArmorCount++;
            }
        }

        private void UpdateShieldStats()
        {
            var shieldController = ShieldApi.GetShieldBlock(Grid);
            if (shieldController == null)
            {
                OriginalMaxShieldHealth = -1;
                MaxShieldHealth = -1;
                CurrentShieldPercent = -1;
                return;
            }

            MaxShieldHealth = ShieldApi.GetMaxHpCap(shieldController);
            if (OriginalMaxShieldHealth == -1 && !ShieldApi.IsFortified(shieldController))
                OriginalMaxShieldHealth = MaxShieldHealth;
            CurrentShieldPercent = ShieldApi.GetShieldPercent(shieldController);
        }

        private void UpdateWeaponStats()
        {
            WeaponCounts.Clear();
            foreach (var weaponBlock in _fatBlocks)
            {
                // Check that the block has points and is a weapon
                int weaponPoints;
                string weaponDisplayName = weaponBlock.DefinitionDisplayNameText;
                if (!PointCheck.PointValues.TryGetValue(weaponBlock.BlockDefinition.SubtypeName, out weaponPoints) ||
                    !WcApi.HasCoreWeapon((MyEntity) weaponBlock))
                    continue;

                float thisClimbingCostMult = 0;

                ShipTracker.ClimbingCostRename(ref weaponDisplayName, ref thisClimbingCostMult);

                if (!WeaponCounts.ContainsKey(weaponDisplayName))
                    WeaponCounts.Add(weaponDisplayName, 0);

                WeaponCounts[weaponDisplayName]++;
            }
        }

        private void CalculateCost(IMyCubeBlock block)
        {
            int blockPoints;
            string blockDisplayName = block.DefinitionDisplayNameText;
            if (!PointCheck.PointValues.TryGetValue(block.BlockDefinition.SubtypeName, out blockPoints))
                return;

            float thisClimbingCostMult = 0;
            ShipTracker.ClimbingCostRename(ref blockDisplayName, ref thisClimbingCostMult);

            if (!BlockCounts.ContainsKey(blockDisplayName))
                BlockCounts.Add(blockDisplayName, 0);

            int thiSpecialBlockCountsockCount = BlockCounts[blockDisplayName]++;

            if (thisClimbingCostMult > 0 && thiSpecialBlockCountsockCount > 1)
                blockPoints += (int)(blockPoints * thiSpecialBlockCountsockCount * thisClimbingCostMult);

            {
                if (block is IMyThrust || block is IMyGyro)
                    MovementPoints += blockPoints;
                if (block is IMyPowerProducer)
                    PowerPoints += blockPoints;
                if (WcApi.HasCoreWeapon((MyEntity)block))
                {
                    var validTargetTypes = new List<string>();
                    WcApi.GetTurretTargetTypes((MyEntity)block, validTargetTypes);
                    if (validTargetTypes.Contains("Projectiles"))
                        PointDefensePoints += blockPoints;
                    else
                        OffensivePoints += blockPoints;
                }
            }

            BattlePoints += blockPoints;
        }

        #endregion
    }
}
