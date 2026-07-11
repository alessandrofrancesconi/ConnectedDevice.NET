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

        private TaskCompletionSource<ServerMessage?>? receiveWaiter;
        private readonly object receiveWaiterLock = new object();

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
                args.SendTimeStart = DateTime.UtcNow;
                await SendDataNative(message);
                args.SendTimeEnd = DateTime.UtcNow;
                this.RaiseMessageSentEvent(args);
            }
            catch (NotConnectedException e)
            {
                this.PrintLog(LogLevel.Error, "Error sending data: {0}", e.Message);
                this.DisconnectFromDevice(e);
                return false;
            }
            catch (Exception e)
            {
                this.PrintLog(LogLevel.Error, "Error sending data: {0}", e.Message);
                args = new MessageSentEventArgs(this, message, new MessageSentException("Error sending data", e));
                this.RaiseMessageSentEvent(args);
            }

            return args.Error == null;
        }
        
        public async Task<ServerMessage?> SendDataAndWait(ClientMessage message, TimeSpan timeout, CancellationToken cToken = default)
        {
            var tcs = new TaskCompletionSource<ServerMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (receiveWaiterLock)
            {
                if (receiveWaiter != null)
                    throw new InvalidOperationException("There is already a pending WaitReceiveData call.");
                receiveWaiter = tcs;
            }
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cToken);
            if (timeout > TimeSpan.Zero) cts.CancelAfter(timeout);
            
            using (cts.Token.Register(() => tcs.TrySetResult(null)))
            {
                try
                {
                    if (await this.SendData(message) == false) return null;
                    return await tcs.Task.ConfigureAwait(false);
                }
                finally
                {
                    lock (receiveWaiterLock)
                    {
                        if (receiveWaiter == tcs) receiveWaiter = null;
                    }
                }
            }
        }

        /// <summary>
        /// Delivers the message to a pending <see cref="WaitReceiveData"/> call, if any.
        /// </summary>
        /// <returns>True if a waiter consumed the message, false if there was none.</returns>
        private bool NotifyReceiveWaiter(ServerMessage message)
        {
            TaskCompletionSource<ServerMessage?>? waiter;
            lock (receiveWaiterLock)
            {
                waiter = receiveWaiter;
                receiveWaiter = null;
            }
            return waiter != null && waiter.TrySetResult(message);
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
                if (Params.MessageTerminator != null && Params.MessageTerminator.Length > 0)
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

                            ParseAndNotifyMessage(PartialReceivedData.ToArray(), DateTime.UtcNow);
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
                    // go straight to the parsing as no terminator is expected 
                    PartialReceivedData.AddRange(data);
                    ParseAndNotifyMessage(PartialReceivedData.ToArray(), DateTime.UtcNow);
                    PartialReceivedData.Clear();
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

            // if there is a pending WaitReceiveData, the message is delivered there and
            // the asynchronous MessageReceived event is suppressed to avoid a double delivery.
            if (NotifyReceiveWaiter(args.Message)) return;
            else RaiseMessageReceivedEvent(args);
        }

        // events raising

        protected void RaiseDeviceDiscoveredEvent(DeviceDiscoveredEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            this.RaiseEvent(this._deviceDiscovered, this, args);
        }

        protected void RaiseDiscoverDevicesFinishedEvent(DiscoverDevicesFinishedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            this.RaiseEvent(this._discoverDevicesFinished, this, args);
        }

        protected void RaiseMessageReceivedEvent(MessageReceivedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            this.RaiseEvent(this._messageReceived, this, args);
        }

        protected void RaiseMessageSentEvent(MessageSentEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            this.RaiseEvent(this._messageSent, this, args);
        }

        protected void RaiseAdapterStateChangedEvent(AdapterStateChangedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            this.RaiseEvent(this._adapterStateChanged, this, args);
        }

        protected void RaiseConnectionChangedEvent(ConnectionChangedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException("args");
            this.RaiseEvent(this._connectionChanged, this, args);
        }

        protected void PrintLog(LogLevel level, string message, params object?[] args)
        {
            this.Params.Logger?.Log(level, "[" + this.GetType().Name + "] " + message, args);
        }
    }
}
