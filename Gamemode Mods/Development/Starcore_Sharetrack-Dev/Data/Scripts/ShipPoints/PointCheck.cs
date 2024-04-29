using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CoreSystems.Api;
using DefenseShields;
using Draygo.API;
using Math0424.Networking;
using Math0424.ShipPoints;
using RelativeTopSpeed;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SCModRepository.Gamemode_Mods.Stable.Starcore_Sharetrack.Data.Scripts.ShipPoints.MatchTimer;
using SCModRepository_Dev.Gamemode_Mods.Development.Starcore_Sharetrack_Dev.Data.Scripts.ShipPoints;
using SENetworkAPI;
using ShipPoints.Commands;
using ShipPoints.Data.Scripts.ShipPoints.Networking;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static Math0424.Networking.MyNetworkHandler;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace klime.PointCheck
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class PointCheck : MySessionComponentBase
    {
        public static PointCheck I;

        public enum ProblemReportState
        {
            ThisIsFine,
            ItsOver
        }

        public const ushort ComId = 42511;
        public const string Keyword = "/debug";
        public const string DisplayName = "Debug";
        public static NetSync<int> ServerMatchState;
        public static int LocalMatchState;
        public static bool AmTheCaptainNow;
        public static int LocalProblemSwitch;
        public static Dictionary<string, int> PointValues = new Dictionary<string, int>();

        public static Dictionary<long, List<ulong>> Sending = new Dictionary<long, List<ulong>>();
        public static Dictionary<long, ShipTracker> Data = new Dictionary<long, ShipTracker>();
        public static HashSet<long> Tracking = new HashSet<long>();
        private static readonly Dictionary<long, IMyPlayer> AllPlayers = new Dictionary<long, IMyPlayer>();
        private static readonly List<IMyPlayer> ListPlayers = new List<IMyPlayer>();

        public static HudAPIv2.HUDMessage
            IntegretyMessage,
            TimerMessage,
            Ticketmessage,
            Problemmessage;

        public static bool Broadcaststat;
        public static string[] Viewmode = { "Player", "Grid", "Grid & Player", "False" };
        public static int Viewstat;
        public static int Decaytime = 180;
        public static int Delaytime = 60; //debug
        public static int MatchTickets = 1500;


        private HashSet<IMyEntity> _managedEntities = new HashSet<IMyEntity>();

        private int _count;
        private int _fastStart;


        private readonly Dictionary<string, int> _bp = new Dictionary<string, int>();

        // Get the sphere model based on the given cap color

        private bool _doClientRequest = true;
        private bool _joinInit;
        private readonly Dictionary<string, double> _m = new Dictionary<string, double>();
        private readonly Dictionary<string, int> _mbp = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _mobp = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _obp = new Dictionary<string, int>();

        private readonly Dictionary<string, int> _pbp = new Dictionary<string, int>();

        //Old cap
        public bool SphereVisual = true;
        public NetSync<string> Team1;
        public NetSync<int> Team1Tickets;
        public NetSync<string> Team2;
        public NetSync<int> Team2Tickets;
        public NetSync<string> Team3;
        public NetSync<int> Team3Tickets;


        public NetSync<int> ThreeTeams;

        // todo: remove this and replace with old solution for just combining BP and mass
        private readonly Dictionary<string, List<string>> _ts = new Dictionary<string, List<string>>();

        public HudAPIv2 TextHudApi { get; private set; }
        public WcApi WcApi { get; private set; }
        public ShieldApi ShApi { get; private set; }
        public RtsApi RtsApi { get; private set; }

        private HudPointsList _hudPointsList;


        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyNetworkHandler.Init();
            MyAPIGateway.Utilities.ShowMessage("ShipPoints v3.2 - Control Zone",
                "Aim at a grid and press Shift+T to show stats, " +
                "Shift+M to track a grid, Shift+J to cycle nametag style. ");

            if (!NetworkApi.IsInitialized) NetworkApi.Init(ComId, DisplayName, Keyword);

            InitializeNetSyncVariables();
        }

        private void InitializeNetSyncVariables()
        {
            Team1Tickets = CreateNetSync(0);
            Team2Tickets = CreateNetSync(0);
            Team3Tickets = CreateNetSync(0);

            Team1 = CreateNetSync("RED");
            Team2 = CreateNetSync("BLU");
            Team3 = CreateNetSync("NEU");

            ServerMatchState = CreateNetSync(0);

            ThreeTeams = CreateNetSync(0);

            //ProblemSwitch = CreateNetSync<int>(0);
        }

        private NetSync<T> CreateNetSync<T>(T defaultValue)
        {
            return new NetSync<T>(this, TransferType.Both, defaultValue, false, false);
        }

        public static void Begin()
        {
            MatchTimer.I.Ticks = 0;
            Broadcaststat = true;
            if (TimerMessage != null)
                TimerMessage.Visible = true;
            if (Ticketmessage != null)
                Ticketmessage.Visible = true;
            LocalMatchState = 1;
            MatchTimer.I.Start();
            MyAPIGateway.Utilities.ShowNotification("Commit die. Zone activates in " + Delaytime / 3600 +
                                                    "m, match ends in " + MatchTimer.I.MatchDurationMinutes + "m.");
            MyLog.Default.WriteLineAndConsole("Match started!");
        }

        public static void EndMatch()
        {
            MatchTimer.I.Ticks = 0;
            Broadcaststat = false;
            if (TimerMessage != null)
                TimerMessage.Visible = false;
            if (Ticketmessage != null)
                Ticketmessage.Visible = false;
            LocalMatchState = 0;
            AmTheCaptainNow = false;
            MatchTimer.I.Stop();
            MyAPIGateway.Utilities.ShowNotification("Match Ended.");
        }

        public static void AddPointValues(object obj)
        {
            // Deserialize the byte array (obj) into a string (var)
            var var = MyAPIGateway.Utilities.SerializeFromBinary<string>((byte[])obj);

            // Check if the deserialization was successful
            if (var != null)
            {
                // Split the string into an array of substrings using the ';' delimiter
                var split = var.Split(';');

                // Iterate through each substring (s) in the split array
                foreach (var s in split)
                {
                    // Split the substring (s) into an array of parts using the '@' delimiter
                    var parts = s.Split('@');
                    int value;

                    // Check if there are exactly 2 parts and if the second part is a valid integer (value)
                    if (parts.Length == 2 && int.TryParse(parts[1], out value))
                    {
                        // Trim the first part (name) and remove any extra whitespaces
                        var name = parts[0].Trim();
                        var lsIndex = name.IndexOf("{LS}");

                        // Check if the name contains "{LS}"
                        if (lsIndex != -1)
                        {
                            // Replace "{LS}" with "Large" and update the PointValues SendingDictionary
                            var largeName = name.Substring(0, lsIndex) + "Large" +
                                            name.Substring(lsIndex + "{LS}".Length);
                            PointValues[largeName] = value;

                            // Replace "{LS}" with "Small" and update the PointValues SendingDictionary
                            var smallName = name.Substring(0, lsIndex) + "Small" +
                                            name.Substring(lsIndex + "{LS}".Length);
                            PointValues[smallName] = value;
                        }
                        else
                        {
                            // Update the PointValues SendingDictionary directly
                            PointValues[name] = value;
                        }
                    }
                }
            }
        }

        public override void LoadData()
        {
            I = this;
            MyAPIGateway.Utilities.RegisterMessageHandler(2546247, AddPointValues);
            CommandHandler.Init();
            //Log.Init($"{ModContext.ModName}.log");
        }

        public override void BeforeStart()
        {
            // Check if the current instance is not a dedicated server
            if (!MyAPIGateway.Utilities.IsDedicated)
                // Initialize the sphere entities
                // Initialize the text_api with the HUDRegistered callback
                TextHudApi = new HudAPIv2(HudRegistered);

            // Initialize the WC_api and load it if it's not null

            WcApi = new WcApi();
            WcApi?.Load();

            // Initialize the SH_api and load it if it's not null
            ShApi = new ShieldApi();
            ShApi?.Load();

            // Initialize the RTS_api and load it if it's not null
            RtsApi = new RtsApi();
            RtsApi?.Load();
        }

        private void HudRegistered()
        {
            _hudPointsList = new HudPointsList();
            
            IntegretyMessage = new HudAPIv2.HUDMessage(scale: 1.15f, font: "BI_SEOutlined",
                Message: new StringBuilder(""), origin: new Vector2D(.51, .95), hideHud: false,
                blend: BlendTypeEnum.PostPP)
            {
                Visible = true
            };
            TimerMessage = new HudAPIv2.HUDMessage(scale: 1.2f, font: "BI_SEOutlined", Message: new StringBuilder(""),
                origin: new Vector2D(0.35, .99), hideHud: false, shadowing: true, blend: BlendTypeEnum.PostPP)
            {
                Visible = false, //defaulted off?
                InitialColor = Color.White
            };
            Ticketmessage = new HudAPIv2.HUDMessage(scale: 1f, font: "BI_SEOutlined", Message: new StringBuilder(""),
                origin: new Vector2D(0.51, .99), hideHud: false, shadowing: true, blend: BlendTypeEnum.PostPP)
            {
                Visible = false, //defaulted off?
                InitialColor = Color.White
            };

            Problemmessage = new HudAPIv2.HUDMessage(scale: 2f, font: "BI_SEOutlined", Message: new StringBuilder(""),
                origin: new Vector2D(-.99, 0), hideHud: false, shadowing: true, blend: BlendTypeEnum.PostPP)
            {
                Visible = false, //defaulted off?
                InitialColor = Color.White
            };
        }

        public override void UpdateAfterSimulation()
        {
            // Send request to server for tracked grids. Why is it in here? so that integretymessage is exist.
            if (_doClientRequest && !MyAPIGateway.Session.IsServer)
            {
                Static.MyNetwork.TransmitToServer(new SyncRequestPacket(), false);
                _doClientRequest = false;
            }

            if (MatchTimer.I.Ticks >= 144000)
            {
                MatchTimer.I.Ticks = 0;
            }

            try
            {
                if (!MyAPIGateway.Utilities.IsDedicated && Broadcaststat)
                {
                    var tick100 = MatchTimer.I.Ticks % 100 == 0;
                    if (MatchTimer.I.Ticks - _fastStart < 300 || tick100)
                    {
                        _fastStart = MatchTimer.I.Ticks;
                        if (_joinInit == false)
                        {
                            Static.MyNetwork.TransmitToServer(new BasicPacket(7), true, true);
                            ServerMatchState.Fetch();
                            Team1.Fetch();
                            Team2.Fetch();
                            Team3.Fetch();
                            ServerMatchState.Fetch();
                            Team1Tickets.Fetch();
                            Team2Tickets.Fetch();
                            Team3Tickets.Fetch();
                            ThreeTeams.Fetch();
                            _joinInit = true;
                        }
                    }
                }

                if (!MyAPIGateway.Utilities.IsDedicated && MatchTimer.I.Ticks % 60 == 0)
                {
                    if (ServerMatchState.Value == 1 && Broadcaststat == false) Broadcaststat = true;
                    if (!MyAPIGateway.Utilities.IsDedicated && AmTheCaptainNow)
                        ServerMatchState.Value = LocalMatchState;
                    else if (!MyAPIGateway.Utilities.IsDedicated && !AmTheCaptainNow)
                        LocalMatchState = ServerMatchState.Value;
                }

                if (Broadcaststat && MatchTimer.I.Ticks % 60 == 0)
                    if (AmTheCaptainNow && ServerMatchState.Value != 1)
                        ServerMatchState.Value = 1;
            }
            catch (Exception e)
            {
                Log.Error($"Exception in UpdateAfterSimulation TryCatch 01: {e}");
            }

            try
            {
                if (MatchTimer.I.Ticks % 60 == 0)
                {
                    AllPlayers.Clear();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(ListPlayers, delegate(IMyPlayer p)
                        {
                            AllPlayers.Add(p.IdentityId, p);
                            return false;
                        }
                    );
                    if (MyAPIGateway.Session.IsServer)
                        foreach (var x in Sending.Keys)
                        {
                            ShipTracker shipTracker;
                            if (!Data.TryGetValue(x, out shipTracker))
                            {
                                var entity = MyEntities.GetEntityById(x) as IMyCubeGrid;
                                if (entity?.Physics != null)
                                {
                                    shipTracker = new ShipTracker(entity);
                                    Data.Add(x, shipTracker);
                                    if (!MyAPIGateway.Utilities.IsDedicated) shipTracker.CreateHud();
                                }
                            }
                            else
                            {
                                shipTracker.Update();
                            }

                            if (shipTracker != null)
                                foreach (var p in Sending[x])
                                {
                                    var packet = new PacketGridData { Id = x, Tracked = shipTracker }
                                        ;
                                    Static.MyNetwork.TransmitToPlayer(packet, p);
                                }
                        }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Exception in UpdateAfterSimulation TryCatch 02: {e}");
            }

            try
            {
                if (MatchTimer.I.Ticks % 60 == 0 && Broadcaststat)
                {
                    _count++;
                    if (_count - _fastStart < 300 || _count % 100 == 0)
                    {
                        _managedEntities.Clear();
                        MyAPIGateway.Entities.GetEntities(_managedEntities, entity => entity is IMyCubeGrid);
                        foreach (var entity in _managedEntities)
                        {
                            var grid = entity as MyCubeGrid;
                            if ((grid == null || !grid.HasBlockWithSubtypeId("LargeFlightMovement")) &&
                                !grid.HasBlockWithSubtypeId("RivalAIRemoteControlLarge"))
                                continue;

                            var entityId = grid.EntityId;
                            if (!Tracking.Contains(entityId))
                            {
                                var packet = new PacketGridData
                                        { Id = entityId, Value = (byte)(Tracking.Contains(entityId) ? 2 : 1) }
                                    ;
                                Static.MyNetwork.TransmitToServer(packet);
                                if (packet.Value == 1)
                                {
                                    MyAPIGateway.Utilities.ShowNotification("ShipTracker: Added grid to tracker");
                                    Tracking.Add(entityId);
                                    if (!IntegretyMessage.Visible) IntegretyMessage.Visible = true;
                                    Data[entityId].CreateHud();
                                }
                                else
                                {
                                    MyAPIGateway.Utilities.ShowNotification(
                                        "ShipTracker: Removed grid from tracker");
                                    Tracking.Remove(entityId);
                                    Data[entityId].DisposeHud();
                                }
                            }

                            _fastStart = _count;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Exception in UpdateAfterSimulation TryCatch 03: {e}");
            }
        }


        public override void Draw()
        {
            //if you are the server do nothing here
            if (MyAPIGateway.Utilities.IsDedicated || !TextHudApi.Heartbeat)
                return;
            try
            {
                if (MyAPIGateway.Session?.Camera != null && MyAPIGateway.Session.CameraController != null && !MyAPIGateway.Gui.ChatEntryVisible &&
                    !MyAPIGateway.Gui.IsCursorVisible && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None)
                {
                    HandleKeyInputs();
                }

                foreach (var x in Data.Keys)
                    if (Tracking.Contains(x))
                        Data[x].UpdateHud();
                    else
                        Data[x].DisposeHud();

                Problemmessage.Message.Clear();
                switch ((ProblemReportState) LocalProblemSwitch)
                {
                    case ProblemReportState.ItsOver:
                        const string tempText = "<color=Red>" + "A PROBLEM HAS BEEN REPORTED," + "\n" +
                                                "CHECK WITH BOTH TEAMS AND THEN TYPE '/fixed' TO CLEAR THIS MESSAGE";
                        Problemmessage.Message.Append(tempText);
                        Problemmessage.Visible = true;
                        break;
                    case ProblemReportState.ThisIsFine:
                        Problemmessage.Visible = false;
                        break;
                }

                _hudPointsList?.UpdateDraw();

                UpdateTrackingData();
            }
            catch (Exception e)
            {
                Log.Error($"Exception in Draw: {e}");
            }
        }


        private readonly Dictionary<MyKeys, Action> _keyAndActionPairs = new Dictionary<MyKeys, Action>
        {
            {
                MyKeys.M, () =>
                {
                    IMyCubeGrid castGrid = RaycastGridFromCamera();
                    var packet = new PacketGridData
                    {
                        Id = castGrid.EntityId,
                        Value = (byte)(Tracking.Contains(castGrid.EntityId) ? 2 : 1)
                    };
                    Static.MyNetwork.TransmitToServer(packet);

                    if (packet.Value == 1)
                    {
                        MyAPIGateway.Utilities.ShowNotification(
                            "ShipTracker: Added grid to tracker");
                        Tracking.Add(castGrid.EntityId);
                        if (!IntegretyMessage.Visible) IntegretyMessage.Visible = true;
                        Data[castGrid.EntityId].CreateHud();
                    }
                    else
                    {
                        MyAPIGateway.Utilities.ShowNotification(
                            "ShipTracker: Removed grid from tracker");
                        Tracking.Remove(castGrid.EntityId);
                        Data[castGrid.EntityId].DisposeHud();
                    }
                }
            },
            {
                MyKeys.N, () =>
                {
                    IntegretyMessage.Visible = !IntegretyMessage.Visible;
                    MyAPIGateway.Utilities.ShowNotification("ShipTracker: Hud visibility set to " +
                                                            IntegretyMessage.Visible);
                }
            },
            {
                MyKeys.B, () =>
                {
                    TimerMessage.Visible = !TimerMessage.Visible;
                    Ticketmessage.Visible = !Ticketmessage.Visible;
                    MyAPIGateway.Utilities.ShowNotification(
                        "ShipTracker: Timer visibility set to " + TimerMessage.Visible);
                }
            },
            {
                MyKeys.J, () =>
                {
                    Viewstat++;
                    if (Viewstat == 4) Viewstat = 0;
                    PointCheckHelpers.NameplateVisible = Viewstat != 3;
                    MyAPIGateway.Utilities.ShowNotification(
                        "ShipTracker: Nameplate visibility set to " + Viewmode[Viewstat]);
                }
            }
        };

        private void HandleKeyInputs()
        {
            if (!MyAPIGateway.Input.IsAnyShiftKeyPressed())
                return;

            if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.T))
                _hudPointsList?.CycleViewState();

            if (MyAPIGateway.Session.PromoteLevel < MyPromoteLevel.Moderator)
                return;

            foreach (var pair in _keyAndActionPairs)
                if (MyAPIGateway.Input.IsNewKeyPressed(pair.Key))
                    pair.Value.Invoke();
        }


        private void UpdateTrackingData()
        {
            if (MatchTimer.I.Ticks % 60 != 0 || IntegretyMessage == null || !TextHudApi.Heartbeat)
                return;

            var tt = new StringBuilder();

            // Clear the dictionaries to remove old data
            _ts.Clear();
            _m.Clear();
            _bp.Clear();
            _mbp.Clear();
            _pbp.Clear();
            _obp.Clear();
            _mobp.Clear();

            MainTrackerUpdate(_ts, _m, _bp, _mbp, _pbp, _obp, _mobp);

            // Match time
            tt.Append("<color=orange>----                 <color=white>Match Time: ")
                .Append(MatchTimer.I.CurrentMatchTime.ToString(@"mm\:ss"))
                .Append('/')
                .Append(MatchTimer.I.MatchDurationString)
                .Append("                 <color=orange>----\n");

            TeamBpCalc(tt, _ts, _m, _bp, _mbp, _pbp, _obp, _mobp);

            var autotrackenabled = false;
            // Autotrack players when match is running, set above bool to true to enable
            if (MatchTimer.I.Ticks % 240 == 0 && autotrackenabled)
            {
                var ce = MyAPIGateway.Session.Player?.Controller?.ControlledEntity?.Entity;
                var ck = ce as IMyCockpit;
                var eid = ck.CubeGrid.EntityId;

                AutoTrackPilotedShip(ck, eid);
            }

            IntegretyMessage.Message.Clear();
            IntegretyMessage.Message.Append(tt);
        }


        private void MainTrackerUpdate(Dictionary<string, List<string>> ts, Dictionary<string, double> m,
            Dictionary<string, int> bp, Dictionary<string, int> mbp, Dictionary<string, int> pbp,
            Dictionary<string, int> obp, Dictionary<string, int> mobp)
        {
            foreach (var z in Tracking)
            {
                ShipTracker d;
                if (!Data.TryGetValue(z, out d))
                    continue;
                d.LastUpdate--;

                if (d.LastUpdate <= 0)
                {
                    Data[z].DisposeHud();
                    Data.Remove(z);
                    continue;
                }

                var fn = d.FactionName;
                var o = d.OwnerName;
                var nd = d.IsFunctional;

                if (!ts.ContainsKey(fn))
                {
                    ts.Add(fn, new List<string>());
                    m[fn] = 0;
                    bp[fn] = 0;
                    mbp[fn] = 0;
                    pbp[fn] = 0;
                    obp[fn] = 0;
                    mobp[fn] = 0;
                }

                if (nd)
                {
                    m[fn] += d.Mass;
                    bp[fn] += d.Bpts;
                }
                else
                {
                    continue;
                }

                mbp[fn] += d.MiscBps;
                pbp[fn] += d.PowerBps;
                obp[fn] += d.OffensiveBps;
                mobp[fn] += d.MovementBps;

                var g = d.GunL.Values.Sum();
                var pwr = FormatPower(Math.Round(d.CurrentPower, 1));
                var ts2 = FormatThrust(Math.Round(d.InstalledThrust, 2));

                ts[fn].Add(CreateDisplayString(o, d, g, pwr, ts2));
            }
        }

        private string FormatPower(double currentPower)
        {
            return currentPower > 1000 ? $"{Math.Round(currentPower / 1000, 1)}GW" : $"{currentPower}MW";
        }

        private string FormatThrust(double installedThrust)
        {
            var thrustInMega = Math.Round(installedThrust / 1e6, 1);
            return thrustInMega > 1e2 ? $"{Math.Round(thrustInMega / 1e3, 2)}GN" : $"{thrustInMega}MN";
        }

        private string CreateDisplayString(string ownerName, ShipTracker d, int g, string power, string thrust)
        {
            var ownerDisplay = ownerName != null ? ownerName.Substring(0, Math.Min(ownerName.Length, 7)) : d.GridName;
            var integrityPercent = (int)(d.CurrentIntegrity / d.OriginalIntegrity * 100);
            var shieldPercent = (int)d.CurrentShieldStrength;
            var shieldColor = shieldPercent <= 0
                ? "red"
                : $"{255},{255 - d.ShieldHeat * 20},{255 - d.ShieldHeat * 20}";
            var weaponColor = g == 0 ? "red" : "orange";
            var functionalColor = d.IsFunctional ? "white" : "red";
            return string.Format(
                "<color={0}>{1,-8}{2,3}%<color={3}> P:<color=orange>{4,3}<color={5}> T:<color=orange>{6,3}<color={7}> W:<color={8}>{9,3}<color={10}> S:<color={11}>{12,3}%<color=white>",
                functionalColor, ownerDisplay, integrityPercent, functionalColor, power, functionalColor, thrust,
                functionalColor, weaponColor, g, functionalColor, shieldColor, shieldPercent);
        }


        private static void TeamBpCalc(StringBuilder tt, Dictionary<string, List<string>> trackedShip,
            Dictionary<string, double> m, Dictionary<string, int> bp, Dictionary<string, int> mbp,
            Dictionary<string, int> pbp, Dictionary<string, int> obp, Dictionary<string, int> mobp)
        {
            foreach (var x in trackedShip.Keys)
            {
                var msValue = m[x] / 1e6;
                var tbi = 100f / bp[x];

                tt.Append("<color=orange>---- ")
                    .Append(x)
                    .Append(" : ")
                    .AppendFormat("{0:0.00}M : {1}bp <color=orange>[", msValue, bp[x]);

                tt.AppendFormat("<color=Red>{0}<color=white>%<color=orange>|", (int)(obp[x] * tbi + 0.5f))
                    .AppendFormat("<color=Green>{0}<color=white>%<color=orange>|", (int)(pbp[x] * tbi + 0.5f))
                    .AppendFormat("<color=DeepSkyBlue>{0}<color=white>%<color=orange>|", (int)(mobp[x] * tbi + 0.5f))
                    .AppendFormat("<color=LightGray>{0}<color=white>%<color=orange>]", (int)(mbp[x] * tbi + 0.5f))
                    .AppendLine(" ---------");

                foreach (var y in trackedShip[x]) tt.AppendLine(y);
            }
        }


        private static void AutoTrackPilotedShip(IMyCockpit cockpit, long entityId)
        {
            if (cockpit == null || Tracking.Contains(entityId)) return;

            var hasGyro = false;
            var hasBatteryOrReactor = false;
            var gridBlocks = new List<IMySlimBlock>();
            cockpit.CubeGrid.GetBlocks(gridBlocks);

            foreach (var block in gridBlocks)
            {
                if (block.FatBlock is IMyGyro)
                    hasGyro = true;
                else if (block.FatBlock is IMyBatteryBlock || block.FatBlock is IMyReactor) hasBatteryOrReactor = true;

                if (hasGyro && hasBatteryOrReactor) break;
            }

            if (hasGyro && hasBatteryOrReactor)
            {
                var packetData = new PacketGridData { Id = entityId, Value = 1 };
                Static.MyNetwork.TransmitToServer(packetData);
                MyAPIGateway.Utilities.ShowNotification("ShipTracker: Added grid to tracker");
                Tracking.Add(entityId);
                if (!IntegretyMessage.Visible) IntegretyMessage.Visible = true;
                Data[entityId].CreateHud();
            }
        }


        public static void There_Is_A_Problem()
        {
            LocalProblemSwitch = 1;
        }

        public static void There_Is_A_Solution()
        {
            LocalProblemSwitch = 0;
        }

        public static IMyPlayer GetOwner(long v)
        {
            if (AllPlayers != null && AllPlayers.ContainsKey(v)) return AllPlayers[v];
            return null;
        }

        protected override void UnloadData()
        {
            Log.Info("Start PointCheck.UnloadData()");
            base.UnloadData();
            CommandHandler.Close();

            TextHudApi?.Unload();
            WcApi?.Unload();
            ShApi?.Unload();
            if (PointValues != null)
            {
                PointValues.Clear();
                Sending.Clear();
                Data.Clear();
                AllPlayers.Clear();
                ListPlayers.Clear();
            }

            //NetworkAPI.Instance.Close();
            foreach (var x in Data.Keys)
                if (Tracking.Contains(x))
                    Data[x].UpdateHud();
                else
                    Data[x].DisposeHud();

            Static?.Dispose();
            MyAPIGateway.Utilities.UnregisterMessageHandler(2546247, AddPointValues);

            I = null;
        }

        public static IMyCubeGrid RaycastGridFromCamera()
        {
            var camMat = MyAPIGateway.Session.Camera.WorldMatrix;
            var hits = new List<MyLineSegmentOverlapResult<MyEntity>>();
            var ray = new LineD(camMat.Translation, camMat.Translation + camMat.Forward * 500);
            MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref ray, hits);
            foreach (var hit in hits)
            {
                var grid = hit.Element as IMyCubeGrid;

                if (grid?.Physics != null)
                    return grid;
            }

            return null;
        }
    }


    public static class GridExtensions
    {
        public static bool HasBlockWithSubtypeId(this IMyCubeGrid grid, string subtypeId)
        {
            List<IMySlimBlock> allBlocks = new List<IMySlimBlock>();
            grid?.GetBlocks(allBlocks, block => block.FatBlock != null);

            foreach (IMySlimBlock block in allBlocks)
                if (block.BlockDefinition.Id.SubtypeName == subtypeId)
                    return true;

            return false;
        }
    }
}