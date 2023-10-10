using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectedDevice.NET.Exceptions;

public class ConnectionLostException : Exception
{
    public ConnectionLostException(string message) : base(message, null)
    {
    }

    public ConnectionLostException(string message, Exception inner) : base(message, inner)
    {
    }
}
