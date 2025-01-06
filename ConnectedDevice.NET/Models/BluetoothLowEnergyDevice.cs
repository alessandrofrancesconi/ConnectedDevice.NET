using ConnectedDevice.NET.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Models
{
    public class BluetoothLowEnergyDevice : RemoteDevice
    {
        public string Name { get; set; }

        public BluetoothLowEnergyDevice(string address, string name) : base(ConnectionType.BLUETOOTH_LE, address)
        {
            Name = name;
        }

        public override string ToString()
        {
            return String.Format("[{0}] ConnectionType: {1}, Address: {2}, Name: {3}",
                this.GetType().ToString(),
                this.ConnectionType.ToString(),
                this.Address,
                this.Name
            );
        }
    }
}
