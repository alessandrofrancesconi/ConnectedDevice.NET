using ConnectedDevice.NET.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Models
{
    public class RemoteDevice
    {
        public ConnectionType ConnectionType;
        public string Name;
        public string Address;

        public RemoteDevice(ConnectionType connectionType, string name, string address)
        {
            ConnectionType = connectionType;
            Name = name;
            Address = address;
        }

        public override string ToString()
        {
            return String.Format("[{0}] ConnectionType: {1}, Name: {2}, Address: {3}",
                this.GetType().ToString(),
                this.ConnectionType.ToString(),
                this.Name,
                this.Address
            );
        }

        public bool Equals(RemoteDevice other)
        {
            return
                (other != null) &&
                (this.ConnectionType == other.ConnectionType) &&
                (this.Address == other.Address);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.ConnectionType, this.Address);
        }
    }
}
