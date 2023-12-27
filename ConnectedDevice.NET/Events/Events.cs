using ConnectedDevice.NET.Communication;
using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Exceptions;
using ConnectedDevice.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET
{
    public static partial class ConnectedDeviceManager
    {
        // events
        internal static EventHandler<DeviceDiscoveredEventArgs> _deviceDiscovered;
        public static event EventHandler<DeviceDiscoveredEventArgs> DeviceDiscovered
        {
            add
            {
                _deviceDiscovered -= value;
                _deviceDiscovered += value;
            }

            remove
            {
                _deviceDiscovered -= value;
            }
        }

        internal static EventHandler<DiscoverDevicesFinishedEventArgs> _discoverDevicesFinished;
        public static event EventHandler<DiscoverDevicesFinishedEventArgs> DiscoverDevicesFinished
        {
            add
            {
                _discoverDevicesFinished -= value;
                _discoverDevicesFinished += value;
            }

            remove
            {
                _discoverDevicesFinished -= value;
            }
        }

        internal static EventHandler<ConnectionChangedEventArgs> _connectionChanged;
        public static event EventHandler<ConnectionChangedEventArgs> ConnectionChanged
        {
            add
            {
                _connectionChanged -= value;
                _connectionChanged += value;
            }

            remove
            {
                _connectionChanged -= value;
            }
        }

        internal static EventHandler<AdapterStateChangedEventArgs> _adapterStateChanged;
        public static event EventHandler<AdapterStateChangedEventArgs> AdapterStateChanged
        {
            add
            {
                _adapterStateChanged -= value;
                _adapterStateChanged += value;
            }

            remove
            {
                _adapterStateChanged -= value;
            }
        }

        internal static EventHandler<MessageReceivedEventArgs> _messageReceived;
        public static event EventHandler<MessageReceivedEventArgs> MessageReceived
        {
            add
            {
                _messageReceived -= value;
                _messageReceived += value;
            }

            remove
            {
                _messageReceived -= value;
            }
        }

        internal static EventHandler<MessageSentEventArgs> _messageSent;
        public static event EventHandler<MessageSentEventArgs> MessageSent
        {
            add
            {
                _messageSent -= value;
                _messageSent += value;
            }

            remove
            {
                _messageSent -= value;
            }
        }

        internal static void RaiseEvent<T>(EventHandler<T> handler, BaseCommunicator source, T args) where T : EventArgs
        {
            handler?.Invoke(source, args);
        }
    }

    public class BaseEventArgs : EventArgs
    {
        public BaseCommunicator Source;

        public BaseEventArgs(BaseCommunicator s)
        {
            Source = s;
        }
    }

    public class AdapterStateChangedEventArgs : BaseEventArgs
    {
        public AdapterState NewState;

        public AdapterStateChangedEventArgs(BaseCommunicator s, AdapterState ns) : base(s)
        {
            this.NewState = ns;
        }
    }
    public class DeviceDiscoveredEventArgs : BaseEventArgs
    {
        public RemoteDevice DiscoveredDevice;

        public DeviceDiscoveredEventArgs(BaseCommunicator s, RemoteDevice d) : base(s)
        {
            this.DiscoveredDevice = d;
        }
    }

    public class DiscoverDevicesFinishedEventArgs : BaseEventArgs
    {
        public Exception? Error;

        public DiscoverDevicesFinishedEventArgs(BaseCommunicator s, Exception? e) : base(s)
        {
            this.Error = e;
        }
    }

    public class ConnectionChangedEventArgs : BaseEventArgs
    {
        public ConnectionState NewState;
        public Exception? Error;

        public ConnectionChangedEventArgs(BaseCommunicator s, ConnectionState ns, Exception? e) : base(s)
        {
            this.NewState = ns;
            this.Error = e;
        }
    }

    public class DataSendFinishedEventArgs : BaseEventArgs
    {
        public Guid TransmissionId;
        public Exception? Error;

        public DataSendFinishedEventArgs(BaseCommunicator s, Guid t, Exception? e) : base(s)
        {
            this.TransmissionId = t;
            this.Error = e;
        }
    }

    public class MessageReceivedEventArgs : BaseEventArgs
    {
        public ServerMessage Message;
        public Exception? Error;

        public MessageReceivedEventArgs(BaseCommunicator s, ServerMessage m, Exception? e = null) : base(s)
        {
            this.Message = m;
            this.Error = e;
        }
    }

    public class MessageSentEventArgs : BaseEventArgs
    {
        public ClientMessage Message;
        public Exception? Error;

        public MessageSentEventArgs(BaseCommunicator s, ClientMessage m, Exception? e = null) : base(s)
        {
            this.Message = m;
            this.Error = e;
        }
    }

}
