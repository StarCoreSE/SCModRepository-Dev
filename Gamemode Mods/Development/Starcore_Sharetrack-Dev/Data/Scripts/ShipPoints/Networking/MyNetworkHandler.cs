using System;
using System.Collections.Generic;
using klime.PointCheck;
using Math0424.ShipPoints;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SCModRepository_Dev.Gamemode_Mods.Development.Starcore_Sharetrack_Dev.Data.Scripts.ShipPoints;
using VRage.Game.ModAPI;
using VRage.Utils;
using static Math0424.Networking.MyEasyNetworkManager;

namespace Math0424.Networking
{
    internal class MyNetworkHandler : IDisposable
    {
        public static MyNetworkHandler Static;
        private static readonly List<ulong> AllPlayers = new List<ulong>();
        private static readonly List<IMyPlayer> ListPlayers = new List<IMyPlayer>();

        public MyEasyNetworkManager MyNetwork;

        protected MyNetworkHandler()
        {
            MyNetwork = new MyEasyNetworkManager(45674);
            MyNetwork.Register();

            MyNetwork.OnRecievedPacket += PacketIn;
        }

        public void Dispose()
        {
            MyNetwork.UnRegister();
            MyNetwork = null;
            Static = null;
        }

        public static void Init()
        {
            if (Static == null) Static = new MyNetworkHandler();
        }

        private void PacketIn(PacketIn e)
        {
            if (e.PacketId == 45) // SyncRequestPacket
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    TrackingManager.I.ServerDoSync();
                }

            if (e.PacketId == 1)
            {
                //inject for shared list

                AllPlayers.Clear();
                ListPlayers.Clear();
                MyAPIGateway.Players.GetPlayers(ListPlayers);
                foreach (var p in ListPlayers) AllPlayers.Add(p.SteamUserId);
                //end


                var packet = e.UnWrap<PacketGridData>();
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    var x = MyEntities.GetEntityById(packet.Id);
                    if (x != null && x is IMyCubeGrid)
                    {
                        if (packet.Value == 1) //add
                        {
                            //if (packet.value == 1 && MyAPIGateway.Session.IsUserAdmin(e.SenderId))
                            if (PointCheck.Sending.ContainsKey(packet.Id))
                                try
                                {
                                    PointCheck.Sending[packet.Id].Remove(e.SenderId);
                                }
                                catch
                                {
                                }
                            else
                                PointCheck.Sending.Add(packet.Id, new List<ulong>());

                            //option 1
                            //PointCheck.Sending[packet.id].Add(e.SenderId);

                            foreach (var p in AllPlayers) PointCheck.Sending[packet.Id].Add(p);
                        }

                    }
                }
                else
                {

                }
            }

            if (e.PacketId == 5)
                if (MyAPIGateway.Session.IsUserAdmin(e.SenderId))
                {
                    foreach (var g in MyEntities.GetEntities())
                        if (g != null && !g.MarkedForClose && g is MyCubeGrid)
                        {
                            var grid = g as MyCubeGrid;
                            var block = PointCheck.I.ShApi.GetShieldBlock(grid);
                            if (block != null) PointCheck.I.ShApi.SetCharge(block, 99999999999);
                        }

                    MyAPIGateway.Utilities.ShowMessage("Shields", "Charged");
                }

            if (e.PacketId == 6) PointCheck.Begin();

            if (e.PacketId == 7)
            {
                // PointCheck.TrackYourselfMyMan();
            }

            if (e.PacketId == 8) PointCheck.EndMatch();


            if (e.PacketId == 17) PointCheck.There_Is_A_Problem();
            if (e.PacketId == 18) PointCheck.There_Is_A_Solution();
        }
    }
}