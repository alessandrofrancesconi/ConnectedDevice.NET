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
    public struct ConnectedDeviceManagerParams
    {
        public ILogger? Logger { get; set; } = null;

        public ConnectedDeviceManagerParams() { }
    }

    public static partial class ConnectedDeviceManager
    {
        private static Dictionary<ConnectionType, BaseCommunicator> AvailableCommunicators = new Dictionary<ConnectionType, BaseCommunicator>();
        private static BaseCommunicator? CurrentCommunicator;

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

            AvailableCommunicators[type] = comm;
            return comm;
        }

        public static BaseCommunicator GetCommunicator(ConnectionType type)
        {
            if (Initialized == false) 
                throw new InvalidOperationException("You must call Initialize() before using this method");

            if (!AvailableCommunicators.ContainsKey(type) || AvailableCommunicators[type] == null)
                throw new ArgumentException(string.Format("Communicator of type '{0}' has not been declared", type.ToString()));

            return AvailableCommunicators[type];
        }

        public static BaseCommunicator GetCurrentCommunicator()
        {
            return CurrentCommunicator;
        }

        public static BaseCommunicator SetCurrentCommunicator(ConnectionType type)
        {
            CurrentCommunicator = GetCommunicator(type);
            return CurrentCommunicator;
        }

        public static void PrintLog(LogLevel level, string message, params object?[] args)
        {
            Params.Logger?.Log(level, "[ConnectedDevice.NET] " + message, args);
        }

        public static RemoteDevice? GetConnectedDevice()
        {
            return CurrentCommunicator?.ConnectedDevice;
        }

        public static T? GetConnectedDevice<T>() where T : RemoteDevice
        {
            var device = CurrentCommunicator?.ConnectedDevice;
            if (device != null && device is T) return (T)device;
            else return null;
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
