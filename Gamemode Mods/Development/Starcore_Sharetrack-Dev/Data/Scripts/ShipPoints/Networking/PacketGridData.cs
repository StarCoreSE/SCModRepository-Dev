using klime.PointCheck;
using ProtoBuf;
using static Math0424.Networking.MyEasyNetworkManager;

namespace Math0424.ShipPoints
{
    [ProtoContract]
    public class PacketGridData : IPacket
    {
        [ProtoMember(2)] public long Id;
        [ProtoMember(3)] public ShipTracker Tracked;
        [ProtoMember(1)] public byte Value;

        public PacketGridData()
        {
            Tracked = new ShipTracker();
        }

        public int GetId()
        {
            return 1;
        }
    }
}