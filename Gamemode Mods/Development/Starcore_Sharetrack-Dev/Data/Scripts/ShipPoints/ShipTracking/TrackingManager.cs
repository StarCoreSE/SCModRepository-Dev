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

        public static void UpdateAfterSimulation()
        {
            I?.Update();
        }

        public static void Close()
        {
            I?.Unload();
            I = null;
        }

        public void BulkTrackGrids(long[] gridIds)
        {
            Log.Info("Recive bulk track request!");
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

            foreach (long gridId in gridIds_List)
            {
                TrackGrid(gridId, false);
            }
        }

        public void TrackGrid(IMyCubeGrid grid, bool share = true)
        {
            if (!AllGrids.Contains(grid))
                return;
            Log.Info("Send track request!");
            ShipTracker tracker = new ShipTracker(grid);
            TrackedGrids.Add(grid, tracker);

            if (!share)
                return;

            if (MyAPIGateway.Session.IsServer)
            {
                ServerDoSync();
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
            if (!TrackedGrids.ContainsKey(grid))
                return;

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
        private readonly HashSet<ShipTracker> _queuedTrackerUpdates = new HashSet<ShipTracker>();

        private TrackingManager()
        {
            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            foreach (var entity in entities)
                OnEntityAdd(entity);
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
        }

        private void Update()
        {
            foreach (var tracker in _queuedTrackerUpdates)
            {
                tracker?.Update();
            }

            _queuedTrackerUpdates.Clear();
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
            var block = entity as IMyCubeBlock; // TODO move this into the assembly
            if (block != null)
                UpdateTrackedBlock(block);

            var grid = entity as IMyCubeGrid;
            if (grid?.Physics == null)
                return;

            AllGrids.Add(grid);

            if (_queuedGridTracks.Contains(grid.EntityId))
            {
                _queuedGridTracks.Remove(grid.EntityId);
                ShipTracker tracker = new ShipTracker(grid);
                TrackedGrids.Add(grid, tracker);
            }
        }

        private void OnEntityRemove(IMyEntity entity)
        {
            var block = entity as IMyCubeBlock;
            if (block != null)
                UpdateTrackedBlock(block);

            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;
            var grid = (IMyCubeGrid) entity;

            AllGrids.Remove(grid);

            if (TrackedGrids.ContainsKey(grid))
            {
                TrackedGrids[grid].DisposeHud();
                TrackedGrids.Remove(grid);
            }
            
            _queuedGridTracks.Remove(grid.EntityId);
        }

        private void UpdateTrackedBlock(IMyCubeBlock block)
        {
            ShipTracker tracker;
            TrackedGrids.TryGetValue(block.CubeGrid, out tracker);
            if (tracker != null)
                _queuedTrackerUpdates.Add(tracker);
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
