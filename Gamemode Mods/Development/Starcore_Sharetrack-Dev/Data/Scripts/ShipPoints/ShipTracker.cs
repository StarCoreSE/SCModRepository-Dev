using System;
using System.Collections.Generic;
using System.Text;
using Draygo.API;
using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace klime.PointCheck
{
    [ProtoContract]
    public class ShipTracker
    {
        [ProtoMember(10)] public int BlockCount;
        [ProtoMember(6)] public int Bpts;
        private readonly List<IMyCubeGrid> _connectedGrids = new List<IMyCubeGrid>();
        [ProtoMember(27)] public float CurrentGyro;
        [ProtoMember(19)] public float CurrentIntegrity;
        [ProtoMember(25)] public float CurrentPower;
        [ProtoMember(12)] public float CurrentShieldStrength;
        [ProtoMember(23)] public Vector3 FactionColor = Vector3.One;
        [ProtoMember(2)] public string FactionName;
        [ProtoMember(4)] public long GridId;


        //passable
        [ProtoMember(1)] public string GridName;

        //[ProtoMember(14)] public float DPS;
        [ProtoMember(16)] public Dictionary<string, int> GunL = new Dictionary<string, int>();
        [ProtoMember(9)] public float Heavyblocks;
        [ProtoMember(7)] public float InstalledThrust;
        [ProtoMember(18)] public bool IsFunctional;
        [ProtoMember(3)] public int LastUpdate;
        [ProtoMember(8)] public float Mass;
        [ProtoMember(37)] public int MiscBps;
        [ProtoMember(31)] public int MiscPercentage;
        [ProtoMember(34)] public int MovementBps;

        [ProtoMember(28)] public int MovementPercentage;

        private HudAPIv2.HUDMessage _nametag;
        [ProtoMember(36)] public int OffensiveBps;
        [ProtoMember(30)] public int OffensivePercentage;
        [ProtoMember(38)] public Vector3 OriginalFactionColor = Vector3.One;
        [ProtoMember(20)] public float OriginalIntegrity = -1;
        [ProtoMember(24)] public float OriginalPower = -1;
        [ProtoMember(41)] public float OriginalShieldStrength = -1;
        [ProtoMember(5)] public long OwnerId;

        [ProtoMember(17)] public string OwnerName;
        [ProtoMember(13)] public int Pcu;
        [ProtoMember(33)] public int PdInvest;
        [ProtoMember(32)] public int PdPercentage;
        [ProtoMember(22)] public Vector3 Position;
        [ProtoMember(35)] public int PowerBps;
        [ProtoMember(29)] public int PowerPercentage;
        [ProtoMember(26)] public Dictionary<string, int> Sbl = new Dictionary<string, int>();
        [ProtoMember(21)] public int ShieldHeat;
        [ProtoMember(11)] public float ShieldStrength;

        [ProtoMember(40)] public Dictionary<string, int> SubgridGunL = new Dictionary<string, int>();
        private readonly List<IMySlimBlock> _tmpBlocks = new List<IMySlimBlock>();

        public ShipTracker()
        {
        }

        public ShipTracker(IMyCubeGrid grid)
        {
            Grid = grid;
            GridId = grid.EntityId;

            grid.OnClose += OnClose;
            Update();
        }

        //Instance
        public IMyCubeGrid Grid { get; }
        public IMyPlayer Owner { get; private set; }

        public void OnClose(IMyEntity e)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                PointCheck.Sending.Remove(e.EntityId);
                PointCheck.Data.Remove(e.EntityId);
                DisposeHud();
            }

            e.OnClose -= OnClose;
        }

        public void Update()
        {
            // Why does this part exist???
            for (var j = 0; j < _tmpBlocks.Count; j++)
            {
                var slim = _tmpBlocks[j];
                if (slim?.CubeGrid == null || slim.IsDestroyed || slim.FatBlock == null)
                    continue;
            }

            LastUpdate = 5;
            if (Grid?.Physics == null)
                return;

            Reset();
            _connectedGrids.Clear();
            MyAPIGateway.GridGroups.GetGroup(Grid, GridLinkTypeEnum.Physical, _connectedGrids);

            if (_connectedGrids.Count <= 0)
                return;

            Mass = ((MyCubeGrid) Grid).GetCurrentMass();
            bool hasPower = false, hasCockpit = false, hasThrust = false, hasGyro = false;
            float movementBpts = 0, powerBpts = 0, offensiveBpts = 0, miscBpts = 0;
            int bonusBpts = 0, pdBpts = 0; // Initial value for point defense battlepoints
            string controller = null;

            TempBlockCheck(ref hasPower, ref hasCockpit, ref hasThrust, ref hasGyro, ref movementBpts,
                ref powerBpts, ref offensiveBpts, ref miscBpts, ref bonusBpts, ref pdBpts, ref controller);

            // pre-calculate totalBpts and totalBptsInv
            var totalBpts = movementBpts + powerBpts + offensiveBpts + miscBpts;
            var totalBptsInv = totalBpts > 0 ? 100f / totalBpts : 100f / (totalBpts + .1f);

            // pre-calculate offensiveBptsInv for point defense percentage
            var offensiveBptsInv = offensiveBpts > 0 ? 100f / offensiveBpts : 100f / (offensiveBpts + .1f);

            // calculate percentage of Bpts for each block type
            MovementPercentage = (int)(movementBpts * totalBptsInv + 0.5f);
            PowerPercentage = (int)(powerBpts * totalBptsInv + 0.5f);
            OffensivePercentage = (int)(offensiveBpts * totalBptsInv + 0.5f);
            MiscPercentage = (int)(miscBpts * totalBptsInv + 0.5f);

            // calculate percentage of point defense Bpts of offensive Bpts
            PdPercentage = (int)(pdBpts * offensiveBptsInv + 0.5f);

            PdInvest = pdBpts;
            MiscBps = (int)miscBpts;
            PowerBps = (int)powerBpts;
            OffensiveBps = (int)offensiveBpts;
            MovementBps = (int)movementBpts;

            var mainGrid = _connectedGrids[0];
            FactionName = "None";
            OwnerName = "Unowned";

            IsFunctional = hasPower && hasCockpit && hasGyro;

            if (mainGrid.BigOwners != null && mainGrid.BigOwners.Count > 0)
            {
                OwnerId = mainGrid.BigOwners[0];
                Owner = PointCheck.GetOwner(OwnerId);
                OwnerName = controller ?? Owner?.DisplayName ?? GridName;

                var f = MyAPIGateway.Session?.Factions?.TryGetPlayerFaction(OwnerId);
                if (f != null)
                {
                    FactionName = f.Tag ?? FactionName;
                    FactionColor = ColorMaskToRgb(f.CustomColor);
                    OriginalFactionColor = f.CustomColor;
                    //MyAPIGateway.Utilities.ShowNotification("RealFac " + f.CustomColor);
                }
            }

            GridName = Grid.DisplayName;
            Position = Grid.Physics.CenterOfMassWorld;


            IMyTerminalBlock shieldBlock = null;
            foreach (var g in _connectedGrids)
                if ((shieldBlock = PointCheck.ShApi.GetShieldBlock(g)) != null)
                    break;

            if (shieldBlock != null)
            {
                ShieldStrength = PointCheck.ShApi.GetMaxHpCap(shieldBlock);
                CurrentShieldStrength = PointCheck.ShApi.GetShieldPercent(shieldBlock);
                if (OriginalShieldStrength == -1 && !PointCheck.ShApi.IsFortified(shieldBlock))
                    OriginalShieldStrength = PointCheck.ShApi.GetMaxHpCap(shieldBlock);
                CurrentShieldStrength = PointCheck.ShApi.GetShieldPercent(shieldBlock) *
                                        (OriginalShieldStrength == -1
                                            ? 1
                                            : PointCheck.ShApi.GetMaxHpCap(shieldBlock) /
                                              OriginalShieldStrength);
                ShieldHeat = PointCheck.ShApi.GetShieldHeat(shieldBlock);
            }

            OriginalIntegrity = OriginalIntegrity == -1 ? CurrentIntegrity : OriginalIntegrity;
            OriginalPower = OriginalPower == -1 ? CurrentPower : OriginalPower;
        }

        private void TempBlockCheck(ref bool hasPower, ref bool hasCockpit, ref bool hasThrust, ref bool hasGyro,
            ref float movementBpts, ref float powerBpts, ref float offensiveBpts, ref float miscBpts, ref int bonusBpts,
            ref int pdBpts, ref string controller)
        {
            var hasCtc = false;

            for (var i = 0; i < _connectedGrids.Count; i++)
            {
                var grid = _connectedGrids[i];
                if (grid != null && grid.Physics != null)
                {
                    var subgrid = grid as MyCubeGrid;
                    BlockCount += subgrid.BlocksCount;
                    Pcu += subgrid.BlocksPCU;
                    _tmpBlocks.Clear();
                    grid.GetBlocks(_tmpBlocks);

                    foreach (var block in _tmpBlocks)
                    {
                        var subtype = block.BlockDefinition?.Id.SubtypeName;
                        var id = "";

                        if (block.FatBlock is IMyGasGenerator)
                            id = "H2O2Generator";
                        else if (block.FatBlock is IMyGasTank)
                            id = "HydrogenTank";
                        else if (block.FatBlock is IMyMotorStator && subtype == "SubgridBase")
                            id = "Invincible Subgrid";
                        else if (block.FatBlock is IMyUpgradeModule)
                            switch (subtype)
                            {
                                case "LargeEnhancer":
                                    id = "Shield Enhancer";
                                    break;
                                case "EmitterL":
                                case "EmitterLA":
                                    id = "Shield Emitter";
                                    break;
                                case "LargeShieldModulator":
                                    id = "Shield Modulator";
                                    break;
                                case "DSControlLarge":
                                case "DSControlTable":
                                    id = "Shield Controller";
                                    break;
                                case "AQD_LG_GyroBooster":
                                    id = "Gyro Booster";
                                    break;
                                case "AQD_LG_GyroUpgrade":
                                    id = "Large Gyro Booster";
                                    break;
                            }
                        else if (block.FatBlock is IMyReactor)
                            switch (subtype)
                            {
                                case "LargeBlockLargeGenerator":
                                case "LargeBlockLargeGeneratorWarfare2":
                                    id = "Large Reactor";
                                    break;
                                case "LargeBlockSmallGenerator":
                                case "LargeBlockSmallGeneratorWarfare2":
                                    id = "Small Reactor";
                                    break;
                            }
                        else if (block.FatBlock is IMyGyro)
                            switch (subtype)
                            {
                                case "LargeBlockGyro":
                                    id = "Small Gyro";
                                    break;
                                case "AQD_LG_LargeGyro":
                                    id = "Large Gyro";
                                    break;
                            }
                        else if (block.FatBlock is IMyCameraBlock)
                            switch (subtype)
                            {
                                case "MA_Buster_Camera":
                                    id = "Buster Camera";
                                    break;
                                case "LargeCameraBlock":
                                    id = "Camera";
                                    break;
                            }

                        if (!string.IsNullOrEmpty(id))
                        {
                            if (Sbl.ContainsKey(id))
                                Sbl[id] += 1;
                            else
                                Sbl.Add(id, 1);
                        }

                        if (block.BlockDefinition != null && !string.IsNullOrEmpty(subtype))
                        {
                            if (subtype.IndexOf("Heavy", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                subtype.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0) Heavyblocks++;
                            if (block.FatBlock != null && !(block.FatBlock is IMyMotorRotor) &&
                                !(block.FatBlock is IMyMotorStator) &&
                                subtype != "SC_SRB") CurrentIntegrity += block.Integrity;
                        }

                        if (block.FatBlock is IMyThrust || block.FatBlock is IMyGyro)
                            movementBpts += PointCheck.PointValues.GetValueOrDefault(id, 0);
                        else if (block.FatBlock is IMyReactor || block.FatBlock is IMyBatteryBlock)
                            powerBpts += PointCheck.PointValues.GetValueOrDefault(id, 0);
                        else
                            offensiveBpts += PointCheck.PointValues.GetValueOrDefault(id, 0);
                    }

                    FatHandling(ref hasPower, ref hasCockpit, ref hasThrust, ref hasGyro, ref hasCtc, ref movementBpts,
                        ref powerBpts, ref offensiveBpts, ref miscBpts, ref bonusBpts, ref pdBpts, ref controller,
                        subgrid);
                }
            }

            if (hasCtc)
                foreach (var weapon in SubgridGunL)
                {
                    // Currently takes a global 20% extra cost wich is not multiplicative with clibing cost
                    var bonusWeaponBp =
                        (int)(PointCheck.PointValues.GetValueOrDefault(weapon.Key, 0) * weapon.Value * 0.2);
                    offensiveBpts += bonusWeaponBp;
                    Bpts += bonusWeaponBp;
                }
        }

        private void FatHandling(ref bool hasPower, ref bool hasCockpit, ref bool hasThrust, ref bool hasGyro,
            ref bool hasCtc, ref float movementBpts, ref float powerBpts, ref float offensiveBpts, ref float miscBpts,
            ref int bonusBpts, ref int pdBpts, ref string controller, MyCubeGrid subgrid)
        {
            // Variables used for extra cost on subgrid weapons (rotorturrets)
            var isMainGrid = false;
            var tempGuns = new Dictionary<string, int>();

            var blocklist = subgrid.GetFatBlocks();
            for (var i1 = 0; i1 < blocklist.Count; i1++)
            {
                var block = blocklist[i1];
                var id = block?.BlockDefinition?.Id.SubtypeId.ToString();

                if (!string.IsNullOrEmpty(id))
                {
                    if (PointCheck.PointValues.ContainsKey(id)) Bpts += PointCheck.PointValues[id];
                }
                else
                {
                    if (block is IMyGravityGeneratorBase)
                        Bpts += PointCheck.PointValues.GetValueOrDefault("GravityGenerator", 0);
                    else if (block is IMySmallGatlingGun)
                        Bpts += PointCheck.PointValues.GetValueOrDefault("SmallGatlingGun", 0);
                    else if (block is IMyLargeGatlingTurret)
                        Bpts += PointCheck.PointValues.GetValueOrDefault("LargeGatlingTurret", 0);
                    else if (block is IMySmallMissileLauncher)
                        Bpts += PointCheck.PointValues.GetValueOrDefault("SmallMissileLauncher", 0);
                    else if (block is IMyLargeMissileTurret)
                        Bpts += PointCheck.PointValues.GetValueOrDefault("LargeMissileTurret", 0);
                }

                var isTerminalBlock = block is IMyTerminalBlock;

                if ((PointCheck.PointValues.ContainsKey(id) && !isTerminalBlock) || block is IMyGyro ||
                    block is IMyReactor || block is IMyBatteryBlock || block is IMyCockpit || block is IMyDecoy ||
                    block is IMyShipDrill || block is IMyGravityGeneratorBase || block is IMyShipWelder ||
                    block is IMyShipGrinder || block is IMyRadioAntenna || (block is IMyThrust &&
                                                                            !(block.BlockDefinition.Id.SubtypeName ==
                                                                              "LargeCameraBlock") &&
                                                                            !(block.BlockDefinition.Id.SubtypeName ==
                                                                              "MA_Buster_Camera") &&
                                                                            !(block.BlockDefinition.Id.SubtypeName ==
                                                                              "BlinkDriveLarge")))
                {
                    var typeId = block.BlockDefinition.Id.TypeId.ToString().Replace("MyObjectBuilder_", "");

                    if (Sbl.ContainsKey(typeId))
                        Sbl[typeId] += 1;
                    else if (typeId != "Reactor" && typeId != "Gyro") Sbl.Add(typeId, 1);

                    if (block is IMyThrust)
                    {
                        InstalledThrust += (block as IMyThrust).MaxEffectiveThrust;
                        hasThrust = true;
                    }

                    if (block is IMyCockpit && (block as IMyCockpit).CanControlShip)
                    {
                        if (hasCockpit && !isMainGrid)
                            // Prevent players from placing Cockpits on subgrids to circumvent BP increase of weapons on subgrids
                            MyAPIGateway.Utilities.ShowNotification("Illegal Cockpit placement on subgrid", 1000,
                                "Red");

                        hasCockpit = true;
                        isMainGrid = true;
                    }

                    if (block is IMyReactor || block is IMyBatteryBlock)
                    {
                        hasPower = true;
                        CurrentPower += (block as IMyPowerProducer).MaxOutput;
                    }

                    if (block is IMyGyro)
                    {
                        hasGyro = true;
                        CurrentGyro +=
                            (MyDefinitionManager.Static.GetDefinition((block as IMyGyro).BlockDefinition) as
                                MyGyroDefinition).ForceMagnitude * (block as IMyGyro).GyroStrengthMultiplier;
                    }

                    if (block is IMyCockpit)
                    {
                        var pilot = (block as IMyCockpit).ControllerInfo?.Controller?.ControlledEntity?.Entity;

                        if (pilot is IMyCockpit) controller = (pilot as IMyCockpit).Pilot.DisplayName;
                    }
                }
                else if (PointCheck.PointValues.ContainsKey(id) && isTerminalBlock && !(block is IMyGyro) &&
                         !(block is IMyReactor) && !(block is IMyBatteryBlock) && !(block is IMyCockpit) &&
                         !(block is IMyDecoy) && !(block is IMyShipDrill) && !(block is IMyGravityGeneratorBase) &&
                         !(block is IMyShipWelder) && !(block is IMyShipGrinder) && !(block is IMyThrust) &&
                         !(block is IMyRadioAntenna) && !(block.BlockDefinition.Id.SubtypeName == "BlinkDriveLarge"))
                {
                    var tBlock = block as IMyTerminalBlock;
                    var tN = tBlock.DefinitionDisplayNameText;
                    var mCs = 0f;

                    ClimbingCostRename(ref tN, ref mCs);

                    if (GunL.ContainsKey(tN))
                        GunL[tN] += 1;
                    else
                        GunL.Add(tN, 1);

                    if (mCs > 0 && GunL[tN] > 1)
                    {
                        bonusBpts = (int)(PointCheck.PointValues[id] * ((GunL[tN] - 1) * mCs));
                        Bpts += bonusBpts;
                    }
                }

                bool isPointDefense;
                bool isWeapon;
                if (PointCheckHelpers.WeaponsDictionary.TryGetValue(block.BlockDefinition.Id.SubtypeName,
                        out isWeapon) && isWeapon)
                {
                    offensiveBpts += PointCheck.PointValues.GetValueOrDefault(id, 0) + bonusBpts;

                    if (tempGuns.ContainsKey(id))
                        tempGuns[id] += 1;
                    else
                        tempGuns.Add(id, 1);

                    // isPointDefense;
                    if (PointCheckHelpers.PdDictionary.TryGetValue(block.BlockDefinition.Id.SubtypeName,
                            out isPointDefense) &&
                        isPointDefense) pdBpts += PointCheck.PointValues.GetValueOrDefault(id, 0);
                }
                else
                {
                    var blockType = block.BlockDefinition.Id.SubtypeName;

                    if (block is IMyThrust || block is IMyGyro || blockType == "BlinkDriveLarge" ||
                        blockType.Contains("Afterburner"))
                        movementBpts += PointCheck.PointValues.GetValueOrDefault(id, 0);
                    else if (block is IMyReactor || block is IMyBatteryBlock)
                        powerBpts += PointCheck.PointValues.GetValueOrDefault(id, 0);
                    else
                        miscBpts += PointCheck.PointValues.GetValueOrDefault(id, 0);
                }

                if (id == "LargeTurretControlBlock") hasCtc = true;
            }

            // Adding extra points to guns when they are not on the main grid
            if (!isMainGrid && _connectedGrids.Count != 1)
                foreach (var weapon in tempGuns)
                    if (SubgridGunL.ContainsKey(weapon.Key))
                        SubgridGunL[weapon.Key] += 1;
                    else
                        SubgridGunL.Add(weapon.Key, 1);
        }

        private static void ClimbingCostRename(ref string costGroupName, ref float costMultiplier)
        {
            switch (costGroupName)
            {
                case "Blink Drive Large":
                    costGroupName = "Blink Drive";
                    costMultiplier = 0.15f;
                    break;
                case "Project Pluto (SLAM)":
                case "SLAM":
                    costGroupName = "SLAM";
                    costMultiplier = 0.25f;
                    break;
                case "[BTI] MRM-10 Modular Launcher 45":
                case "[BTI] MRM-10 Modular Launcher 45 Reversed":
                case "[BTI] MRM-10 Modular Launcher":
                case "[BTI] MRM-10 Modular Launcher Middle":
                case "[BTI] MRM-10 Launcher":
                    costGroupName = "MRM-10 Launcher";
                    costMultiplier = 0.04f;
                    break;
                case "[BTI] LRM-5 Modular Launcher 45 Reversed":
                case "[BTI] LRM-5 Modular Launcher 45":
                case "[BTI] LRM-5 Modular Launcher Middle":
                case "[BTI] LRM-5 Modular Launcher":
                case "[BTI] LRM-5 Launcher":
                    costGroupName = "LRM-5 Launcher";
                    costMultiplier = 0.10f;
                    break;
                case "[MA] Gimbal Laser T2 Armored":
                case "[MA] Gimbal Laser T2 Armored Slope 45":
                case "[MA] Gimbal Laser T2 Armored Slope 2":
                case "[MA] Gimbal Laser T2 Armored Slope":
                case "[MA] Gimbal Laser T2":
                    costGroupName = "Gimbal Laser T2";
                    costMultiplier = 0f;
                    break;
                case "[MA] Gimbal Laser Armored Slope 45":
                case "[MA] Gimbal Laser Armored Slope 2":
                case "[MA] Gimbal Laser Armored Slope":
                case "[MA] Gimbal Laser Armored":
                case "[MA] Gimbal Laser":
                    costGroupName = "Gimbal Laser";
                    costMultiplier = 0f;
                    break;
                case "[ONYX] BR-RT7 Afflictor Slanted Burst Cannon":
                case "[ONYX] BR-RT7 Afflictor 70mm Burst Cannon":
                case "[ONYX] Afflictor":
                    costGroupName = "Afflictor";
                    costMultiplier = 0f;
                    break;
                case "[MA] Slinger AC 150mm Sloped 30":
                case "[MA] Slinger AC 150mm Sloped 45":
                case "[MA] Slinger AC 150mm Gantry Style":
                case "[MA] Slinger AC 150mm Sloped 45 Gantry":
                case "[MA] Slinger AC 150mm":
                case "[MA] Slinger":
                    costGroupName = "Slinger";
                    costMultiplier = 0f;
                    break;
                case "[ONYX] Heliod Plasma Pulser":
                    costGroupName = "Heliod Plasma Pulser";
                    costMultiplier = 0.50f;
                    break;
                case "[MA] UNN Heavy Torpedo Launcher":
                    costGroupName = "UNN Heavy Torpedo Launcher";
                    costMultiplier = 0.15f;
                    break;
                case "[BTI] SRM-8":
                    costGroupName = "SRM-8";
                    costMultiplier = 0.15f;
                    break;
                case "[BTI] Starcore Arrow-IV Launcher":
                    costGroupName = "Starcore Arrow-IV Launcher";
                    costMultiplier = 0.15f;
                    break;
                case "[HAS] Tartarus VIII":
                    costGroupName = "Tartarus VIII";
                    costMultiplier = 0.15f;
                    break;
                case "[HAS] Cocytus IX":
                    costGroupName = "Cocytus IX";
                    costMultiplier = 0.15f;
                    break;
                case "[MA] MCRN Torpedo Launcher":
                    costGroupName = "MCRN Torpedo Launcher";
                    costMultiplier = 0.15f;
                    break;
                case "Flares":
                    costGroupName = "Flares";
                    costMultiplier = 0.25f;
                    break;
                case "[EXO] Chiasm [Arc Emitter]":
                    costGroupName = "Chiasm [Arc Emitter]";
                    costMultiplier = 0.15f;
                    break;
                case "[BTI] Medium Laser":
                case "[BTI] Large Laser":
                    costGroupName = " Laser";
                    costMultiplier = 0.15f;
                    break;
                case "Reinforced Blastplate":
                case "Active Blastplate":
                case "Standard Blastplate A":
                case "Standard Blastplate B":
                case "Standard Blastplate C":
                case "Elongated Blastplate":
                case "7x7 Basedplate":
                    costGroupName = "Blastplate";
                    costMultiplier = 1.00f;
                    break;
                case "[EXO] Taiidan":
                case "[EXO] Taiidan Fighter Launch Rail":
                case "[EXO] Taiidan Bomber Launch Rail":
                case "[EXO] Taiidan Fighter Hangar Bay":
                case "[EXO] Taiidan Bomber Hangar Bay":
                case "[EXO] Taiidan Bomber Hangar Bay Medium":
                case "[EXO] Taiidan Fighter Small Bay":
                    costGroupName = "Taiidan";
                    costMultiplier = 0.25f;
                    break;
                case "[40K] Gothic Torpedo Launcher":
                    costGroupName = "Gothic Torpedo Launcher";
                    costMultiplier = 0.15f;
                    break;
                case "[MID] AX 'Spitfire' Light Rocket Turret":
                    costGroupName = "Spitfire Turret";
                    costMultiplier = 0.15f;
                    break;
                case "[FLAW] Naval RL-10x 'Avalanche' Medium Range Launchers":
                case "[FLAW] Naval RL-10x 'Avalanche' Angled Medium Range Launchers":
                    costGroupName = "RL-10x Avalanche";
                    costMultiplier = 0.15f;
                    break;
                case "[MID] LK 'Bonfire' Guided Rocket Turret":
                    costGroupName = "Bonfire Turret";
                    costMultiplier = 0.2f;
                    break;
                case "[FLAW] Warp Beacon - Longsword":
                    costGroupName = "Longsword Bomber";
                    costMultiplier = 0.2f;
                    break;
                case "[FLAW] Phoenix Snubfighter Launch Bay":
                    costGroupName = "Snubfighters";
                    costMultiplier = 0.1f;
                    break;
                case "[FLAW] Hadean Superheavy Plasma Blastguns":
                    costGroupName = "Plasma Blastgun";
                    costMultiplier = 0.121f;
                    break;
                case "[FLAW] Vindicator Kinetic Battery":
                    costGroupName = "Kinetic Battery";
                    costMultiplier = 0.120f;
                    break;
                case "[FLAW] Goalkeeper Casemate Flak Battery":
                    costGroupName = "Goalkeeper Flakwall";
                    costMultiplier = 0.119f;
                    break;
                case "Shield Controller":
                case "Shield Controller Table":
                case "Structural Integrity Field Generator":
                    costGroupName = "Defensive Generator";
                    costMultiplier = 50.00f;
                    break;
            }
        }


        private static Vector3 ColorMaskToRgb(Vector3 colorMask)
        {
            return MyColorPickerConstants.HSVOffsetToHSV(colorMask).HSVtoColor();
        }

        public void CreateHud()
        {
            _nametag = new HudAPIv2.HUDMessage(new StringBuilder(OwnerName), Vector2D.Zero, font: "BI_SEOutlined",
                blend: BlendTypeEnum.PostPP, hideHud: false, shadowing: true);
            UpdateHud();
        }

        public void UpdateHud()
        {
            try
            {
                if (_nametag == null)
                    return;

                _nametag.Message.Clear();
                var camera = MyAPIGateway.Session.Camera;
                const int distanceThreshold = 20000;
                const int maxAngle = 60; // Adjust this angle as needed

                if (_nametag != null)
                {
                    var e = MyEntities.GetEntityById(GridId);
                    Vector3D pos;

                    if (e != null && e is IMyCubeGrid)
                    {
                        var g = e as IMyCubeGrid;
                        pos = g.Physics.CenterOfMassWorld;
                    }
                    else
                    {
                        pos = Position;
                    }

                    var targetHudPos = camera.WorldToScreen(ref pos);
                    var newOrigin = new Vector2D(targetHudPos.X, targetHudPos.Y);

                    _nametag.InitialColor = new Color(FactionColor);
                    var cameraForward = camera.WorldMatrix.Forward;
                    var toTarget = pos - camera.WorldMatrix.Translation;
                    var fov = camera.FieldOfViewAngle;
                    var angle = GetAngleBetweenDegree(toTarget, cameraForward);

                    var stealthed = ((uint)e.Flags & 0x1000000) > 0;
                    var visible = !(newOrigin.X > 1 || newOrigin.X < -1 || newOrigin.Y > 1 || newOrigin.Y < -1) &&
                                  angle <= fov && !stealthed;

                    var distance = Vector3D.Distance(camera.WorldMatrix.Translation, pos);
                    _nametag.Scale = 1 - MathHelper.Clamp(distance / distanceThreshold, 0, 1) +
                                     30 / Math.Max(maxAngle, angle * angle * angle);
                    _nametag.Origin = new Vector2D(targetHudPos.X,
                        targetHudPos.Y + MathHelper.Clamp(-0.000125 * distance + 0.25, 0.05, 0.25));
                    _nametag.Visible = PointCheckHelpers.NameplateVisible && visible;
                    _nametag.Message.Clear();

                    if (IsFunctional)
                    {
                        var nameText = PointCheck.Viewstat == 0 || PointCheck.Viewstat == 2 ? OwnerName : GridName;
                        _nametag.Message.Append(nameText);

                        if (PointCheck.Viewstat == 2) _nametag.Message.Append("\n" + GridName);
                    }
                    else
                    {
                        var nameText = PointCheck.Viewstat == 0 || PointCheck.Viewstat == 2
                            ? OwnerName + "<color=white>:[Dead]"
                            : GridName + "<color=white>:[Dead]";
                        _nametag.Message.Append(nameText);

                        if (PointCheck.Viewstat == 2)
                            _nametag.Message.Append("\n" + GridName + "<color=white>:[Dead]");
                    }

                    _nametag.Offset = -_nametag.GetTextLength() / 2;
                }
            }
            catch (Exception)
            {
                // Handle exceptions here, or consider logging them.
            }
        }

        private double GetAngleBetweenDegree(Vector3D vectorA, Vector3D vectorB)
        {
            vectorA.Normalize();
            vectorB.Normalize();
            return Math.Acos(MathHelper.Clamp(vectorA.Dot(vectorB), -1, 1)) * (180.0 / Math.PI);
        }

        public void DisposeHud()
        {
            if (_nametag != null)
            {
                _nametag.Visible = false;
                _nametag.Message.Clear();
                _nametag.DeleteMessage();
            }

            _nametag = null;
        }

        private void Reset()
        {
            Sbl.Clear();
            GunL.Clear();
            SubgridGunL.Clear();
            Bpts = 0;
            InstalledThrust = 0;
            Mass = 0;
            Heavyblocks = 0;
            BlockCount = 0;
            ShieldStrength = 0;
            CurrentShieldStrength = 0;
            CurrentIntegrity = 0;
            CurrentPower = 0;
            Pcu = 0;
            //DPS = 0;
        }
    }
}