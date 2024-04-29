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
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using static klime.PointCheck.PointCheck;
using static VRageRender.MyBillboard;

namespace SCModRepository_Dev.Gamemode_Mods.Development.Starcore_Sharetrack_Dev.Data.Scripts.ShipPoints
{
    internal class HudPointsList
    {
        #region APIs
        private WcApi WcApi => PointCheck.WcApi;
        private ShieldApi ShApi => PointCheck.ShApi;
        private RtsApi RtsApi => PointCheck.RtsApi;
        private HudAPIv2 TextHudApi => PointCheck.TextHudApi;
        #endregion

        private ViewState _vState = ViewState.None;
        private enum ViewState
        {
            None,
            InView,
            InView2,
            ExitView
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
            _vState++;
            if (_vState > ViewState.ExitView)
                _vState = ViewState.None;

            if (_vState == ViewState.ExitView)
            {
                if (_statMessageBattle != null && PointCheck.TextHudApi.Heartbeat)
                    if (_statMessageBattle.Visible)
                    {
                        _statMessageBattle.Message.Clear();
                        _statMessageBattleGunlist.Message.Clear();
                        _statMessageBattle.Visible = false;
                        _statMessageBattleGunlist.Visible = false;
                    }

                _vState = ViewState.None;
            }
        }

        public void UpdateDraw()
        {
            ShiftTHandling();
            BattleShiftTHandling();
        }

        #endregion



        private void ShiftTHandling()
        {
            if (_vState != ViewState.InView || _statMessage == null || !TextHudApi.Heartbeat)
                return; //shift T menu

            var cockpit = MyAPIGateway.Session.ControlledObject?.Entity as IMyCockpit;
            if (cockpit == null || MyAPIGateway.Session.IsCameraUserControlledSpectator)
            {
                //user is not in cockpit

                var camMat = MyAPIGateway.Session.Camera.WorldMatrix;
                IHitInfo hits;
                MyAPIGateway.Physics.CastRay(camMat.Translation + camMat.Forward * 0.5,
                    camMat.Translation + camMat.Forward * 500, out hits);
                if (hits?.HitEntity is IMyCubeGrid)
                {
                    var icubeG = hits.HitEntity as IMyCubeGrid;
                    if (icubeG?.Physics != null) ShiftTCals(icubeG);
                }
                else
                {
                    if (_statMessage != null && TextHudApi.Heartbeat && _statMessage.Visible)
                    {
                        _statMessage.Message.Clear();
                        _statMessage.Visible = false;
                    }
                }
            }
            else
            {
                // user is in cockpit

                var icubeG = cockpit.CubeGrid;
                if (icubeG?.Physics != null)
                {
                    ShiftTCals(icubeG);
                }

                else
                {
                    if (_statMessage != null && TextHudApi.Heartbeat && _statMessage.Visible)
                    {
                        _statMessage.Message.Clear();
                        _statMessage.Visible = false;
                    }
                }
            }
        }

        private void BattleShiftTHandling()
        {
            if (_vState == ViewState.InView2 && _statMessageBattle != null && TextHudApi.Heartbeat) //shift T menu
            {
                if (_statMessage != null && TextHudApi.Heartbeat)
                    if (_statMessage.Visible)
                    {
                        _statMessage.Message.Clear();
                        _statMessage.Visible = false;
                    }

                var cockpit = MyAPIGateway.Session.ControlledObject?.Entity as IMyCockpit;
                if (cockpit == null || MyAPIGateway.Session.IsCameraUserControlledSpectator)
                {
                    //user not in cockpit
                    var camMat = MyAPIGateway.Session.Camera.WorldMatrix;
                    IHitInfo hits;
                    MyAPIGateway.Physics.CastRay(camMat.Translation + camMat.Forward * 0.5,
                        camMat.Translation + camMat.Forward * 500, out hits);
                    if (hits?.HitEntity is IMyCubeGrid)
                    {
                        var icubeG = hits.HitEntity as IMyCubeGrid;
                        BattleShiftTCalcs(icubeG);
                    }
                    else
                    {
                        if (_statMessageBattle != null && TextHudApi.Heartbeat && _statMessageBattle.Visible)
                        {
                            _statMessageBattle.Message.Clear();
                            _statMessageBattle.Visible = false;
                            _statMessageBattleGunlist.Message.Clear();
                            _statMessageBattleGunlist.Visible = false;
                        }
                    }
                }

                else
                {
                    //user is in cockpit

                    //MyAPIGateway.Utilities.ShowNotification("INCOCKPITB");
                    var icubeG = cockpit.CubeGrid;
                    if (icubeG?.Physics != null)
                    {
                        BattleShiftTCalcs(icubeG);
                    }

                    else
                    {
                        if (_statMessageBattle != null && TextHudApi.Heartbeat && _statMessageBattle.Visible)
                        {
                            _statMessageBattle.Message.Clear();
                            _statMessageBattle.Visible = false;
                            _statMessageBattleGunlist.Message.Clear();
                            _statMessageBattleGunlist.Visible = false;
                        }
                    }
                }
            }
        }

        private void ShiftTCals(IMyCubeGrid icubeG)
        {
            // Update once per second
            if (MatchTimer.I.Ticks % 60 != 0)
                return;

            var trkd = new ShipTracker(icubeG);
            var pdInvestment = $"{trkd.PdPercentage}";
            var pdInvestmentNum = $"{trkd.PdInvest}";
            var totalShieldString = "None";

            if (trkd.ShieldStrength > 100)
                totalShieldString = $"{trkd.ShieldStrength / 100f:F2} M";
            else if (trkd.ShieldStrength > 1 && trkd.ShieldStrength < 100)
                totalShieldString = $"{trkd.ShieldStrength:F0}0 K";

            var gunTextBuilder = new StringBuilder();
            foreach (var x in trkd.GunL.Keys)
                gunTextBuilder.AppendFormat("<color=Green>{0}<color=White> x {1}\n", trkd.GunL[x], x);
            var gunText = gunTextBuilder.ToString();

            var specialBlockTextBuilder = new StringBuilder();
            foreach (var x in trkd.Sbl.Keys)
                specialBlockTextBuilder.AppendFormat("<color=Green>{0}<color=White> x {1}\n", trkd.Sbl[x], x);
            var specialBlockText = specialBlockTextBuilder.ToString();

            var massString = $"{trkd.Mass}";

            var thrustInKilograms = icubeG.GetMaxThrustInDirection(Base6Directions.Direction.Backward) / 9.81f;
            //float weight = trkd.Mass;
            var mass = trkd.Mass;
            var twr = (float)Math.Round(thrustInKilograms / mass, 1);

            if (trkd.Mass > 1000000)
            {
                massString = $"{Math.Round(trkd.Mass / 1000000f, 1):F2}m";
            }

            var twRs = $"{twr:F3}";
            var thrustString = $"{Math.Round(trkd.InstalledThrust, 1)}";

            if (trkd.InstalledThrust > 1000000)
                thrustString = $"{Math.Round(trkd.InstalledThrust / 1000000f, 1):F2}M";

            var playerName = trkd.Owner == null ? trkd.GridName : trkd.Owner.DisplayName;
            var factionName = trkd.Owner == null
                ? ""
                : MyAPIGateway.Session?.Factions?.TryGetPlayerFaction(trkd.OwnerId)?.Name;

            var speed = icubeG.GridSizeEnum == MyCubeSize.Large
                ? MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed
                : MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
            var reducedAngularSpeed = 0f;

            if (RtsApi != null && RtsApi.IsReady)
            {
                speed = (float)Math.Round(RtsApi.GetMaxSpeed(icubeG), 2);
                reducedAngularSpeed = RtsApi.GetReducedAngularSpeed(icubeG);
            }


            var pwrNotation = trkd.CurrentPower > 1000 ? "GW" : "MW";
            var tempPwr = trkd.CurrentPower > 1000
                ? $"{Math.Round(trkd.CurrentPower / 1000, 1):F1}"
                : Math.Round(trkd.CurrentPower, 1).ToString();
            var pwr = tempPwr + pwrNotation;

            var gyroString = $"{Math.Round(trkd.CurrentGyro, 1)}";

            double tempGyro2;
            if (trkd.CurrentGyro >= 1000000)
            {
                tempGyro2 = Math.Round(trkd.CurrentGyro / 1000000f, 1);
                if (tempGyro2 > 1000)
                    gyroString = $"{Math.Round(tempGyro2 / 1000, 1):F1}G";
                else
                    gyroString = $"{Math.Round(tempGyro2, 1):F1}M";
            }


            var sb = new StringBuilder();

            // Basic Info
            sb.AppendLine("----Basic Info----");
            sb.AppendFormat("<color=White>{0} ", icubeG.DisplayName);
            sb.AppendFormat("<color=Green>Owner<color=White>: {0} ", playerName);
            sb.AppendFormat("<color=Green>Faction<color=White>: {0}\n", factionName);
            sb.AppendFormat("<color=Green>Mass<color=White>: {0} kg\n", massString);
            sb.AppendFormat("<color=Green>Heavy blocks<color=White>: {0}\n", trkd.Heavyblocks);
            sb.AppendFormat("<color=Green>Total blocks<color=White>: {0}\n", trkd.BlockCount);
            sb.AppendFormat("<color=Green>PCU<color=White>: {0}\n", trkd.Pcu);
            sb.AppendFormat("<color=Green>Size<color=White>: {0}\n",
                (icubeG.Max + Vector3.Abs(icubeG.Min)).ToString());
            // sb.AppendFormat("<color=Green>Max Speed<color=White>: {0} | <color=Green>TWR<color=White>: {1}\n", speed, TWRs);
            sb.AppendFormat(
                "<color=Green>Max Speed<color=White>: {0} | <color=Green>Reduced Angular Speed<color=White>: {1:F2} | <color=Green>TWR<color=White>: {2}\n",
                speed, reducedAngularSpeed, twRs);
            sb.AppendLine(); //blank line

            // Battle Stats
            sb.AppendLine("<color=Orange>----Battle Stats----");
            sb.AppendFormat("<color=Green>Battle Points<color=White>: {0}\n", trkd.Bpts);
            sb.AppendFormat(
                "<color=Orange>[<color=Red> {0}% <color=Orange>| <color=Green>{1}% <color=Orange>| <color=DeepSkyBlue>{2}% <color=Orange>| <color=LightGray>{3}% <color=Orange>]\n",
                trkd.OffensivePercentage, trkd.PowerPercentage, trkd.MovementPercentage, trkd.MiscPercentage);
            sb.AppendFormat(
                "<color=Green>PD Investment<color=White>: <color=Orange>( <color=white>{0}% <color=Orange>|<color=Crimson> {1}%<color=Orange> )\n",
                pdInvestmentNum, pdInvestment);
            sb.AppendFormat(
                "<color=Green>Shield Max HP<color=White>: {0} <color=Orange>(<color=White>{1}%<color=Orange>)\n",
                totalShieldString, (int)trkd.CurrentShieldStrength);
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

        private void BattleShiftTCalcs(IMyCubeGrid icubeG)
        {
            if (icubeG?.Physics != null && MatchTimer.I.Ticks % 60 == 0)
            {
                var tracked = new ShipTracker(icubeG);
                var totalShield = tracked.ShieldStrength;
                var totalShieldString = totalShield > 100
                    ? $"{Math.Round(totalShield / 100f, 2):F2} M"
                    : totalShield > 1
                        ? $"{Math.Round(totalShield, 0):F0}0 K"
                        : "None";

                var maxSpeed = icubeG.GridSizeEnum == MyCubeSize.Large
                    ? MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed
                    : MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
                var reducedAngularSpeed = 0f;
                var negativeInfluence = 0f;

                if (RtsApi != null && RtsApi.IsReady)
                {
                    maxSpeed = (float)Math.Round(RtsApi.GetMaxSpeed(icubeG), 2);
                    reducedAngularSpeed = RtsApi.GetReducedAngularSpeed(icubeG);
                    negativeInfluence = RtsApi.GetNegativeInfluence(icubeG);
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
}
