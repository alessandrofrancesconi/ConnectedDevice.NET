using ConnectedDevice.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ConnectedDevice.NET.Communication.Protocol
{
    public interface IMessageParser
    {
        public ServerMessage Parse(RemoteDevice sender, byte[] data);
    }
}
