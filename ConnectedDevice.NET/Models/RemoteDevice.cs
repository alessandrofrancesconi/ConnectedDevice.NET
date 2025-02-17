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

        public RemoteDevice(string address)
        {
            Address = address;
        }

        public override string ToString()
        {
            return String.Format("[{0}] Address: {1}",
                this.GetType().ToString(),
                this.Address
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
