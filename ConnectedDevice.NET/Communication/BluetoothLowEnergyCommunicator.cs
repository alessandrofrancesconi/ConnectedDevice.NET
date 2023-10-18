﻿using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Exceptions;
using ConnectedDevice.NET.Models;
using Microsoft.Extensions.Logging;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Communication
{
    public class BluetoothLowEnergyCommunicatorParams : BaseCommunicatorParams
    {
        public ScanMode ScanMode { get; set; } = ScanMode.Balanced;
        public int ScanTimeout { get; set; } = 3000;
        public ScanFilterOptions? ScanFilterOptions { get; set; } = default;
        public Func<RemoteDevice, bool>? DeviceFilter { get; set; } = null;
        public ConnectParameters ConnectParameters { get; set; } = default;

        public Guid WriteCharacteristicGuid { get; set; } = new Guid("0000ffe1-0000-1000-8000-00805f9b34fb");
        public Guid ReadCharacteristicGuid { get; set; } = new Guid("0000ffe1-0000-1000-8000-00805f9b34fb");
    }

    public abstract class BluetoothLowEnergyCommunicator : BaseCommunicator
    {
        private IBluetoothLE Ble;
        
        private Task ConnectionTask;
        private IDevice ConnectedDeviceNative;
        private ICharacteristic WriteCharacteristic;

        private List<RemoteDevice> FoundDevices;

        protected BluetoothLowEnergyCommunicatorParams Params;

        public BluetoothLowEnergyCommunicator(IBluetoothLE ble, BluetoothLowEnergyCommunicatorParams p = default) : base(ConnectionType.BLUETOOTH_LE, p)
        {
            if (ble == null) throw new ArgumentNullException(nameof(ble));

            this.Ble = ble;
            this.Ble.StateChanged += Ble_StateChanged;
            this.Ble.Adapter.DeviceDiscovered += Adapter_DeviceDiscovered;
            this.Ble.Adapter.DeviceConnectionError += Adapter_DeviceConnectionLost;
            this.Ble.Adapter.DeviceConnectionLost += Adapter_DeviceConnectionLost;

            this.FoundDevices = new List<RemoteDevice>();

            this.Params = p;
        }

        private void Adapter_DeviceConnectionLost(object? sender, DeviceErrorEventArgs e)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Error, "Device connection lost. {0}", e.ToString());
            var exc = new ConnectionLostException(e.ErrorMessage);
            this.DisconnectFromDeviceNative(exc);
        }

        private void Adapter_DeviceDiscovered(object? sender, DeviceEventArgs e)
        {
            var dev = new RemoteDevice(ConnectionType.BLUETOOTH_LE, e.Device.Name, e.Device.Id.ToString());

            ConnectedDeviceManager.PrintLog(LogLevel.Information, "Device discovered: {0}", dev);
            var args = new DeviceDiscoveredEventArgs(dev);
            this.RaiseDeviceDiscoveredEvent(args);
        }

        private void Ble_StateChanged(object? sender, BluetoothStateChangedArgs e)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Information, "Adapter state changed from {0} to {1}", e.OldState, e.NewState);

            AdapterStateChangedEventArgs args;
            switch (e.NewState)
            {
                case BluetoothState.On:
                    args = new AdapterStateChangedEventArgs(AdapterState.ON);
                    break;
                case BluetoothState.Off:
                    args = new AdapterStateChangedEventArgs(AdapterState.OFF);
                    break;
                case BluetoothState.Unavailable:
                    args = new AdapterStateChangedEventArgs(AdapterState.MISSING);
                    break;
                default:
                    return;
            }

            this.RaiseAdapterStateChangedEvent(args);
        }

        public override void StartDiscoverDevices(CancellationToken cToken)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Request to discover devices...");

            if (this.Ble.Adapter.IsScanning)
            {
                ConnectedDeviceManager.PrintLog(LogLevel.Warning, "Already scanning.");
                return;
            }

            this.FoundDevices.Clear();
            new Task(async () =>
            {
                this.Ble.Adapter.ScanMode = Params.ScanMode;
                this.Ble.Adapter.ScanTimeout = Params.ScanTimeout;
                await this.Ble.Adapter.StartScanningForDevicesAsync(
                    Params.ScanFilterOptions, 
                    (dev) => 
                    {
                        if (Params.DeviceFilter != null)
                        {
                            var rd = new RemoteDevice(ConnectionType.BLUETOOTH_LE, dev.Name, dev.Id.ToString());
                            bool valid = Params.DeviceFilter(rd);
                            if (!valid)
                            {
                                ConnectedDeviceManager.PrintLog(LogLevel.Warning, "Device found but filtered ({0}, {1})", dev.Id.ToString(), dev.Name);
                                return false;
                            }
                            else return true;
                        }
                        else return true;
                    }, 
                    false,
                    cToken);

                ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Discover finished.");
                var args = new DiscoverDevicesFinishedEventArgs(null);
                this.RaiseDiscoverDevicesFinishedEvent(args);
            }).Start();
        }

        protected override void ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default)
        {
            if (this.ConnectionTask != null && this.ConnectionTask.Status == TaskStatus.Running)
            {
                ConnectedDeviceManager.PrintLog(LogLevel.Warning, "A connection attempt is already running.");
                return;
            }

            this.ConnectionTask = Task.Run(async () =>
            {
                try
                {
                    this.ConnectedDeviceNative = await this.Ble.Adapter.ConnectToKnownDeviceAsync(new Guid(dev.Address), this.Params.ConnectParameters, cToken);
                    ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Connected to {0}. Setting RX/TX services...", dev.Address);

                    var services = await this.ConnectedDeviceNative.GetServicesAsync(cToken);
                    ICharacteristic? writeChar = null, readChar = null;
                    foreach (var service in services)
                    {
                        if (writeChar == null)
                        {
                            writeChar = await service.GetCharacteristicAsync(this.Params.WriteCharacteristicGuid);
                            if (writeChar != null)
                            {
                                this.WriteCharacteristic = writeChar;
                            }
                        }

                        if (readChar == null)
                        {
                            readChar = await service.GetCharacteristicAsync(this.Params.ReadCharacteristicGuid);
                            if (readChar != null)
                            {
                                readChar.ValueUpdated += ReadCharacteristic_ValueUpdated;
                                _ = readChar.StartUpdatesAsync();
                            }
                        }

                        if (writeChar != null && readChar != null)
                            break;
                    }

                    ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Connection to '{0}' completed", dev.ToString());
                    this.ConnectedDevice = dev;
                    var args = new ConnectionChangedEventArgs(ConnectionState.CONNECTED, null);
                    this.RaiseConnectionChangedEvent(args);
                }
                catch (Exception e)
                {
                    ConnectedDeviceManager.PrintLog(LogLevel.Error, "Error while connecting: " + e.Message);
                    this.DisconnectFromDeviceNative(e);
                }
            });
        }

        private void ReadCharacteristic_ValueUpdated(object? sender, CharacteristicUpdatedEventArgs e)
        {
            this.HandleReceivedData(e.Characteristic.Value);
        }

        protected override void DisconnectFromDeviceNative(Exception? e = null)
        {
            Task.Run(async () =>
            {
                await this.Ble.Adapter.DisconnectDeviceAsync(this.ConnectedDeviceNative);
                this.ConnectedDevice = null;

                var args = new ConnectionChangedEventArgs(ConnectionState.DISCONNECTED, e);
                this.RaiseConnectionChangedEvent(args);
            });
        }

        protected override async void SendDataNative(ClientMessage message)
        {
            var res = await this.WriteCharacteristic.WriteAsync(message.RawData);
            if (res != 0) throw new Exception("Data send error");
        }
    }
}
