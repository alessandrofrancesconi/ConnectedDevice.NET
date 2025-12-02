using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Models
{
    public class RemoteDevice
    {
        public string Address { get; set; }
        public string DisplayName { get; set; }

        public RemoteDevice(string address)
        {
            Address = address;
        }

        public RemoteDevice(string address, string name) : this(address)
        {
            DisplayName = name;
        }

        public override string ToString()
        {
            return String.Format("[{0}] Address: {1}, DisplayName: {2}",
                this.GetType().ToString(),
                this.Address, 
                this.DisplayName
            );
        }

        public bool Equals(RemoteDevice other)
        {
            return
                (other != null) &&
                (this.Address == other.Address);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Address);
        }
    }
}
