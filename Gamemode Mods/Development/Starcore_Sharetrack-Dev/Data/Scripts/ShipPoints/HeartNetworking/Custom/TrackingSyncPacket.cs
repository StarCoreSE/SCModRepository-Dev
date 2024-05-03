﻿using ProtoBuf;
using ShipPoints.ShipTracking;
using System;
using Sandbox.ModAPI;

namespace ShipPoints.HeartNetworking.Custom
{
    /// <summary>
    /// Packet used for syncing tracked grids.
    /// </summary>
    [ProtoContract]
    internal class TrackingSyncPacket : PacketBase
    {
        [ProtoMember(21)] public long[] TrackedGrids;
        [ProtoMember(22)] public bool? IsAddingReference;

        public TrackingSyncPacket() { }

        public TrackingSyncPacket(long[] trackedGrids)
        {
            TrackedGrids = trackedGrids;
        }

        public TrackingSyncPacket(long referenceGrid, bool isAddingReference)
        {
            TrackedGrids = new[] { referenceGrid };
            IsAddingReference = isAddingReference;
        }

        public override void Received(ulong SenderSteamId)
        {
            if (TrackedGrids == null)
            {
                Log.Info("Null TrackedGrids!");
                TrackedGrids = Array.Empty<long>();
            }

            if (IsAddingReference == null)
                TrackingManager.I.BulkTrackGrids(TrackedGrids);
            else if ((bool) IsAddingReference)
                TrackingManager.I.TrackGrid(TrackedGrids[0], MyAPIGateway.Session.IsServer);
            else
                TrackingManager.I.UntrackGrid(TrackedGrids[0], MyAPIGateway.Session.IsServer);
            Log.Info("Receive track request! " + (IsAddingReference == null));
        }
    }
}