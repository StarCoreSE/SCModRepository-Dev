using ProtoBuf;
using static Math0424.Networking.MyEasyNetworkManager;

namespace ShipPoints.Data.Scripts.ShipPoints.Networking
{
    [ProtoContract]
    internal class TimerPacket : ITPacket
    {
        [ProtoMember(1)] public int ServerTime;

        public TimerPacket()
        {
        } // Empty constructor required for deserialization

        public TimerPacket(int serverTime)
        {
            this.ServerTime = serverTime;
        }

        public int GetTime()
        {
            return ServerTime;
        }
    }
}