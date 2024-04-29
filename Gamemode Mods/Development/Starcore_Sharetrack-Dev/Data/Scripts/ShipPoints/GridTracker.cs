using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCModRepository_Dev.Gamemode_Mods.Development.Starcore_Sharetrack_Dev.Data.Scripts.ShipPoints
{
    [ProtoContract]
    internal class GridTracker
    {
        [ProtoMember(1)] public long EntityId;
    }
}
