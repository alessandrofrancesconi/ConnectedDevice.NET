using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Exceptions;
using ConnectedDevice.NET.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Communication
{
    public enum ConnectionType
    {
        SIMULATED,
        BLUETOOTH_CLASSIC,
        BLUETOOTH_LE,
        USB
    };

    public enum ConnectionState
    {
        DISCONNECTED,
        CONNECTED,
    }

    public enum AdapterState
    {
        MISSING,
        ON,
        OFF
    }

    public abstract class BaseCommunicatorParams
    {
        public byte[] MessageTerminator { get; set; } = null;
    }

    public abstract class BaseCommunicator
    {
        public ConnectionType ConnectionType { get; protected set; }
        public RemoteDevice? ConnectedDevice { get; protected set; }

        protected IMessageParser? ConnectedDeviceParser { get; set; }

        private List<byte> PartialReceivedData;
        private readonly object receivedDataLock = new object();

        private BaseCommunicatorParams Params;

        public BaseCommunicator(ConnectionType type, BaseCommunicatorParams p = default)
        {
            this.ConnectionType = type;
            this.Params = p;
            this.PartialReceivedData = new List<byte>();
        }

        public abstract AdapterState GetAdapterState();
        public abstract ConnectionState GetConnectionState();
        public abstract Task DiscoverDevices(CancellationToken cToken = default);

        public async Task ConnectToDevice(RemoteDevice dev, IMessageParser parser, CancellationToken cToken = default)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Start connecting to '{0}'...", dev.ToString());
            this.ConnectedDeviceParser = parser;
            await this.ConnectToDeviceNative(dev, cToken);
        }
        protected abstract Task ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default);

        public async Task DisconnectFromDevice()
        {
            if (this.ConnectedDevice == null)
            {
                ConnectedDeviceManager.PrintLog(LogLevel.Debug, "No device to disconnect from.");
                return;
            }

            ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Start disconnecting from '{0}'...", this.ConnectedDevice.ToString());
            await this.DisconnectFromDeviceNative();
        }
        protected abstract Task DisconnectFromDeviceNative(Exception? e = null);

        public virtual async Task<bool> SendData(ClientMessage message)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Sending data of type '{0}'...", message.GetType().ToString());
            MessageSentEventArgs args = null;
            try
            {
                await this.SendDataNative(message);
                args = new MessageSentEventArgs(this, message);
            }
            catch (Exception e)
            {
                ConnectedDeviceManager.PrintLog(LogLevel.Error, "Error sending data: {0}", e.Message);
                args = new MessageSentEventArgs(this, message, new MessageSentException("Error sending data", e));
            }
            finally
            {
                this.RaiseMessageSentEvent(args);
            }

            return args.Error != null;
        }
        protected abstract Task SendDataNative(ClientMessage message);

        protected void HandleReceivedData(string data)
        {
            this.HandleReceivedData(Encoding.UTF8.GetBytes(data));
        }

        protected void HandleReceivedData(byte[] data)
        {
            if (ConnectedDeviceManager.Params.Logger != null)
            {
                string utf8 = ASCIIEncoding.UTF8.GetString(data);
                ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Received data: '{0}'", utf8);
            }

            lock (this.receivedDataLock)
            {
                if (this.Params.MessageTerminator?.Length > 0)
                {

                    var startIndex = 0;
                    do
                    {
                        var subData = data.Skip(startIndex).ToArray();
                        var terminatorIndex = Utils.IndexOfBytes(subData, this.Params.MessageTerminator);
                        if (terminatorIndex > -1)
                        {
                            var dataLength = terminatorIndex + this.Params.MessageTerminator.Length;
                            var sub = new byte[dataLength];
                            Array.Copy(subData, sub, dataLength);
                            this.PartialReceivedData.AddRange(sub);

                            this.ParseAndNotifyMessage(this.PartialReceivedData.ToArray());
                            this.PartialReceivedData.Clear();
                            startIndex += dataLength;
                        }
                        else
                        {
                            // no terminator found, just append to the partial data without parsing, and exit
                            this.PartialReceivedData.AddRange(subData.ToArray());
                            break;
                        }
                    }
                    while (true);
                }
                else
                {
                    // go straigth to the parsing as no terminator is expected 
                    this.ParseAndNotifyMessage(data);
                }
            }
        }

        private void ParseAndNotifyMessage(byte[] data)
        {
            MessageReceivedEventArgs args = null;
            try
            {
                var message = this.ConnectedDeviceParser?.Parse(this.ConnectedDevice, data);
                args = new MessageReceivedEventArgs(this, message, null);
            }
            catch (Exception e)
            {
                var msg = string.Format("Error while parsing the incoming message: {0}", e.Message);
                ConnectedDeviceManager.PrintLog(LogLevel.Error, msg);
                args = new MessageReceivedEventArgs(this, null, new ProtocolException(msg, e));
            }
            finally
            {
                this.RaiseMessageReceivedEvent(args);
            }
        }

        // events raising

        protected void RaiseDeviceDiscoveredEvent(DeviceDiscoveredEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            ConnectedDeviceManager.RaiseEvent(ConnectedDeviceManager._deviceDiscovered, this, args);
        }

        protected void RaiseDiscoverDevicesFinishedEvent(DiscoverDevicesFinishedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            ConnectedDeviceManager.RaiseEvent(ConnectedDeviceManager._discoverDevicesFinished, this, args);
        }

        protected void RaiseMessageReceivedEvent(MessageReceivedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            ConnectedDeviceManager.RaiseEvent(ConnectedDeviceManager._messageReceived, this, args);
        }

        protected void RaiseMessageSentEvent(MessageSentEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            ConnectedDeviceManager.RaiseEvent(ConnectedDeviceManager._messageSent, this, args);
        }

        protected void RaiseAdapterStateChangedEvent(AdapterStateChangedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            ConnectedDeviceManager.RaiseEvent(ConnectedDeviceManager._adapterStateChanged, this, args);
        }

        protected void RaiseConnectionChangedEvent(ConnectionChangedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            ConnectedDeviceManager.RaiseEvent(ConnectedDeviceManager._connectionChanged, this, args);
        }
    }
}
