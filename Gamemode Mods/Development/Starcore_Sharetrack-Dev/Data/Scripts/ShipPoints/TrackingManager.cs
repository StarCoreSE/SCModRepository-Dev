using System;
using System.Collections.Generic;
using System.Net;
using klime.PointCheck;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Scripts.ShipPoints.HeartNetwork;
using Scripts.ShipPoints.HeartNetwork.Custom;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace SCModRepository_Dev.Gamemode_Mods.Development.Starcore_Sharetrack_Dev.Data.Scripts.ShipPoints
{
    internal class TrackingManager
    {
        public static TrackingManager I;

        #region Public Methods

        public static void Init()
        {
            I = new TrackingManager();
        }

        public static void Close()
        {
            I?.Unload();
            I = null;
        }

        public void BulkTrackGrids(long[] gridIds)
        {
            List<long> gridIds_List = new List<long>(gridIds);
            foreach (var grid in AllGrids)
            {
                if (gridIds.Contains(grid.EntityId))
                {
                    gridIds_List.Remove(grid.EntityId);
                    continue;
                }
                UntrackGrid(grid, false);
            }

            foreach (long gridId in gridIds)
            {
                TrackGrid(gridId, false);
            }
        }

        public void TrackGrid(IMyCubeGrid grid, bool share = true)
        {
            if (!AllGrids.Contains(grid))
                return;
            ShipTracker tracker = new ShipTracker(grid);
            TrackedGrids.Add(grid, tracker);
            tracker.CreateHud();

            if (!share)
                return;

            if (MyAPIGateway.Session.IsServer)
            {
                TrackingSyncPacket packet = new TrackingSyncPacket(GetGridIds());
                HeartNetwork.I.SendToEveryone(packet);
            }
            else
            {
                TrackingSyncPacket packet = new TrackingSyncPacket(grid.EntityId, true);
                HeartNetwork.I.SendToServer(packet);
            }
        }

        public void TrackGrid(long gridId, bool share = true)
        {
            IMyCubeGrid grid = MyAPIGateway.Entities.GetEntityById(gridId) as IMyCubeGrid;
            if (grid == null)
            {
                _queuedGridTracks.Add(gridId);
                return;
            }
            TrackGrid(grid, share);
        }

        public void UntrackGrid(IMyCubeGrid grid, bool share = true)
        {
            TrackedGrids[grid].DisposeHud();
            TrackedGrids.Remove(grid);

            if (!share)
                return;

            if (MyAPIGateway.Session.IsServer)
            {
                ServerDoSync();
            }
            else
            {
                TrackingSyncPacket packet = new TrackingSyncPacket(grid.EntityId, false);
                HeartNetwork.I.SendToServer(packet);
            }
        }

        public void UntrackGrid(long gridId, bool share = true)
        {
            IMyCubeGrid grid = MyAPIGateway.Entities.GetEntityById(gridId) as IMyCubeGrid;
            _queuedGridTracks.Remove(gridId);
            if (grid != null)
                UntrackGrid(grid, share);
        }

        public bool IsGridTracked(IMyCubeGrid grid)
        {
            return TrackedGrids.ContainsKey(grid);
        }

        public void ServerDoSync()
        {
            TrackingSyncPacket packet = new TrackingSyncPacket(GetGridIds());
            HeartNetwork.I.SendToEveryone(packet);
        }

        #endregion

        public HashSet<IMyCubeGrid> AllGrids = new HashSet<IMyCubeGrid>();
        public Dictionary<IMyCubeGrid, ShipTracker> TrackedGrids = new Dictionary<IMyCubeGrid, ShipTracker>();
        private readonly HashSet<long> _queuedGridTracks = new HashSet<long>();

        private TrackingManager()
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
        }

        private void Unload()
        {
            AllGrids.Clear();
            foreach (var tracker in TrackedGrids.Values)
                tracker.DisposeHud();
            TrackedGrids.Clear();

            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;
        }

        private void OnEntityAdd(IMyEntity entity)
        {
            var grid = entity as IMyCubeGrid;
            if (grid?.Physics == null)
                return;

            AllGrids.Add(grid);

            if (_queuedGridTracks.Contains(grid.EntityId))
            {
                _queuedGridTracks.Remove(grid.EntityId);
                ShipTracker tracker = new ShipTracker(grid);
                TrackedGrids.Add(grid, tracker);
                tracker.CreateHud();
            }
        }

        private void OnEntityRemove(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;
            var grid = (IMyCubeGrid) entity;

            AllGrids.Remove(grid);
            TrackedGrids[grid].DisposeHud();
            TrackedGrids.Remove(grid);
            _queuedGridTracks.Remove(grid.EntityId);
        }

        private long[] GetGridIds()
        {
            var gridIds = new List<long>();
            foreach (var grid in TrackedGrids.Keys)
            {
                gridIds.Add(grid.EntityId);
            }
            return gridIds.ToArray();
        }
    }
}
