using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using Scripts.ShipPoints.HeartNetwork.Custom;

namespace Scripts.ShipPoints.HeartNetwork
{
    [ProtoInclude(1, typeof(TrackingSyncPacket))]

    [ProtoContract(UseProtoMembersOnly = true)]
    public abstract partial class PacketBase
    {
        /// <summary>
        /// Called whenever your packet is recieved.
        /// </summary>
        /// <param name="SenderSteamId"></param>
        public abstract void Received(ulong SenderSteamId);

        public static Type[] Types = {
            typeof(PacketBase),
            typeof(TrackingSyncPacket),
        };
    }
}