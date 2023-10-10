using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Communication.Protocol
{
    public class ServerMessage
    {
        public byte[] Data;

        public override string? ToString()
        {
            return ASCIIEncoding.ASCII.GetString(Data);
        }
    }
}
