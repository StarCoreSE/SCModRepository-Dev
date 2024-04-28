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
using SENetworkAPI;
using ShipPoints.Commands;
using ShipPoints.Data.Scripts.ShipPoints.Networking;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
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

        public enum ViewState
        {
            None,
            InView,
            InView2,
            ExitView
        }

        public enum ViewStateP
        {
            ThisIsFine,
            ItsOver
        }

        public const ushort ComId = 42511;
        public const string Keyword = "/debug";
        public const string DisplayName = "Debug";
        private const double CombatRadius = 12500;
        private const double ViewDistSqr = 306250000;
        public static NetSync<int> ServerMatchState;
        public static int LocalMatchState;
        public static bool _amTheCaptainNow;
        public static int LocalGameModeSwitch = 3;
        public static int LocalProblemSwitch;
        public static Dictionary<string, int> PointValues = new Dictionary<string, int>();

        private static readonly Dictionary<long, List<ulong>> SendingDictionary = new Dictionary<long, List<ulong>>();
        public static Dictionary<long, List<ulong>> Sending = SendingDictionary;
        private static readonly Dictionary<long, ShipTracker> DataDictionary = new Dictionary<long, ShipTracker>();
        public static Dictionary<long, ShipTracker> Data = DataDictionary;
        public static HashSet<long> Tracking = new HashSet<long>();
        private static readonly Dictionary<long, IMyPlayer> AllPlayers = new Dictionary<long, IMyPlayer>();
        private static readonly List<IMyPlayer> ListPlayers = new List<IMyPlayer>();

        public static HudAPIv2.HUDMessage StatMessage,
            IntegretyMessage,
            TimerMessage,
            Ticketmessage,
            StatMessageBattle,
            StatMessageBattleGunlist,
            Problemmessage;

        public static bool Broadcaststat;
        public static string[] Viewmode = { "Player", "Grid", "Grid & Player", "False" };
        public static int Viewstat;
        public static int Wintime = 120;
        public static int Decaytime = 180;
        public static int Delaytime = 60; //debug
        public static int Matchtime = 72000;
        public static int MatchTickets = 1500;
        public static int TempServerTimer;


        private readonly List<MyEntity> _managedEntities = new List<MyEntity>(1000);

        //  private readonly StringBuilder _gunTextBuilder = new StringBuilder();
        private readonly StringBuilder _speedTextBuilder = new StringBuilder();
        private BoundingSphereD _combatMaxSphere = new BoundingSphereD(Vector3D.Zero, CombatRadius + 22500);
        private int _count;
        private int _fastStart;

        private readonly StringBuilder _gunTextBuilder = new StringBuilder();

        private readonly Dictionary<string, int> _bp = new Dictionary<string, int>();

        //public NetSync<int> CaptainCapTimerZ1; public NetSync<int> CaptainCapTimerZ2; public NetSync<int> CaptainCapTimerZ3;
        public NetSync<int> CaptainCapTimerZ1T1;
        public NetSync<int> CaptainCapTimerZ1T2;
        public NetSync<int> CaptainCapTimerZ1T3;
        public NetSync<int> CaptainCapTimerZ2T1;
        public NetSync<int> CaptainCapTimerZ2T2;
        public NetSync<int> CaptainCapTimerZ2T3;
        public NetSync<int> CaptainCapTimerZ3T1;
        public NetSync<int> CaptainCapTimerZ3T2;

        public NetSync<int> CaptainCapTimerZ3T3;

        //
        public NetSync<Vector3D> CaptainRandVector3D;

        public Vector3D ClientRandVector3D;
        // Get the sphere model based on the given cap color

        private bool _doClientRequest = true;
        public NetSync<int> GameModeSwitch;
        private bool _joinInit;
        private readonly Dictionary<string, double> _m = new Dictionary<string, double>();
        private readonly Dictionary<string, int> _mbp = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _mobp = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _obp = new Dictionary<string, int>();

        private readonly Dictionary<string, int> _pbp = new Dictionary<string, int>();

        //Old cap
        public NetSync<int> ServerSyncTimer;
        public bool SphereVisual = true;
        public NetSync<string> Team1;
        public NetSync<int> Team1Tickets;
        public NetSync<string> Team2;
        public NetSync<int> Team2Tickets;
        public NetSync<string> Team3;
        public NetSync<int> Team3Tickets;
        public int TempLocalTimer;

        private HudAPIv2 _textApi;

        public NetSync<int> ThreeTeams;

        // todo: remove this and replace with old solution for just combining BP and mass
        private readonly Dictionary<string, List<string>> _ts = new Dictionary<string, List<string>>();
        private ViewState _vState = ViewState.None;
        private ViewStateP _vStateP = ViewStateP.ThisIsFine;
        public static WcApi WcApi { get; private set; }
        public static ShieldApi ShApi { get; private set; }
        public RtsApi RtsApi { get; private set; }


        //end visual
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyNetworkHandler.Init();
            MyAPIGateway.Utilities.ShowMessage("ShipPoints v3.2 - Control Zone",
                "Aim at a grid and press Shift+T to show stats, " +
                "Shift+M to track a grid, Shift+J to cycle nametag style. " +
                "Type '/sphere' to turn off/on the sphere visuals.");

            if (!NetworkApi.IsInitialized) NetworkApi.Init(ComId, DisplayName, Keyword);

            InitializeNetSyncVariables();
        }

        private void InitializeNetSyncVariables()
        {
            CaptainCapTimerZ1T1 = CreateNetSync(0);
            CaptainCapTimerZ1T2 = CreateNetSync(0);
            CaptainCapTimerZ1T3 = CreateNetSync(0);
            CaptainCapTimerZ2T1 = CreateNetSync(0);
            CaptainCapTimerZ2T2 = CreateNetSync(0);
            CaptainCapTimerZ2T3 = CreateNetSync(0);
            CaptainCapTimerZ3T1 = CreateNetSync(0);
            CaptainCapTimerZ3T2 = CreateNetSync(0);
            CaptainCapTimerZ3T3 = CreateNetSync(0);

            Team1Tickets = CreateNetSync(0);
            Team2Tickets = CreateNetSync(0);
            Team3Tickets = CreateNetSync(0);

            Team1 = CreateNetSync("RED");
            Team2 = CreateNetSync("BLU");
            Team3 = CreateNetSync("NEU");

            ServerMatchState = CreateNetSync(0);
            ServerSyncTimer = CreateNetSync(0);

            ThreeTeams = CreateNetSync(0);
            GameModeSwitch = CreateNetSync(3);

            //ProblemSwitch = CreateNetSync<int>(0);

            CaptainRandVector3D = CreateNetSync(ClientRandVector3D);
        }

        private NetSync<T> CreateNetSync<T>(T defaultValue)
        {
            return new NetSync<T>(this, TransferType.Both, defaultValue, false, false);
        }

        public static void Begin()
        {
            TempServerTimer = 0;
            PointCheckHelpers.Timer = 0;
            Broadcaststat = true;
            if (TimerMessage != null)
                TimerMessage.Visible = true;
            if (Ticketmessage != null)
                Ticketmessage.Visible = true;
            LocalMatchState = 1;
            MatchTimer.I.Start(Matchtime / 60d / 60d);
            MyAPIGateway.Utilities.ShowNotification("Commit die. Zone activates in " + Delaytime / 3600 +
                                                    "m, match ends in " + Matchtime / 3600 + "m.");
            MyLog.Default.WriteLineAndConsole("Match started!");
        }

        public static void EndMatch()
        {
            TempServerTimer = 0;
            PointCheckHelpers.Timer = 0;
            Broadcaststat = false;
            if (TimerMessage != null)
                TimerMessage.Visible = false;
            if (Ticketmessage != null)
                Ticketmessage.Visible = false;
            LocalMatchState = 0;
            _amTheCaptainNow = false;
            MatchTimer.I.Stop();
            MyAPIGateway.Utilities.ShowNotification("Match Ended.");
        }

        public static void TrackYourselfMyMan()
        {
            try
            {
                if (!Broadcaststat ||
                    !(MyAPIGateway.Session.Player.Controller?.ControlledEntity?.Entity is IMyCockpit))
                    return;

                // Clear tracking and sending lists
                Tracking.Clear();
                Sending.Clear();

                // Get the controlled cockpit
                var cockpit = (IMyCockpit) MyAPIGateway.Session.Player.Controller?.ControlledEntity?.Entity;

                // Check if the grid data exists
                if (cockpit == null || !Data.ContainsKey(cockpit.CubeGrid.EntityId))
                    return;

                // Dispose the current HUD
                Data[cockpit.CubeGrid.EntityId].DisposeHud();

                // Create a packet for the grid data
                var packet = new PacketGridData
                {
                    Id = cockpit.CubeGrid.EntityId,
                    Value = (byte)(Tracking.Contains(cockpit.CubeGrid.EntityId) ? 2 : 1)
                };

                // Transmit the packet to the server
                Static.MyNetwork.TransmitToServer(packet, true, true);

                // Update tracking and HUD based on the packet value
                if (packet.Value == 1)
                {
                    Tracking.Add(cockpit.CubeGrid.EntityId);
                    IntegretyMessage.Visible = true;
                    Data[cockpit.CubeGrid.EntityId].CreateHud();
                }
                else
                {
                    Tracking.Remove(cockpit.CubeGrid.EntityId);
                    Data[cockpit.CubeGrid.EntityId].DisposeHud();
                    IntegretyMessage.Visible = false;

                    // Create another packet to re-track if necessary
                    var packetB = new PacketGridData
                    {
                        Id = cockpit.CubeGrid.EntityId,
                        Value = (byte)(Tracking.Contains(cockpit.CubeGrid.EntityId) ? 2 : 1)
                    };

                    // Transmit the packet to the server
                    Static.MyNetwork.TransmitToServer(packetB);

                    if (packetB.Value == 1)
                    {
                        Tracking.Add(cockpit.CubeGrid.EntityId);
                        IntegretyMessage.Visible = true;
                        Data[cockpit.CubeGrid.EntityId].CreateHud();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Exception in TrackYourselfMyMan: {e}");
            }
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
            //base.BeforeStart();
            // Check if the current instance is not a dedicated server
            if (!MyAPIGateway.Utilities.IsDedicated)
                // Initialize the sphere entities
                // Initialize the text_api with the HUDRegistered callback
                _textApi = new HudAPIv2(HudRegistered);

            // Initialize the WC_api and load it if it's not null

            WcApi = new WcApi();
            if (WcApi != null) WcApi.Load();

            // Initialize the SH_api and load it if it's not null
            ShApi = new ShieldApi();
            if (ShApi != null) ShApi.Load();

            // Initialize the RTS_api and load it if it's not null
            RtsApi = new RtsApi();
            if (RtsApi != null) RtsApi.Load();
        }

        private void HudRegistered()
        {
            StatMessage = new HudAPIv2.HUDMessage(scale: 1f, font: "BI_SEOutlined", Message: new StringBuilder(""),
                origin: new Vector2D(-.99, .99), hideHud: false, blend: BlendTypeEnum.PostPP)
            {
                //Blend = BlendTypeEnum.PostPP,
                Visible = false, //defaulted off?
                InitialColor = Color.Orange
                //ShadowColor = Color.Black,
            };
            StatMessageBattle = new HudAPIv2.HUDMessage(scale: 1.25f, font: "BI_SEOutlined",
                Message: new StringBuilder(""), origin: new Vector2D(-.54, -0.955), hideHud: false,
                blend: BlendTypeEnum.PostPP)
            {
                //Blend = BlendTypeEnum.PostPP,
                Visible = false //defaulted off?
                //ShadowColor = Color.Black,
            };
            StatMessageBattleGunlist = new HudAPIv2.HUDMessage(scale: 1.25f, font: "BI_SEOutlined",
                Message: new StringBuilder(""), origin: new Vector2D(-.99, .99), hideHud: false, shadowing: true,
                blend: BlendTypeEnum.PostPP)
            {
                //Blend = BlendTypeEnum.PostPP,
                Visible = false //defaulted off?
                //ShadowColor = Color.Black,
            };
            IntegretyMessage = new HudAPIv2.HUDMessage(scale: 1.15f, font: "BI_SEOutlined",
                Message: new StringBuilder(""), origin: new Vector2D(.51, .95), hideHud: false,
                blend: BlendTypeEnum.PostPP)
            {
                Visible = true
                //InitialColor = Color.Orange
            };
            TimerMessage = new HudAPIv2.HUDMessage(scale: 1.2f, font: "BI_SEOutlined", Message: new StringBuilder(""),
                origin: new Vector2D(0.35, .99), hideHud: false, shadowing: true, blend: BlendTypeEnum.PostPP)
            {
                Visible = false, //defaulted off?
                InitialColor = Color.White
                //ShadowColor = Color.Black
            };
            Ticketmessage = new HudAPIv2.HUDMessage(scale: 1f, font: "BI_SEOutlined", Message: new StringBuilder(""),
                origin: new Vector2D(0.51, .99), hideHud: false, shadowing: true, blend: BlendTypeEnum.PostPP)
            {
                Visible = false, //defaulted off?
                InitialColor = Color.White
                //ShadowColor = Color.Black
            };

            Problemmessage = new HudAPIv2.HUDMessage(scale: 2f, font: "BI_SEOutlined", Message: new StringBuilder(""),
                origin: new Vector2D(-.99, 0), hideHud: false, shadowing: true, blend: BlendTypeEnum.PostPP)
            {
                Visible = false, //defaulted off?
                InitialColor = Color.White
                //ShadowColor = Color.Black
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

            TempLocalTimer++;
            PointCheckHelpers.Timer++;
            if (PointCheckHelpers.Timer >= 144000)
            {
                PointCheckHelpers.Timer = 0;
                TempLocalTimer = 0;
                TempServerTimer = 0;
            }

            if (_joinInit)
            {
            }

            if (MyAPIGateway.Utilities.IsDedicated && TempServerTimer % 60 == 0 && Broadcaststat)
            {
                ServerSyncTimer.Value = TempServerTimer;
                ServerSyncTimer.Push();
            }

            if (Broadcaststat && !_amTheCaptainNow && TempLocalTimer % 60 == 0)
            {
                ServerSyncTimer.Fetch();
                PointCheckHelpers.Timer = ServerSyncTimer.Value;
                TempLocalTimer = 0;
            }

            try
            {
                if (!MyAPIGateway.Utilities.IsDedicated && Broadcaststat)
                {
                    var tick100 = PointCheckHelpers.Timer % 100 == 0;
                    if (PointCheckHelpers.Timer - _fastStart < 300 || tick100)
                    {
                        _fastStart = PointCheckHelpers.Timer;
                        if (_joinInit == false)
                        {
                            Static.MyNetwork.TransmitToServer(new BasicPacket(7), true, true);
                            ServerMatchState.Fetch();
                            Team1.Fetch();
                            Team2.Fetch();
                            Team3.Fetch();
                            ServerMatchState.Fetch();
                            ServerSyncTimer.Fetch();
                            Team1Tickets.Fetch();
                            Team2Tickets.Fetch();
                            Team3Tickets.Fetch();
                            ThreeTeams.Fetch();
                            GameModeSwitch.Fetch();
                            LocalGameModeSwitch = GameModeSwitch.Value;
                            _joinInit = true;
                        }
                    }
                }

                if (!MyAPIGateway.Utilities.IsDedicated && TempLocalTimer % 60 == 0)
                {
                    if (ServerMatchState.Value == 1 && Broadcaststat == false) Broadcaststat = true;
                    if (!MyAPIGateway.Utilities.IsDedicated && _amTheCaptainNow)
                        ServerMatchState.Value = LocalMatchState;
                    else if (!MyAPIGateway.Utilities.IsDedicated && !_amTheCaptainNow)
                        LocalMatchState = ServerMatchState.Value;
                }

                if (Broadcaststat && PointCheckHelpers.Timer % 60 == 0)
                    if (_amTheCaptainNow && ServerMatchState.Value != 1)
                        ServerMatchState.Value = 1;
            }
            catch (Exception e)
            {
                Log.Error($"Exception in UpdateAfterSimulation TryCatch 01: {e}");
            }

            try
            {
                if (PointCheckHelpers.Timer % 60 == 0)
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
                if (PointCheckHelpers.Timer % 60 == 0 && Broadcaststat)
                {
                    var tick100 = _count % 100 == 0;
                    _count++;
                    if (_count - _fastStart < 300 || tick100)
                    {
                        _managedEntities.Clear();
                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref _combatMaxSphere, _managedEntities,
                            MyEntityQueryType.Dynamic);
                        foreach (var entity in _managedEntities)
                        {
                            var grid = entity as MyCubeGrid;
                            if ((grid != null && grid.HasBlockWithSubtypeId("LargeFlightMovement")) ||
                                grid.HasBlockWithSubtypeId("RivalAIRemoteControlLarge"))
                            {
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
            }
            catch (Exception e)
            {
                Log.Error($"Exception in UpdateAfterSimulation TryCatch 03: {e}");
            }
        }


        public override void Draw()
        {
            //if you are the server do nothing here
            if (MyAPIGateway.Utilities.IsDedicated) return;
            try
            {
                var promoLevel = MyAPIGateway.Session.PromoteLevel;

                if (MyAPIGateway.Session?.Camera != null && MyAPIGateway.Session.CameraController != null && !MyAPIGateway.Gui.ChatEntryVisible &&
                    !MyAPIGateway.Gui.IsCursorVisible && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None)
                {
                    if (MyAPIGateway.Input.IsAnyShiftKeyPressed())
                    {
                        if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.T))
                        {
                            _vState++;
                            if (_vState > ViewState.ExitView)
                                _vState = ViewState.None;
                        }

                        if (promoLevel >= MyPromoteLevel.Moderator)
                        {
                            var camMat = MyAPIGateway.Session.Camera.WorldMatrix;
                            IHitInfo hits;
                            var keyAndActionPairs = new Dictionary<MyKeys, Action>
                            {
                                {
                                    MyKeys.M, () =>
                                    {
                                        MyAPIGateway.Physics.CastRay(camMat.Translation + camMat.Forward * 0.5,
                                            camMat.Translation + camMat.Forward * 500, out hits);
                                        if (hits?.HitEntity is IMyCubeGrid)
                                        {
                                            var packet = new PacketGridData
                                            {
                                                Id = hits.HitEntity.EntityId,
                                                Value = (byte)(Tracking.Contains(hits.HitEntity.EntityId) ? 2 : 1)
                                            };
                                            Static.MyNetwork.TransmitToServer(packet);

                                            if (packet.Value == 1)
                                            {
                                                MyAPIGateway.Utilities.ShowNotification(
                                                    "ShipTracker: Added grid to tracker");
                                                Tracking.Add(hits.HitEntity.EntityId);
                                                if (!IntegretyMessage.Visible) IntegretyMessage.Visible = true;
                                                Data[hits.HitEntity.EntityId].CreateHud();
                                            }
                                            else
                                            {
                                                MyAPIGateway.Utilities.ShowNotification(
                                                    "ShipTracker: Removed grid from tracker");
                                                Tracking.Remove(hits.HitEntity.EntityId);
                                                Data[hits.HitEntity.EntityId].DisposeHud();
                                            }
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

                            foreach (var pair in keyAndActionPairs)
                                if (MyAPIGateway.Input.IsNewKeyPressed(pair.Key))
                                    pair.Value.Invoke();
                        }
                    }
                }

                _vStateP = LocalProblemSwitch == 1 ? ViewStateP.ItsOver :
                    LocalProblemSwitch == 0 ? ViewStateP.ThisIsFine : _vStateP;

                if (_textApi.Heartbeat)
                    foreach (var x in Data.Keys)
                        if (Tracking.Contains(x))
                            Data[x].UpdateHud();
                        else
                            Data[x].DisposeHud();

                if (_vStateP == ViewStateP.ItsOver && Problemmessage != null && _textApi.Heartbeat)
                {
                    const string tempText = "<color=Red>" + "A PROBLEM HAS BEEN REPORTED," + "\n" +
                                            "CHECK WITH BOTH TEAMS AND THEN TYPE '/fixed' TO CLEAR THIS MESSAGE";

                    Problemmessage.Message.Clear();
                    Problemmessage.Message.Append(tempText);
                    Problemmessage.Visible = true;
                }

                if (_vStateP == ViewStateP.ThisIsFine && Problemmessage != null && _textApi.Heartbeat)
                {
                    Problemmessage.Message.Clear();
                    Problemmessage.Visible = false;
                }

                ShiftTHandling();

                BattleShiftTHandling();

                UpdateTrackingData();

                if (_vState == ViewState.ExitView)
                {
                    if (StatMessageBattle != null && _textApi.Heartbeat)
                        if (StatMessageBattle.Visible)
                        {
                            StatMessageBattle.Message.Clear();
                            StatMessageBattleGunlist.Message.Clear();
                            StatMessageBattle.Visible = false;
                            StatMessageBattleGunlist.Visible = false;
                        }

                    _vState = ViewState.None;
                }
            }
            catch (Exception e)
            {
                Log.Error($"Exception in Draw: {e}");
            }
        }

        private void ShiftTHandling()
        {
            if (_vState != ViewState.InView || StatMessage == null || !_textApi.Heartbeat)
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
                    if (StatMessage != null && _textApi.Heartbeat && StatMessage.Visible)
                    {
                        StatMessage.Message.Clear();
                        StatMessage.Visible = false;
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
                    if (StatMessage != null && _textApi.Heartbeat && StatMessage.Visible)
                    {
                        StatMessage.Message.Clear();
                        StatMessage.Visible = false;
                    }
                }
            }
        }

        private void ShiftTCals(IMyCubeGrid icubeG)
        {
            if (PointCheckHelpers.Timer % 60 == 0)
            {
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

                var tempText = sb.ToString();
                StatMessage.Message.Clear();
                StatMessage.Message.Append(tempText);
                StatMessage.Visible = true;
            }
        }

        private void BattleShiftTHandling()
        {
            if (_vState == ViewState.InView2 && StatMessageBattle != null && _textApi.Heartbeat) //shift T menu
            {
                if (StatMessage != null && _textApi.Heartbeat)
                    if (StatMessage.Visible)
                    {
                        StatMessage.Message.Clear();
                        StatMessage.Visible = false;
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
                        if (StatMessageBattle != null && _textApi.Heartbeat && StatMessageBattle.Visible)
                        {
                            StatMessageBattle.Message.Clear();
                            StatMessageBattle.Visible = false;
                            StatMessageBattleGunlist.Message.Clear();
                            StatMessageBattleGunlist.Visible = false;
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
                        if (StatMessageBattle != null && _textApi.Heartbeat && StatMessageBattle.Visible)
                        {
                            StatMessageBattle.Message.Clear();
                            StatMessageBattle.Visible = false;
                            StatMessageBattleGunlist.Message.Clear();
                            StatMessageBattleGunlist.Visible = false;
                        }
                    }
                }
            }
        }


        private void BattleShiftTCalcs(IMyCubeGrid icubeG)
        {
            if (icubeG?.Physics != null && PointCheckHelpers.Timer % 60 == 0)
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

                StatMessageBattleGunlist.Message.Length = 0;
                StatMessageBattleGunlist.Message.Append(_gunTextBuilder);

                StatMessageBattle.Message.Length = 0;
                StatMessageBattle.Message.Append(string.Format("<color=White>{0} ({1}%)", totalShieldString,
                    (int)tracked.CurrentShieldStrength));

                StatMessageBattle.Visible = true;
                StatMessageBattleGunlist.Visible = true;
            }
        }

        private void UpdateTrackingData()
        {
            if (PointCheckHelpers.Timer % 60 == 0 && IntegretyMessage != null && _textApi.Heartbeat)
            {
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
                if (PointCheckHelpers.Timer % 240 == 0 && autotrackenabled)
                {
                    var ce = MyAPIGateway.Session.Player?.Controller?.ControlledEntity?.Entity;
                    var ck = ce as IMyCockpit;
                    var eid = ck.CubeGrid.EntityId;

                    AutoTrackPilotedShip(ck, eid);
                }

                IntegretyMessage.Message.Clear();
                IntegretyMessage.Message.Append(tt);
            }
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
            base.UnloadData();
            CommandHandler.Close();

            if (_textApi != null) _textApi.Unload();
            if (WcApi != null) WcApi.Unload();
            if (ShApi != null) ShApi.Unload();
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
    }


    public static class GridExtensions
    {
        public static bool HasBlockWithSubtypeId(this IMyCubeGrid grid, string subtypeId)
        {
            var found = false;

            grid.GetBlocks(null, delegate(IMySlimBlock block)
            {
                if (block.FatBlock != null && block.BlockDefinition.Id.SubtypeName == subtypeId)
                {
                    found = true;
                    return false; // Stop the GetBlocks iteration once a matching block is found
                }

                return false;
            });

            return found;
        }
    }
}