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
        public UsbDevice(string port) : base(ConnectionType.USB, port)
        {

        }

        public override string ToString()
        {
            return String.Format("[{0}] ConnectionType: USB, Port: {1}",
                this.GetType().ToString(),
                this.Address
            );
        }
    }
}
