﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Exceptions
{
    public class ProtocolException : Exception
    {
        public ProtocolException(string? message) : base(message)
        {
        }

        public ProtocolException(string message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
