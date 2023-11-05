using ConnectedDevice.NET.Communication;
using ConnectedDevice.NET.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET
{
    public class ConnectedDeviceManagerParams
    {
        public ILogger? Logger { get; set; } = null;
    }

    public static partial class ConnectedDeviceManager
    {
        private static Dictionary<ConnectionType, BaseCommunicator> Communicators = new Dictionary<ConnectionType, BaseCommunicator>();
        internal static ConnectedDeviceManagerParams Params = new ConnectedDeviceManagerParams();
        internal static bool Initialized = false;

        public static void Initialize(ConnectedDeviceManagerParams parameters = default)
        {
            Params = parameters;
            Initialized = true;
            PrintLog(LogLevel.Debug, "Initialized");
        }

        public static BaseCommunicator SetCommunicator(ConnectionType type, BaseCommunicator comm)
        {
            if (Initialized == false) 
                throw new InvalidOperationException("You must call Initialize() before using this method");

            Communicators[type] = comm;
            return comm;
        }

        public static BaseCommunicator GetCommunicator(ConnectionType type)
        {
            if (Initialized == false) 
                throw new InvalidOperationException("You must call Initialize() before using this method");

            if (!Communicators.ContainsKey(type) || Communicators[type] == null)
                throw new ArgumentException("Communicator of type '{0}' does not exist", type.ToString());

            return Communicators[type];
        }

        public static void PrintLog(LogLevel level, string message, params object?[] args)
        {
            Params.Logger?.Log(level, "[ConnectedDevice.NET] " + message, args);
        }

        public static RemoteDevice? GetConnectedDevice(ConnectionType type)
        {
            if (Initialized == false)
                throw new InvalidOperationException("You must call Initialize() before using this method");

            var communicator = GetCommunicator(type);
            return communicator.ConnectedDevice;
        }
    }
}
