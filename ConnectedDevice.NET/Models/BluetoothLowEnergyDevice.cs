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

        public BluetoothLowEnergyDevice(string address, string name) : base(address)
        {
            Name = name;
        }

        public override string ToString()
        {
            return String.Format("[{0}] ConnectionType: BLE, Address: {1}, Name: {2}",
                this.GetType().ToString(),
                this.Address,
                this.Name
            );
        }
    }
}
