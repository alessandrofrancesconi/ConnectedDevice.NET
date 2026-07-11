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
    public partial class DeviceCommunicator
    {
        // events
        internal EventHandler<DeviceDiscoveredEventArgs> _deviceDiscovered;
        public event EventHandler<DeviceDiscoveredEventArgs> DeviceDiscovered
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

        internal EventHandler<DiscoverDevicesFinishedEventArgs> _discoverDevicesFinished;
        public event EventHandler<DiscoverDevicesFinishedEventArgs> DiscoverDevicesFinished
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

        internal EventHandler<ConnectionChangedEventArgs> _connectionChanged;
        public event EventHandler<ConnectionChangedEventArgs> ConnectionChanged
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

        internal EventHandler<AdapterStateChangedEventArgs> _adapterStateChanged;
        public event EventHandler<AdapterStateChangedEventArgs> AdapterStateChanged
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

        internal EventHandler<MessageReceivedEventArgs> _messageReceived;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived
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

        internal EventHandler<MessageSentEventArgs> _messageSent;
        public event EventHandler<MessageSentEventArgs> MessageSent
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

        internal void RaiseEvent<T>(EventHandler<T> handler, DeviceCommunicator source, T args) where T : EventArgs
        {
            handler?.Invoke(source, args);
        }
    }

    public class BaseEventArgs : EventArgs
    {
        public DeviceCommunicator SourceCommunicator;

        public BaseEventArgs(DeviceCommunicator s)
        {
            SourceCommunicator = s;
        }
    }

    public class AdapterStateChangedEventArgs : BaseEventArgs
    {
        public AdapterState NewState;

        public AdapterStateChangedEventArgs(DeviceCommunicator s, AdapterState ns) : base(s)
        {
            this.NewState = ns;
        }
    }
    public class DeviceDiscoveredEventArgs : BaseEventArgs
    {
        public RemoteDevice DiscoveredDevice;

        public DeviceDiscoveredEventArgs(DeviceCommunicator s, RemoteDevice d) : base(s)
        {
            this.DiscoveredDevice = d;
        }
    }

    public class DiscoverDevicesFinishedEventArgs : BaseEventArgs
    {
        public Exception? Error;

        public DiscoverDevicesFinishedEventArgs(DeviceCommunicator s, Exception? e) : base(s)
        {
            this.Error = e;
        }
    }

    public class ConnectionChangedEventArgs : BaseEventArgs
    {
        public ConnectionState NewState;
        public Exception? Error;

        public ConnectionChangedEventArgs(DeviceCommunicator s, ConnectionState ns, Exception? e) : base(s)
        {
            this.NewState = ns;
            this.Error = e;
        }
    }

    public class DataSendFinishedEventArgs : BaseEventArgs
    {
        public Guid TransmissionId;
        public Exception? Error;

        public DataSendFinishedEventArgs(DeviceCommunicator s, Guid t, Exception? e) : base(s)
        {
            this.TransmissionId = t;
            this.Error = e;
        }
    }

    public class MessageReceivedEventArgs : BaseEventArgs
    {
        public DateTime ReceiveTime;
        public ServerMessage Message;
        public Exception? Error;

        public MessageReceivedEventArgs(DeviceCommunicator s, ServerMessage m, Exception? e = null) : base(s)
        {
            this.Message = m;
            this.Error = e;
        }
    }

    public class MessageSentEventArgs : BaseEventArgs
    {
        public DateTime SendTimeStart, SendTimeEnd;
        public ClientMessage Message;
        public Exception? Error;

        public MessageSentEventArgs(DeviceCommunicator s, ClientMessage m, Exception? e = null) : base(s)
        {
            this.Message = m;
            this.Error = e;
        }
    }

}
