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
        public ConnectionType ConnectionType { get; set; }
        public string Address { get; set; }

        public RemoteDevice(ConnectionType connectionType, string address)
        {
            ConnectionType = connectionType;
            Address = address;
        }

        public override string ToString()
        {
            return String.Format("[{0}] ConnectionType: {1}, Address: {2}",
                this.GetType().ToString(),
                this.ConnectionType.ToString(),
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
