using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SCModRepository_Dev.Gamemode_Mods.Development.Starcore_Sharetrack_Dev.Data.Scripts.ShipPoints;

namespace Scripts.ShipPoints.HeartNetwork.Custom
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
            if (TrackedGrids != null)
                TrackingManager.I.BulkTrackGrids(TrackedGrids);
            else if (IsAddingReference ?? false)
                TrackingManager.I.TrackGrid(TrackedGrids[0], false);
            else
                TrackingManager.I.UntrackGrid(TrackedGrids[0], false);
        }
    }
}
