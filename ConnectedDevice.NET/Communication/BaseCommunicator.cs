﻿using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Exceptions;
using ConnectedDevice.NET.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
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
        public byte[] MessageTerminator = null;
    }

    public abstract class BaseCommunicator
    {
        public ConnectionType ConnectionType { get; protected set; }
        public ConnectionState ConnectionState { get; protected set; }

        public RemoteDevice? ConnectedDevice { get; protected set; }
        protected IMessageParser? ConnectedDeviceParser { get; set; }

        public EventHandler<DeviceDiscoveredEventArgs>? OnDeviceDiscovered;
        public EventHandler<DiscoverDevicesFinishedEventArgs>? OnDiscoverDevicesFinished;
        public EventHandler<ConnectionChangedEventArgs>? OnConnectionChanged;
        public EventHandler<AdapterStateChangedEventArgs>? OnAdapterStateChanged;
        public EventHandler<MessageReceivedEventArgs>? OnMessageReceived;

        private List<byte> PartialReceivedData;

        private BaseCommunicatorParams Params;

        public BaseCommunicator(ConnectionType type, BaseCommunicatorParams p = default)
        {
            this.ConnectionType = type;
            this.Params = p;
            this.PartialReceivedData = new List<byte>();
            this.ConnectionState = ConnectionState.DISCONNECTED;
        }

        public abstract AdapterState GetAdapterState();
        public abstract void StartDiscoverDevices(CancellationToken cToken = default);

        public void ConnectToDevice(RemoteDevice dev, IMessageParser parser, CancellationToken cToken = default)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Start connecting to '{0}'...", dev.ToString());
            this.ConnectedDeviceParser = parser;
            this.ConnectToDeviceNative(dev, cToken);
        }
        protected abstract void ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default);

        public void DisconnectFromDevice()
        {
            if (this.ConnectedDevice == null)
            {
                ConnectedDeviceManager.PrintLog(LogLevel.Debug, "No device to disconnect from.");
                return;
            }

            ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Start disconnecting from '{0}'...", this.ConnectedDevice.ToString());
            this.DisconnectFromDeviceNative();
        }
        protected abstract void DisconnectFromDeviceNative(Exception? e = null);

        public virtual async Task SendData(ClientMessage message)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Sending data of type '{0}'...", message.GetType().ToString());
            await Task.Run(() =>
            {
                try
                {
                    this.SendDataNative(message);
                    return Task.FromResult(true);
                }
                catch (Exception e)
                {
                    ConnectedDeviceManager.PrintLog(LogLevel.Error, "Error sending data: {0}", e.Message);
                    return Task.FromException(e);
                }
            });
        }
        protected abstract void SendDataNative(ClientMessage message);

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

            if (this.Params.MessageTerminator?.Length > 0)
            {
                var startIndex = 0;
                do
                {
                    var terminatorIndex = data.AsSpan(startIndex).IndexOf(this.Params.MessageTerminator);
                    if (terminatorIndex > -1)
                    {
                        var dataLength = terminatorIndex + this.Params.MessageTerminator.Length;
                        var sub = new byte[dataLength];
                        Array.Copy(data, startIndex, sub, 0, dataLength);
                        this.PartialReceivedData.AddRange(sub);

                        this.ParseAndNotifyMessage(this.PartialReceivedData.ToArray());
                        this.PartialReceivedData.Clear();
                        startIndex += dataLength;
                    }
                    else
                    {
                        // no terminator found, just append to the partial data without parsing, and exit
                        this.PartialReceivedData.AddRange(data);
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

        private void ParseAndNotifyMessage(byte[] data)
        {
            var args = new MessageReceivedEventArgs();
            try
            {
                var message = this.ConnectedDeviceParser?.Parse(this.ConnectedDevice, data);
                args.Message = message;
            }
            catch (Exception e)
            {
                var msg = string.Format("Error while parsing the incoming message: {0}", e.Message);
                ConnectedDeviceManager.PrintLog(LogLevel.Error, msg);

                args.Error = new ProtocolException(msg, e);
            }
            finally
            {
                this.OnMessageReceived?.Invoke(this, args);
            }
        }

    }

    public class AdapterStateChangedEventArgs : EventArgs
    {
        public AdapterState NewState;
    };

    public class DeviceDiscoveredEventArgs : EventArgs
    {
        public RemoteDevice DiscoveredDevice;
    }

    public class DiscoverDevicesFinishedEventArgs : EventArgs
    {
        public Exception? Error;
    }

    public class ConnectionChangedEventArgs : EventArgs
    {
        public ConnectionState NewState;
        public Exception? Error;
    }

    public class DataSendFinishedEventArgs : EventArgs
    {
        public Guid TransmissionId;
        public Exception? Error;
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public ServerMessage Message;
        public ProtocolException? Error;
    }
}
