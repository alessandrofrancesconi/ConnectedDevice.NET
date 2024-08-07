using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Exceptions;
using ConnectedDevice.NET.Models;
using Microsoft.Extensions.Logging;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using Plugin.BLE.Abstractions.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Communication
{
    public class BluetoothLowEnergyCommunicatorParams : BaseCommunicatorParams
    {
        public enum BtScanMatchMode
        {
            STICKY,
            AGRESSIVE
        };
        public enum BtScanMode
        {
            PASSIVE,
            LOW_POWER,
            BALANCED,
            LOW_LATENCY
        };

        public BtScanMode ScanMode { get; set; } = BtScanMode.BALANCED;
        public BtScanMatchMode ScanMatchMode { get; set; } = BtScanMatchMode.STICKY;
        public int ScanTimeout { get; set; } = 3000;
        public ScanFilterOptions? ScanFilterOptions { get; set; } = default;
        public Func<RemoteDevice, bool>? DeviceFilter { get; set; } = null;
        public ConnectParameters ConnectParameters { get; set; } = default;
        public int ConnectTimeout { get; set; } = 5000;

        public List<Guid> SupportedServiceGuids { get; set; } = new List<Guid>() { new Guid("0000ffe0-0000-1000-8000-00805f9b34fb") };
        public List<Guid> SupportedWriteCharacteristicGuids { get; set; } = new List<Guid>() { new Guid("0000ffe1-0000-1000-8000-00805f9b34fb") };
        public List<Guid> SupportedReadCharacteristicGuids { get; set; } = new List<Guid>() { new Guid("0000ffe1-0000-1000-8000-00805f9b34fb") };
    }

    public abstract class BluetoothLowEnergyCommunicator : BaseCommunicator
    {
        private IBluetoothLE Ble;
        
        private IDevice ConnectedDeviceNative;
        protected ICharacteristic WriteCharacteristic;
        protected List<ICharacteristic> ReadCharacteristics;

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

            this.ReadCharacteristics = new List<ICharacteristic>();

            this.FoundDevices = new List<RemoteDevice>();

            this.Params = p;
        }

        private void Adapter_DeviceConnectionLost(object? sender, DeviceErrorEventArgs e)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Error, "Device connection lost. {0}", e.ErrorMessage);
            var exc = new NotConnectedException(e.ErrorMessage);
            _ = this.DisconnectFromDeviceNative(exc);
        }

        private void Adapter_DeviceDiscovered(object? sender, DeviceEventArgs e)
        {
            var dev = new RemoteDevice(ConnectionType.BLUETOOTH_LE, e.Device.Name, e.Device.Id.ToString());

            ConnectedDeviceManager.PrintLog(LogLevel.Information, "Device discovered: {0}", dev);
            var args = new DeviceDiscoveredEventArgs(this, dev);
            this.RaiseDeviceDiscoveredEvent(args);
        }

        private void Ble_StateChanged(object? sender, BluetoothStateChangedArgs e)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Information, "Adapter state changed from {0} to {1}", e.OldState, e.NewState);

            AdapterStateChangedEventArgs args;
            switch (e.NewState)
            {
                case BluetoothState.On:
                    args = new AdapterStateChangedEventArgs(this, AdapterState.ON);
                    break;
                case BluetoothState.Off:
                    args = new AdapterStateChangedEventArgs(this, AdapterState.OFF);
                    break;
                case BluetoothState.Unavailable:
                    args = new AdapterStateChangedEventArgs(this, AdapterState.MISSING);
                    break;
                default:
                    return;
            }

            this.RaiseAdapterStateChangedEvent(args);
        }

        public override async Task DiscoverDevices(CancellationToken cToken)
        {
            ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Request to discover devices...");

            if (this.Ble.Adapter.IsScanning)
            {
                ConnectedDeviceManager.PrintLog(LogLevel.Warning, "Already scanning.");
                return;
            }

            this.FoundDevices.Clear();
            if (this.Params.ScanMatchMode == BluetoothLowEnergyCommunicatorParams.BtScanMatchMode.STICKY)
                this.Ble.Adapter.ScanMatchMode = ScanMatchMode.STICKY;
            if (this.Params.ScanMatchMode == BluetoothLowEnergyCommunicatorParams.BtScanMatchMode.AGRESSIVE)
                this.Ble.Adapter.ScanMatchMode = ScanMatchMode.AGRESSIVE;

            if (this.Params.ScanMode == BluetoothLowEnergyCommunicatorParams.BtScanMode.PASSIVE)
                this.Ble.Adapter.ScanMode = ScanMode.Passive;
            if (this.Params.ScanMode == BluetoothLowEnergyCommunicatorParams.BtScanMode.LOW_POWER)
                this.Ble.Adapter.ScanMode = ScanMode.LowPower;
            if (this.Params.ScanMode == BluetoothLowEnergyCommunicatorParams.BtScanMode.BALANCED)
                this.Ble.Adapter.ScanMode = ScanMode.Balanced;
            if (this.Params.ScanMode == BluetoothLowEnergyCommunicatorParams.BtScanMode.LOW_LATENCY)
                this.Ble.Adapter.ScanMode = ScanMode.LowLatency;

            CancellationTokenSource localToken = new CancellationTokenSource();
            CancellationTokenSource mergedTokens = CancellationTokenSource.CreateLinkedTokenSource(localToken.Token, cToken);
            if (this.Params.ScanTimeout > 0) localToken.CancelAfter(this.Params.ScanTimeout);

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
                mergedTokens.Token);

            ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Discover finished.");
            var args = new DiscoverDevicesFinishedEventArgs(this, null);
            this.RaiseDiscoverDevicesFinishedEvent(args);
        }

        protected override async Task ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default)
        {
            try
            {
                CancellationTokenSource localToken = new CancellationTokenSource();
                CancellationTokenSource mergedTokens = CancellationTokenSource.CreateLinkedTokenSource(localToken.Token, cToken);
                if (this.Params.ConnectTimeout > 0) localToken.CancelAfter(this.Params.ConnectTimeout);

                this.ConnectedDeviceNative = await this.Ble.Adapter.ConnectToKnownDeviceAsync(new Guid(dev.Address), this.Params.ConnectParameters, mergedTokens.Token);
                this.ConnectedDevice = dev; 
                ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Connected to {0}. Setting RX/TX services...", this.ConnectedDevice.Address);

                IService? service = null;
                bool serviceFound = false;
                foreach (var sId in this.Params.SupportedServiceGuids)
                {
                    service = await this.ConnectedDeviceNative.GetServiceAsync(sId, mergedTokens.Token);
                    if (service != null)
                    {
                        serviceFound = true;
                        break;
                    }
                }

                if (!serviceFound) throw new Exception("Cannot find needed service");

                ICharacteristic? writeChar = null, readChar = null;
                bool readCharFound = (this.Params.SupportedReadCharacteristicGuids.Count == 0);
                bool writeCharFound = (this.Params.SupportedWriteCharacteristicGuids.Count == 0);
                foreach (var wId in this.Params.SupportedWriteCharacteristicGuids)
                {
                    writeChar = await service.GetCharacteristicAsync(wId);
                    if (writeChar != null && writeChar.CanWrite)
                    {
                        this.WriteCharacteristic = writeChar;
                        writeCharFound = true;
                        break;
                    }
                }
                foreach (var rId in this.Params.SupportedReadCharacteristicGuids)
                {
                    readChar = await service.GetCharacteristicAsync(rId);
                    if (readChar != null && readChar.CanUpdate)
                    {
                        this.ReadCharacteristics.Add(readChar);
                        readChar.ValueUpdated += ReadCharacteristic_ValueUpdated;
                        _ = readChar.StartUpdatesAsync();
                        readCharFound = true;
                        break;
                    }
                }

                if (!writeCharFound) throw new Exception("Cannot find needed write characteristic");
                if (!readCharFound) throw new Exception("Cannot find needed read characteristic");

                ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Connection to '{0}' completed", this.ConnectedDevice.ToString());
                var args = new ConnectionChangedEventArgs(this, ConnectionState.CONNECTED, null);
                this.RaiseConnectionChangedEvent(args);
            }
            catch (Exception e)
            {
                ConnectedDeviceManager.PrintLog(LogLevel.Error, "Error while connecting: " + e.Message);
                _ = this.DisconnectFromDeviceNative(e);
            }
        }

        private void ReadCharacteristic_ValueUpdated(object? sender, CharacteristicUpdatedEventArgs e)
        {
            this.HandleReceivedData(e.Characteristic.Value);
        }

        protected override async Task DisconnectFromDeviceNative(Exception? e = null)
        {
            if (this.ConnectedDeviceNative != null)
            {
                // stop listening from read characteristics
                foreach (var readChar in this.ReadCharacteristics)
                {
                    try
                    {
                        await readChar.StopUpdatesAsync();
                    }
                    catch (Exception readEx)
                    {
                        ConnectedDeviceManager.PrintLog(LogLevel.Warning, "Cannot stop listening to Characteristic '{0}' while disconnecting: {1}", readChar.Id, readEx.Message);
                    }
                }
                this.ReadCharacteristics.Clear();

                string name = this.ConnectedDeviceNative.Name;
                await this.Ble.Adapter.DisconnectDeviceAsync(this.ConnectedDeviceNative);
                ConnectedDeviceManager.PrintLog(LogLevel.Debug, "Disconnection from '{0}' completed", name);
                this.ConnectedDeviceNative = null;
            }

            this.ConnectedDevice = null;

            var args = new ConnectionChangedEventArgs(this, ConnectionState.DISCONNECTED, e);
            this.RaiseConnectionChangedEvent(args);
        }

        protected override async Task SendDataNative(ClientMessage message)
        {
            if (this.WriteCharacteristic == null) throw new NullReferenceException("Write characteristic is not set. Cannot send data.");

            var res = await this.WriteCharacteristic.WriteAsync(message.Data);
            if (res != 0) throw new Exception("Data send error");
        }

        public override ConnectionState GetConnectionState()
        {
            if (this.ConnectedDeviceNative?.State == DeviceState.Connected) return ConnectionState.CONNECTED;
            else return ConnectionState.DISCONNECTED;
        }
    }
}
