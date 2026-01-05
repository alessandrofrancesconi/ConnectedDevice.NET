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

namespace ConnectedDevice.NET
{
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

    public class DeviceCommunicatorParams
    {
        public ILogger? Logger { get; set; } = null;
        public byte[] MessageTerminator { get; set; } = null;
        public Func<RemoteDevice, bool>? DeviceFilter { get; set; } = null;
    }

    public abstract partial class DeviceCommunicator : IDisposable
    {
        public RemoteDevice? ConnectedDevice { get; protected set; }

        protected IMessageParser? ConnectedDeviceParser { get; set; }

        private List<byte> PartialReceivedData;
        private readonly object receivedDataLock = new object();

        public DeviceCommunicatorParams Params;

        public DeviceCommunicator(DeviceCommunicatorParams? p = null)
        {
            Params = p;
            if (Params == null) Params = new DeviceCommunicatorParams();

            PartialReceivedData = new List<byte>();
        }

        public abstract string GetInterfaceName();
        public abstract AdapterState GetAdapterState();
        public abstract ConnectionState GetConnectionState();
        public abstract Task DiscoverDevices(CancellationToken cToken = default);

        public abstract void Dispose();

        public async Task ConnectToDevice(RemoteDevice dev, IMessageParser parser, CancellationToken cToken = default) 
        {
            this.PrintLog(LogLevel.Debug, "Start connecting to '{0}'...", dev.ToString());
            ConnectedDeviceParser = parser;
            await ConnectToDeviceNative(dev, cToken);
        }
        protected abstract Task ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default);

        public async Task DisconnectFromDevice(Exception? externalException = null)
        {
            if (ConnectedDevice == null)
            {
                this.PrintLog(LogLevel.Debug, "No device to disconnect from.");
                return;
            }

            this.PrintLog(LogLevel.Debug, "Start disconnecting from '{0}'...", ConnectedDevice.ToString());
            await DisconnectFromDeviceNative(externalException);
        }
        protected abstract Task DisconnectFromDeviceNative(Exception? e = null);

        public async Task<bool> SendData(ClientMessage message)
        {
            var valueStr = string.Empty;
            foreach (var d in message.Data)
            {
                valueStr += d + "[" + Convert.ToChar(d) + "]";
            }
            this.PrintLog(LogLevel.Debug, "Sending message of type '{0}' with data '{1}'", message.GetType().ToString(), valueStr);
            MessageSentEventArgs args = null;
            try
            {
                args = new MessageSentEventArgs(this, message);
                args.SendTimeStart = DateTime.Now;
                await SendDataNative(message);
                args.SendTimeEnd = DateTime.Now;
            }
            catch (Exception e)
            {
                this.PrintLog(LogLevel.Error, "Error sending data: {0}", e.Message);
                args = new MessageSentEventArgs(this, message, new MessageSentException("Error sending data", e));
            }
            finally
            {
                RaiseMessageSentEvent(args);
            }

            return args.Error == null;
        }
        protected abstract Task SendDataNative(ClientMessage message);

        protected void HandleReceivedData(string data)
        {
            HandleReceivedData(Encoding.UTF8.GetBytes(data));
        }

        protected void HandleReceivedData(byte[] data)
        {
            string utf8 = Encoding.UTF8.GetString(data);
            this.PrintLog(LogLevel.Debug, "Received data: '{0}'", utf8);

            lock (receivedDataLock)
            {
                if (Params.MessageTerminator?.Length > 0)
                {

                    var startIndex = 0;
                    do
                    {
                        var subData = data.Skip(startIndex).ToArray();
                        var terminatorIndex = Utils.IndexOfBytes(subData, Params.MessageTerminator);
                        if (terminatorIndex > -1)
                        {
                            var dataLength = terminatorIndex + Params.MessageTerminator.Length;
                            var sub = new byte[dataLength];
                            Array.Copy(subData, sub, dataLength);
                            PartialReceivedData.AddRange(sub);

                            ParseAndNotifyMessage(PartialReceivedData.ToArray(), DateTime.Now);
                            PartialReceivedData.Clear();
                            startIndex += dataLength;
                        }
                        else
                        {
                            // no terminator found, just append to the partial data without parsing, and exit
                            PartialReceivedData.AddRange(subData.ToArray());
                            break;
                        }
                    }
                    while (true);
                }
                else
                {
                    // go straigth to the parsing as no terminator is expected 
                    ParseAndNotifyMessage(data, DateTime.Now);
                }
            }
        }

        private void ParseAndNotifyMessage(byte[] data, DateTime messageTime)
        {
            MessageReceivedEventArgs args = null;
            if (this.ConnectedDeviceParser != null)
            {
                try
                {
                    var message = ConnectedDeviceParser.Parse(ConnectedDevice, data);
                    args = new MessageReceivedEventArgs(this, message, null);
                    args.ReceiveTime = messageTime;
                }
                catch (Exception e)
                {
                    var msg = string.Format("Error while parsing the incoming message: {0}", e.Message);
                    this.PrintLog(LogLevel.Error, msg);
                    args = new MessageReceivedEventArgs(this, null, new ProtocolException(msg, e));
                }
            }
            else
            {
                args = new MessageReceivedEventArgs(this, new ServerMessage()
                {
                    Data = data
                });
                args.ReceiveTime = messageTime;
            }

            RaiseMessageReceivedEvent(args);
        }

        // events raising

        protected void RaiseDeviceDiscoveredEvent(DeviceDiscoveredEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            DeviceCommunicator.RaiseEvent(DeviceCommunicator._deviceDiscovered, this, args);
        }

        protected void RaiseDiscoverDevicesFinishedEvent(DiscoverDevicesFinishedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            DeviceCommunicator.RaiseEvent(DeviceCommunicator._discoverDevicesFinished, this, args);
        }

        protected void RaiseMessageReceivedEvent(MessageReceivedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            DeviceCommunicator.RaiseEvent(DeviceCommunicator._messageReceived, this, args);
        }

        protected void RaiseMessageSentEvent(MessageSentEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            DeviceCommunicator.RaiseEvent(DeviceCommunicator._messageSent, this, args);
        }

        protected void RaiseAdapterStateChangedEvent(AdapterStateChangedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            DeviceCommunicator.RaiseEvent(DeviceCommunicator._adapterStateChanged, this, args);
        }

        protected void RaiseConnectionChangedEvent(ConnectionChangedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            DeviceCommunicator.RaiseEvent(DeviceCommunicator._connectionChanged, this, args);
        }

        protected void PrintLog(LogLevel level, string message, params object?[] args)
        {
            this.Params.Logger?.Log(level, "[" + this.GetType().Name + "] " + message, args);
        }
    }
}
