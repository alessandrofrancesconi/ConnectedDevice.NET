using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectedDevice.NET.Exceptions;

public class PlatformException : Exception
{
    public PlatformException(string message) : base(message, null)
    {
    }

    public PlatformException(string message, Exception inner) : base(message, inner)
    {
    }
}
