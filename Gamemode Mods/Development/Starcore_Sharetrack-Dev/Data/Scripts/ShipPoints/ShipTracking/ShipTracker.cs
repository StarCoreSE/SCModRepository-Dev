using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DefenseShields;
using Draygo.API;
using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SCModRepository_Dev.Gamemode_Mods.Development.Starcore_Sharetrack_Dev.Data.Scripts.ShipPoints;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace klime.PointCheck
{
    public class ShipTracker
    {
        public IMyCubeGrid Grid { get; private set; }
        public IMyPlayer Owner { get; private set; }
        public long OwnerId { get; private set; }
        public IMyCharacter Pilot { get; private set; }
        public bool IsFunctional { get; private set; } = false;




        public string GridName => Grid.DisplayName;
        public float Mass => ((MyCubeGrid)Grid).GetCurrentMass();
        public Vector3 Position => Grid.Physics.CenterOfMassWorld;
        public IMyFaction OwnerFaction => MyAPIGateway.Session?.Factions?.TryGetPlayerFaction(OwnerId);
        public Vector3 FactionColor => ColorMaskToRgb(OwnerFaction?.CustomColor ?? Vector3.Zero);
        public string OwnerName => Pilot?.DisplayName ?? Owner?.DisplayName ?? "Unowned";

        #region GridStats Pointers

        #region Global Stats

        public int BlockCount
        {
            get
            {
                int total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.BlockCount;
                return total;
            }
        }
        public int HeavyArmorCount
        {
            get
            {
                int total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.HeavyArmorCount;
                return total;
            }
        }
        public int PCU
        {
            get
            {
                int total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.PCU;
                return total;
            }
        }
        public float TotalThrust
        {
            get
            {
                float total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.TotalThrust;
                return total;
            }
        }
        public float TotalTorque
        {
            get
            {
                float total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.TotalTorque;
                return total;
            }
        }
        public float TotalPower
        {
            get
            {
                float total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.TotalPower;
                return total;
            }
        }
        public Dictionary<string, int> SpecialBlockCounts
        {
            get
            {
                Dictionary<string, int> blockCounts = new Dictionary<string, int>();
                foreach (var stats in _gridStats.Values)
                {
                    foreach (var key in stats.SpecialBlockCounts.Keys)
                    {
                        if (!blockCounts.ContainsKey(key))
                            blockCounts.Add(key, 0);
                        blockCounts[key] += stats.SpecialBlockCounts[key];
                    }
                }

                return blockCounts;
            }
        }

        #endregion

        #region BattlePoint Stats
        public int BattlePoints
        {
            get
            {
                int total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.BattlePoints;
                return total;
            }
        }
        public int OffensivePoints
        {
            get
            {
                int total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.OffensivePoints;
                return total;
            }
        }
        public int PowerPoints
        {
            get
            {
                int total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.PowerPoints;
                return total;
            }
        }
        public int MovementPoints
        {
            get
            {
                int total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.MovementPoints;
                return total;
            }
        }
        public int PointDefensePoints
        {
            get
            {
                int total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.PointDefensePoints;
                return total;
            }
        }

        public int RemainingPoints => BattlePoints - OffensivePoints - PowerPoints - MovementPoints - PointDefensePoints;

        #endregion

        #region Shield Stats

        public float OriginalMaxShieldHealth
        {
            get
            {
                float total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.OriginalMaxShieldHealth;
                return total;
            }
        }
        public float MaxShieldHealth
        {
            get
            {
                float total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.MaxShieldHealth;
                return total;
            }
        }
        public float CurrentShieldPercent
        {
            get
            {
                float total = 0;
                foreach (var stats in _gridStats.Values)
                    total += stats.CurrentShieldPercent;
                return total;
            }
        }

        #endregion

        #region Weapon Stats 

        public Dictionary<string, int> WeaponCounts
        {
            get
            {
                Dictionary<string, int> blockCounts = new Dictionary<string, int>();
                foreach (var stats in _gridStats.Values)
                {
                    foreach (var key in stats.WeaponCounts.Keys)
                    {
                        if (!blockCounts.ContainsKey(key))
                            blockCounts.Add(key, 0);
                        blockCounts[key] += stats.WeaponCounts[key];
                    }
                }

                return blockCounts;
            }
        }

        #endregion

        #endregion




        private HudAPIv2.HUDMessage _nametag;

        private readonly Dictionary<IMyCubeGrid, GridStats> _gridStats = new Dictionary<IMyCubeGrid, GridStats>();


        private ShipTracker()
        {
        }

        public ShipTracker(IMyCubeGrid grid, bool showOnHud = true)
        {
            Grid = grid;

            Update();

            if (!showOnHud)
                return;

            Grid.OnClose += OnClose;
            Grid.GetGridGroup(GridLinkTypeEnum.Physical).OnGridAdded += OnGridAdd;
            Grid.GetGridGroup(GridLinkTypeEnum.Physical).OnGridRemoved += OnGridRemove;

            _nametag = new HudAPIv2.HUDMessage(new StringBuilder(OwnerName), Vector2D.Zero, font: "BI_SEOutlined",
                blend: BlendTypeEnum.PostPP, hideHud: false, shadowing: true);
            UpdateHud();
        }

        public void OnClose(IMyEntity e)
        {
            Grid.OnClose -= OnClose;
            Grid.GetGridGroup(GridLinkTypeEnum.Physical).OnGridAdded -= OnGridAdd;
            Grid.GetGridGroup(GridLinkTypeEnum.Physical).OnGridRemoved -= OnGridRemove;

            if (MyAPIGateway.Session.IsServer)
            {
                TrackingManager.I.TrackedGrids.Remove(Grid);
                DisposeHud();
            }

            e.OnClose -= OnClose;
        }

        public void Update()
        {
            if (Grid?.Physics == null) // TODO transfer to a different grid
                return;

            // TODO: Update pilots
        }

        private void OnGridAdd(IMyGridGroupData groupData, IMyCubeGrid grid, IMyGridGroupData previousGroupData)
        {
            if (_gridStats.ContainsKey(grid))
                return;
            _gridStats.Add(grid, new GridStats(grid));
            _gridStats[grid].Update();
        }

        private void OnGridRemove(IMyGridGroupData groupData, IMyCubeGrid grid, IMyGridGroupData newGroupData)
        {
            if (!_gridStats.ContainsKey(grid))
                return;
            _gridStats[grid].Close();
            _gridStats.Remove(grid);
        }

        public static void ClimbingCostRename(ref string costGroupName, ref float costMultiplier)
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

        /// <summary>
        /// Updates the nametag display.
        /// </summary>
        public void UpdateHud()
        {
            if (_nametag == null)
                return;

            try
            {
                var camera = MyAPIGateway.Session.Camera;
                const int distanceThreshold = 20000;
                const int maxAngle = 60; // Adjust this angle as needed

                Vector3D gridPosition = Position;

                var targetHudPos = camera.WorldToScreen(ref gridPosition);
                var newOrigin = new Vector2D(targetHudPos.X, targetHudPos.Y);

                _nametag.InitialColor = new Color(FactionColor);
                var fov = camera.FieldOfViewAngle;
                var angle = GetAngleBetweenDegree(gridPosition - camera.WorldMatrix.Translation, camera.WorldMatrix.Forward);

                var stealthed = ((uint)Grid.Flags & 0x1000000) > 0;
                var visible = !(newOrigin.X > 1 || newOrigin.X < -1 || newOrigin.Y > 1 || newOrigin.Y < -1) &&
                              angle <= fov && !stealthed;

                var distance = Vector3D.Distance(camera.WorldMatrix.Translation, gridPosition);
                _nametag.Scale = 1 - MathHelper.Clamp(distance / distanceThreshold, 0, 1) +
                                 30 / Math.Max(maxAngle, angle * angle * angle);
                _nametag.Origin = new Vector2D(targetHudPos.X,
                    targetHudPos.Y + MathHelper.Clamp(-0.000125 * distance + 0.25, 0.05, 0.25));
                _nametag.Visible = PointCheckHelpers.NameplateVisible && visible;

                _nametag.Message.Clear();

                string nameTagText = "";

                if ((PointCheck.NametagViewState & NametagSettings.PlayerName) > 0)
                    nameTagText += OwnerName;
                if ((PointCheck.NametagViewState & NametagSettings.GridName) > 0)
                    nameTagText += "\n" + GridName;
                if (!IsFunctional)
                    nameTagText += "<color=white>:[Dead]";

                _nametag.Message.Append(nameTagText.TrimStart('\n'));
                _nametag.Offset = -_nametag.GetTextLength() / 2;
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

        [Flags]
        public enum NametagSettings
        {
            None = 0,
            PlayerName = 1,
            GridName = 2,
        }
    }
}