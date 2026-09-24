using ConnectedDevice.NET.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Exceptions
{
    public class PermissionPendingException : PlatformException
    {
        public PermissionPendingException(string message) : base(message, null)
        {
        }
    }
}
