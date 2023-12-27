using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectedDevice.NET.Exceptions;

public class MessageSentException : Exception
{
    public MessageSentException(string message) : base(message, null)
    {
    }

    public MessageSentException(string message, Exception inner) : base(message, inner)
    {
    }
}

public class ConnectionLostException : Exception
{
    public ConnectionLostException(string message) : base(message, null)
    {
    }

    public ConnectionLostException(string message, Exception inner) : base(message, inner)
    {
    }
}
