using CoreSystems.Api;
using DefenseShields;
using Draygo.API;
using klime.PointCheck;
using RelativeTopSpeed;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using SCModRepository.Gamemode_Mods.Stable.Starcore_Sharetrack.Data.Scripts.ShipPoints.MatchTimer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static VRageRender.MyBillboard;

namespace SCModRepository_Dev.Gamemode_Mods.Development.Starcore_Sharetrack_Dev.Data.Scripts.ShipPoints
{
    internal class HudPointsList
    {
        #region APIs
        private WcApi WcApi => PointCheck.I.WcApi;
        private ShieldApi ShApi => PointCheck.I.ShApi;
        private RtsApi RtsApi => PointCheck.I.RtsApi;
        private HudAPIv2 TextHudApi => PointCheck.I.TextHudApi;
        #endregion

        private ViewState _viewState = ViewState.None;
        private enum ViewState
        {
            None,
            InView,
            InView2,
        }


        private readonly HudAPIv2.HUDMessage
            _statMessage = new HudAPIv2.HUDMessage(scale: 1f, font: "BI_SEOutlined", Message: new StringBuilder(""),
                origin: new Vector2D(-.99, .99), hideHud: false, blend: BlendTypeEnum.PostPP)
            {
                Visible = false,
                InitialColor = Color.Orange
            },
            _statMessageBattleGunlist = new HudAPIv2.HUDMessage(scale: 1.25f, font: "BI_SEOutlined",
                Message: new StringBuilder(""), origin: new Vector2D(-.99, .99), hideHud: false, shadowing: true,
                blend: BlendTypeEnum.PostPP)
            {
                Visible = false
            },
            _statMessageBattle = new HudAPIv2.HUDMessage(scale: 1.25f, font: "BI_SEOutlined",
                Message: new StringBuilder(""), origin: new Vector2D(-.54, -0.955), hideHud: false,
                blend: BlendTypeEnum.PostPP)
            {
                Visible = false
            };

        private readonly StringBuilder _gunTextBuilder = new StringBuilder();
        private readonly StringBuilder _speedTextBuilder = new StringBuilder();

        #region Public Methods

        public void CycleViewState()
        {
            _viewState++;
            if (_viewState > ViewState.InView2)
            {
                _statMessageBattle.Message.Clear();
                _statMessageBattleGunlist.Message.Clear();
                _statMessageBattle.Visible = false;
                _statMessageBattleGunlist.Visible = false;

                _viewState = ViewState.None;
            }
        }

        public void UpdateDraw()
        {
            if (!TextHudApi.Heartbeat)
                return;

            switch (_viewState)
            {
                case ViewState.InView:
                    ShiftTHandling();
                    break;
                case ViewState.InView2:
                    BattleShiftTHandling();
                    break;
            }
        }

        #endregion

        private static IMyCubeGrid GetFocusedGrid()
        {
            var cockpit = MyAPIGateway.Session.ControlledObject?.Entity as IMyCockpit;
            if (cockpit == null || MyAPIGateway.Session.IsCameraUserControlledSpectator)
            {
                return PointCheck.RaycastGridFromCamera();
            }
            else if (cockpit.CubeGrid?.Physics != null) // user is in cockpit
            {
                return cockpit.CubeGrid;
            }

            return null;
        }

        private void ShiftTHandling()
        {
            IMyCubeGrid focusedGrid = GetFocusedGrid();
            if (focusedGrid != null) 
                ShiftTCals(focusedGrid);
            else if(_statMessage.Visible)
            {
                _statMessage.Message.Clear();
                _statMessage.Visible = false;
            }
        }

        private void BattleShiftTHandling()
        {
            if (_statMessage.Visible)
            {
                _statMessage.Message.Clear();
                _statMessage.Visible = false;
            }

            IMyCubeGrid focusedGrid = GetFocusedGrid();
            if (focusedGrid != null)
                BattleShiftTCalcs(focusedGrid);
            else if (_statMessageBattle.Visible)
            {
                _statMessageBattle.Message.Clear();
                _statMessageBattle.Visible = false;
                _statMessageBattleGunlist.Message.Clear();
                _statMessageBattleGunlist.Visible = false;
            }
        }

        private void ShiftTCals(IMyCubeGrid focusedGrid)
        {
            // Update once per second
            if (MatchTimer.I.Ticks % 60 != 0)
                return;

            ShipTracker shipTracker;
            TrackingManager.I.TrackedGrids.TryGetValue(focusedGrid, out shipTracker);
            if (shipTracker == null)
                return;

            var totalShieldString = "None";

            if (shipTracker.ShieldStrength > 100)
                totalShieldString = $"{shipTracker.ShieldStrength / 100f:F2} M";
            else if (shipTracker.ShieldStrength > 1 && shipTracker.ShieldStrength < 100)
                totalShieldString = $"{shipTracker.ShieldStrength:F0}0 K";

            var gunTextBuilder = new StringBuilder();
            foreach (var x in shipTracker.GunL.Keys)
                gunTextBuilder.AppendFormat("<color=Green>{0}<color=White> x {1}\n", shipTracker.GunL[x], x);
            var gunText = gunTextBuilder.ToString();

            var specialBlockTextBuilder = new StringBuilder();
            foreach (var x in shipTracker.Sbl.Keys)
                specialBlockTextBuilder.AppendFormat("<color=Green>{0}<color=White> x {1}\n", shipTracker.Sbl[x], x);
            var specialBlockText = specialBlockTextBuilder.ToString();

            var massString = $"{shipTracker.Mass}";

            var thrustInKilograms = focusedGrid.GetMaxThrustInDirection(Base6Directions.Direction.Backward) / 9.81f;
            //float weight = trkd.Mass;
            var mass = shipTracker.Mass;
            var twr = (float)Math.Round(thrustInKilograms / mass, 1);

            if (shipTracker.Mass > 1000000)
            {
                massString = $"{Math.Round(shipTracker.Mass / 1000000f, 1):F2}m";
            }

            var twRs = $"{twr:F3}";
            var thrustString = $"{Math.Round(shipTracker.InstalledThrust, 1)}";

            if (shipTracker.InstalledThrust > 1000000)
                thrustString = $"{Math.Round(shipTracker.InstalledThrust / 1000000f, 1):F2}M";

            var playerName = shipTracker.Owner == null ? shipTracker.GridName : shipTracker.Owner.DisplayName;
            var factionName = shipTracker.Owner == null
                ? ""
                : MyAPIGateway.Session?.Factions?.TryGetPlayerFaction(shipTracker.OwnerId)?.Name;

            var speed = focusedGrid.GridSizeEnum == MyCubeSize.Large
                ? MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed
                : MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
            var reducedAngularSpeed = 0f;

            if (RtsApi != null && RtsApi.IsReady)
            {
                speed = (float)Math.Round(RtsApi.GetMaxSpeed(focusedGrid), 2);
                reducedAngularSpeed = RtsApi.GetReducedAngularSpeed(focusedGrid);
            }


            var pwrNotation = shipTracker.CurrentPower > 1000 ? "GW" : "MW";
            var tempPwr = shipTracker.CurrentPower > 1000
                ? $"{Math.Round(shipTracker.CurrentPower / 1000, 1):F1}"
                : Math.Round(shipTracker.CurrentPower, 1).ToString();
            var pwr = tempPwr + pwrNotation;

            var gyroString = $"{Math.Round(shipTracker.CurrentGyro, 1)}";

            if (shipTracker.CurrentGyro >= 1000000)
            {
                double tempGyro2 = Math.Round(shipTracker.CurrentGyro / 1000000f, 1);
                gyroString = tempGyro2 > 1000 ? $"{Math.Round(tempGyro2 / 1000, 1):F1}G" : $"{Math.Round(tempGyro2, 1):F1}M";
            }


            var sb = new StringBuilder();

            // Basic Info
            sb.AppendLine("----Basic Info----");
            sb.AppendFormat("<color=White>{0} ", focusedGrid.DisplayName);
            sb.AppendFormat("<color=Green>Owner<color=White>: {0} ", playerName);
            sb.AppendFormat("<color=Green>Faction<color=White>: {0}\n", factionName);
            sb.AppendFormat("<color=Green>Mass<color=White>: {0} kg\n", massString);
            sb.AppendFormat("<color=Green>Heavy blocks<color=White>: {0}\n", shipTracker.Heavyblocks);
            sb.AppendFormat("<color=Green>Total blocks<color=White>: {0}\n", shipTracker.BlockCount);
            sb.AppendFormat("<color=Green>PCU<color=White>: {0}\n", shipTracker.Pcu);
            sb.AppendFormat("<color=Green>Size<color=White>: {0}\n",
                (focusedGrid.Max + Vector3.Abs(focusedGrid.Min)).ToString());
            // sb.AppendFormat("<color=Green>Max Speed<color=White>: {0} | <color=Green>TWR<color=White>: {1}\n", speed, TWRs);
            sb.AppendFormat(
                "<color=Green>Max Speed<color=White>: {0} | <color=Green>Reduced Angular Speed<color=White>: {1:F2} | <color=Green>TWR<color=White>: {2}\n",
                speed, reducedAngularSpeed, twRs);
            sb.AppendLine(); //blank line

            // Battle Stats
            sb.AppendLine("<color=Orange>----Battle Stats----");
            sb.AppendFormat("<color=Green>Battle Points<color=White>: {0}\n", shipTracker.Bpts);
            sb.AppendFormat(
                "<color=Orange>[<color=Red> {0}% <color=Orange>| <color=Green>{1}% <color=Orange>| <color=DeepSkyBlue>{2}% <color=Orange>| <color=LightGray>{3}% <color=Orange>]\n",
                shipTracker.OffensivePercentage, shipTracker.PowerPercentage, shipTracker.MovementPercentage, shipTracker.MiscPercentage);
            sb.Append(
                $"<color=Green>PD Investment<color=White>: <color=Orange>( <color=white>{shipTracker.PdInvest}% <color=Orange>|<color=Crimson> {shipTracker.PdPercentage}%<color=Orange> )\n");
            sb.AppendFormat(
                "<color=Green>Shield Max HP<color=White>: {0} <color=Orange>(<color=White>{1}%<color=Orange>)\n",
                totalShieldString, (int)shipTracker.CurrentShieldStrength);
            sb.AppendFormat("<color=Green>Thrust<color=White>: {0}N\n", thrustString);
            sb.AppendFormat("<color=Green>Gyro<color=White>: {0}N\n", gyroString);
            sb.AppendFormat("<color=Green>Power<color=White>: {0}\n", pwr);
            sb.AppendLine(); //blank line
            // Blocks Info
            sb.AppendLine("<color=Orange>----Blocks----");
            sb.AppendLine(specialBlockText);
            sb.AppendLine(); //blank line
            // Armament Info
            sb.AppendLine("<color=Orange>----Armament----");
            sb.Append(gunText);

            _statMessage.Message = sb;
            _statMessage.Visible = true;
        }

        private void BattleShiftTCalcs(IMyCubeGrid focusedGrid)
        {
            if (MatchTimer.I.Ticks % 60 != 0)
                return;

            var tracked = new ShipTracker(focusedGrid);
            var totalShield = tracked.ShieldStrength;
            var totalShieldString = totalShield > 100
                ? $"{Math.Round(totalShield / 100f, 2):F2} M"
                : totalShield > 1
                    ? $"{Math.Round(totalShield, 0):F0}0 K"
                    : "None";

            var maxSpeed = focusedGrid.GridSizeEnum == MyCubeSize.Large
                ? MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed
                : MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
            var reducedAngularSpeed = 0f;
            var negativeInfluence = 0f;

            if (RtsApi != null && RtsApi.IsReady)
            {
                maxSpeed = (float)Math.Round(RtsApi.GetMaxSpeed(focusedGrid), 2);
                reducedAngularSpeed = RtsApi.GetReducedAngularSpeed(focusedGrid);
                negativeInfluence = RtsApi.GetNegativeInfluence(focusedGrid);
            }

            _speedTextBuilder.Clear();
            _speedTextBuilder.Append($"\n<color=Green>Max Speed<color=White>: {maxSpeed:F2} m/s");
            _speedTextBuilder.Append(
                $"\n<color=Green>Reduced Angular Speed<color=White>: {reducedAngularSpeed:F2} rad/s");
            _speedTextBuilder.Append($"\n<color=Green>Negative Influence<color=White>: {negativeInfluence:F2}");

            _gunTextBuilder.Clear();
            foreach (var x in tracked.GunL)
                _gunTextBuilder.Append($"<color=Green>{x.Value} x <color=White>{x.Key}\n");

            var thrustString = $"{Math.Round(tracked.InstalledThrust, 1)}";
            if (tracked.InstalledThrust > 1000000)
                thrustString = $"{Math.Round(tracked.InstalledThrust / 1000000f, 1):F2}M";

            var gyroString = $"{Math.Round(tracked.CurrentGyro, 1)}";
            double tempGyro2;
            if (tracked.CurrentGyro >= 1000000)
            {
                tempGyro2 = Math.Round(tracked.CurrentGyro / 1000000f, 1);
                if (tempGyro2 > 1000)
                    gyroString = $"{Math.Round(tempGyro2 / 1000, 1):F1}G";
                else
                    gyroString = $"{Math.Round(tempGyro2, 1):F1}M";
            }

            var pwrNotation = tracked.CurrentPower > 1000 ? "GW" : "MW";
            var tempPwr = tracked.CurrentPower > 1000
                ? $"{Math.Round(tracked.CurrentPower / 1000, 1):F1}"
                : Math.Round(tracked.CurrentPower, 1).ToString();
            var pwr = tempPwr + pwrNotation;

            _gunTextBuilder.Append($"\n<color=Green>Thrust<color=White>: {thrustString} N")
                .Append($"\n<color=Green>Gyro<color=White>: {gyroString} N")
                .Append($"\n<color=Green>Power<color=White>: {pwr}")
                .Append(_speedTextBuilder);

            _statMessageBattleGunlist.Message.Length = 0;
            _statMessageBattleGunlist.Message.Append(_gunTextBuilder);

            _statMessageBattle.Message.Length = 0;
            _statMessageBattle.Message.Append(string.Format("<color=White>{0} ({1}%)", totalShieldString,
                (int)tracked.CurrentShieldStrength));

            _statMessageBattle.Visible = true;
            _statMessageBattleGunlist.Visible = true;
        }
    }
}
