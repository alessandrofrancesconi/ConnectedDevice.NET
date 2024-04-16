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

public class NotConnectedException : Exception
{
    public NotConnectedException(string message) : base(message, null)
    {
    }

    public NotConnectedException(string message, Exception inner) : base(message, inner)
    {
    }
}
