using ConnectedDevice.NET.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Models
{
    public class UsbDevice : RemoteDevice
    {
        public UsbDevice(string address) : base(ConnectionType.USB, address)
        {

        }
    }
}
