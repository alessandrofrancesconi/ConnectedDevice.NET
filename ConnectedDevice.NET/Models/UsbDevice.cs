using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Models
{
    public class UsbDevice : RemoteDevice
    {
        public UsbDevice(string port) : base(port)
        {

        }

        public override string ToString()
        {
            return String.Format("[{0}] Port: {1}",
                this.GetType().ToString(),
                this.Address
            );
        }
    }
}
