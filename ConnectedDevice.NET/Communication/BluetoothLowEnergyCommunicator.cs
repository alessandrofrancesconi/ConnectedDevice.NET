using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Exceptions;
using ConnectedDevice.NET.Models;
using Microsoft.Extensions.Logging;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using Plugin.BLE.Abstractions.Extensions;

namespace ConnectedDevice.NET.Communication
{
    public struct ServiceAndCharacteristics
    {
        public Guid ServiceGuid;
        public Guid? WriteCharacteristicGuid;
        public Guid? ReadCharacteristicGuid;
        public Guid? NotifyCharacteristicGuid;

        public ServiceAndCharacteristics(Guid service, Guid? read, Guid? notify, Guid? write)
        {
            ServiceGuid = service;
            ReadCharacteristicGuid = read;
            NotifyCharacteristicGuid = notify;
            WriteCharacteristicGuid = write;
        }
    }

    public class BluetoothLowEnergyCommunicatorParams : DeviceCommunicatorParams
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
        public ScanFilterOptions? ScanFilterOptions { get; set; } = new ScanFilterOptions();
        public ConnectParameters ConnectParameters { get; set; } = new ConnectParameters();
        public int ConnectTimeout { get; set; } = 5000;
        public new Func<RemoteDevice, bool>? DeviceFilter { get; set; } = null;

        public List<ServiceAndCharacteristics> SupportedCharacteristics = new List<ServiceAndCharacteristics>()
        {
            // Nordic nRF
            new ServiceAndCharacteristics(
                new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e"),
                null,
                new Guid("6e400003-b5a3-f393-e0a9-e50e24dcca9e"),
                new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e")),

            // TI CC245X
            new ServiceAndCharacteristics(
                new Guid("0000ffe0-0000-1000-8000-00805f9b34fb"),
                new Guid("0000ffe1-0000-1000-8000-00805f9b34fb"),
                new Guid("0000ffe1-0000-1000-8000-00805f9b34fb"),
                new Guid("0000ffe1-0000-1000-8000-00805f9b34fb")),
            
            // nRF52832
            new ServiceAndCharacteristics(
                new Guid("0000fff0-0000-1000-8000-00805f9b34fb"),
                new Guid("0000fff1-0000-1000-8000-00805f9b34fb"),
                new Guid("0000fff1-0000-1000-8000-00805f9b34fb"),
                new Guid("0000fff2-0000-1000-8000-00805f9b34fb")),

            // Microchip RN4870/71, BM70/71
            new ServiceAndCharacteristics(
                new Guid("49535343-fe7d-4ae5-8fa9-9fafd205e455"),
                new Guid("49535343-1e4d-4bd9-ba61-23c647249616"),
                new Guid("49535343-1e4d-4bd9-ba61-23c647249616"),
                new Guid("49535343-8841-43f4-a8d4-ecbe34729bb3"))
        };

        public static readonly BluetoothLowEnergyCommunicatorParams Default = new() {};
    }

    public abstract class BluetoothLowEnergyCommunicator : DeviceCommunicator
    {
        private IBluetoothLE Ble;
        
        private IDevice ConnectedDeviceNative;
        protected List<ICharacteristic> WriteCharacteristics;
        protected ICharacteristic? WriteCharacteristicToUse;
        protected List<ICharacteristic> NotifyCharacteristics;
        protected List<ICharacteristic> ReadCharacteristics;

        private List<RemoteDevice> FoundDevices;

        public BluetoothLowEnergyCommunicator(IBluetoothLE ble, BluetoothLowEnergyCommunicatorParams? p = null) : base(p ?? BluetoothLowEnergyCommunicatorParams.Default)
        {
            if (ble == null) throw new ArgumentNullException(nameof(ble));

            this.Ble = ble;
            this.Ble.StateChanged += Ble_StateChanged;
            this.Ble.Adapter.DeviceDiscovered += Adapter_DeviceDiscovered;
            this.Ble.Adapter.DeviceConnectionError += Adapter_DeviceConnectionLost;
            this.Ble.Adapter.DeviceConnectionLost += Adapter_DeviceConnectionLost;

            this.WriteCharacteristics = new List<ICharacteristic>();
            this.NotifyCharacteristics = new List<ICharacteristic>();
            this.ReadCharacteristics = new List<ICharacteristic>();

            this.FoundDevices = new List<RemoteDevice>();

            this.Params = p;
        }

        public override string GetInterfaceName()
        {
            return "Bluetooth LE";
        }

        private void Adapter_DeviceConnectionLost(object? sender, DeviceErrorEventArgs e)
        {
            this.PrintLog(LogLevel.Error, "Device connection lost. {0}", e.ErrorMessage);
            var exc = new NotConnectedException(e.ErrorMessage);
            _ = this.DisconnectFromDeviceNative(exc);
        }

        private void Adapter_DeviceDiscovered(object? sender, DeviceEventArgs e)
        {
            var dev = new RemoteDevice(e.Device.Id.ToString(), e.Device.Name);

            this.PrintLog(LogLevel.Information, "Device discovered: {0}", dev);
            var args = new DeviceDiscoveredEventArgs(this, dev);
            this.RaiseDeviceDiscoveredEvent(args);
        }

        private void Ble_StateChanged(object? sender, BluetoothStateChangedArgs e)
        {
            this.PrintLog(LogLevel.Information, "Adapter state changed from {0} to {1}", e.OldState, e.NewState);

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
            this.PrintLog(LogLevel.Debug, "Request to discover devices...");

            if (this.Ble.Adapter.IsScanning)
            {
                this.PrintLog(LogLevel.Warning, "Already scanning.");
                return;
            }

            this.FoundDevices.Clear();
            var bleParams = (BluetoothLowEnergyCommunicatorParams)this.Params;
            if (bleParams.ScanMatchMode == BluetoothLowEnergyCommunicatorParams.BtScanMatchMode.STICKY)
                this.Ble.Adapter.ScanMatchMode = ScanMatchMode.STICKY;
            if (bleParams.ScanMatchMode == BluetoothLowEnergyCommunicatorParams.BtScanMatchMode.AGRESSIVE)
                this.Ble.Adapter.ScanMatchMode = ScanMatchMode.AGRESSIVE;

            if (bleParams.ScanMode == BluetoothLowEnergyCommunicatorParams.BtScanMode.PASSIVE)
                this.Ble.Adapter.ScanMode = ScanMode.Passive;
            if (bleParams.ScanMode == BluetoothLowEnergyCommunicatorParams.BtScanMode.LOW_POWER)
                this.Ble.Adapter.ScanMode = ScanMode.LowPower;
            if (bleParams.ScanMode == BluetoothLowEnergyCommunicatorParams.BtScanMode.BALANCED)
                this.Ble.Adapter.ScanMode = ScanMode.Balanced;
            if (bleParams.ScanMode == BluetoothLowEnergyCommunicatorParams.BtScanMode.LOW_LATENCY)
                this.Ble.Adapter.ScanMode = ScanMode.LowLatency;

            CancellationTokenSource localToken = new CancellationTokenSource();
            CancellationTokenSource mergedTokens = CancellationTokenSource.CreateLinkedTokenSource(localToken.Token, cToken);
            if (bleParams.ScanTimeout > 0) localToken.CancelAfter(bleParams.ScanTimeout);

            await this.Ble.Adapter.StartScanningForDevicesAsync(
                bleParams.ScanFilterOptions,
                (dev) =>
                {
                    if (Params.DeviceFilter != null)
                    {
                        var rd = new RemoteDevice(dev.Id.ToString(), dev.Name);
                        bool valid = Params.DeviceFilter(rd);
                        if (!valid)
                        {
                            this.PrintLog(LogLevel.Warning, "Device found but filtered ({0}, {1})", dev.Id.ToString(), dev.Name);
                            return false;
                        }
                        else return true;
                    }
                    else return true;
                },
                false,
                mergedTokens.Token);

            this.PrintLog(LogLevel.Debug, "Discover finished.");
            var args = new DiscoverDevicesFinishedEventArgs(this, null);
            this.RaiseDiscoverDevicesFinishedEvent(args);
        }

        protected override async Task ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default)
        {
            try
            {
                var bleParams = (BluetoothLowEnergyCommunicatorParams)this.Params;
                CancellationTokenSource localToken = new CancellationTokenSource();
                CancellationTokenSource mergedTokens = CancellationTokenSource.CreateLinkedTokenSource(localToken.Token, cToken);
                if (bleParams.ConnectTimeout > 0) localToken.CancelAfter(bleParams.ConnectTimeout);

                this.ConnectedDeviceNative = await this.Ble.Adapter.ConnectToKnownDeviceAsync(new Guid(dev.Address), bleParams.ConnectParameters, mergedTokens.Token);
                this.PrintLog(LogLevel.Debug, "Connected to {0}. Setting RX/TX services...", dev.Address);

                // for each declared Service, setup the characteristics
                this.WriteCharacteristics.Clear();
                this.NotifyCharacteristics.Clear();
                this.ReadCharacteristics.Clear();
                foreach (var swr in bleParams.SupportedCharacteristics)
                {
                    var service = await this.ConnectedDeviceNative.GetServiceAsync(swr.ServiceGuid, mergedTokens.Token);
                    if (service != null)
                    {
                        // service exists, retrieve characteristics

                        // write
                        if (swr.WriteCharacteristicGuid.HasValue)
                        {
                            var write = await service.GetCharacteristicAsync(swr.WriteCharacteristicGuid.Value);
                            if (write != null && write.CanWrite)
                            {
                                this.WriteCharacteristics.Add(write);
                                this.PrintLog(LogLevel.Debug, "Registered Write characteristic {0}.", write.Id);
                            }
                        }

                        // read
                        if (swr.ReadCharacteristicGuid.HasValue)
                        {
                            var read = await service.GetCharacteristicAsync(swr.ReadCharacteristicGuid.Value);
                            if (read != null && read.CanRead)
                            {
                                this.ReadCharacteristics.Add(read);
                                this.PrintLog(LogLevel.Debug, "Registered Read characteristic {0}.", read.Id);
                            }
                        }

                        // notify
                        if (swr.NotifyCharacteristicGuid.HasValue)
                        {
                            var notify = await service.GetCharacteristicAsync(swr.NotifyCharacteristicGuid.Value);
                            if (notify != null && notify.CanUpdate)
                            {
                                this.NotifyCharacteristics.Add(notify);
                                notify.ValueUpdated += NotifyCharacteristic_ValueUpdated;
                                _ = notify.StartUpdatesAsync();
                                this.PrintLog(LogLevel.Debug, "Registered Notify characteristic {0}.", notify.Id);
                            }
                        }
                    }
                }

                if (this.WriteCharacteristics.Count == 0) this.PrintLog(LogLevel.Warning, "No suitable Write characteristics have ben found.");
                if (this.ReadCharacteristics.Count == 0) this.PrintLog(LogLevel.Warning, "No suitable Read characteristics have ben found.");
                if (this.NotifyCharacteristics.Count == 0) this.PrintLog(LogLevel.Warning, "No suitable Notify characteristics have ben found.");

                if (bleParams.SupportedCharacteristics.Any() &&
                    this.WriteCharacteristics.Count == 0 && this.ReadCharacteristics.Count == 0 && this.NotifyCharacteristics.Count == 0)
                {
                    // can do nothing -> raise error
                    var dump = await this.DumpAllServices(this.ConnectedDeviceNative);
                    throw new NotConnectedException("There is no supported characteristic on this device.\n" + dump);
                }
                else
                {
                    this.ConnectedDevice = dev;
                    this.PrintLog(LogLevel.Debug, "Connection to '{0}' completed", this.ConnectedDevice.ToString());
                    var args = new ConnectionChangedEventArgs(this, ConnectionState.CONNECTED, null);
                    this.RaiseConnectionChangedEvent(args);
                }

            }
            catch (Exception e)
            {
                this.PrintLog(LogLevel.Error, "Error while connecting: " + e.Message);
                _ = this.DisconnectFromDeviceNative(e);
            }
        }

        private async Task<string> DumpAllServices(IDevice device)
        {
            var dump = "Services found in Device '" + device.Name + "':\n";
            var services = await device.GetServicesAsync();
            foreach (var service in services)
            {
                dump += "\tService: '" + service.Id.ToString() + "'\n";
                var chars = await service.GetCharacteristicsAsync();
                foreach(var chr in chars)
                {
                    dump += "\t\tCharacteristic: '" + chr.Id.ToString() + "' CanWrite=" + chr.CanWrite + "/" + "' CanRead=" + chr.CanRead + "/" + "' CanUpdate=" + chr.CanUpdate + "\n";
                }
            }

            return dump;
        }

        private void NotifyCharacteristic_ValueUpdated(object? sender, CharacteristicUpdatedEventArgs e)
        {
            this.HandleReceivedData(e.Characteristic.Value);
        }

        protected override async Task DisconnectFromDeviceNative(Exception? e = null)
        {
            if (this.ConnectedDeviceNative != null)
            {
                // stop listening from read characteristics
                foreach (var nChar in this.NotifyCharacteristics)
                {
                    try
                    {
                        await nChar.StopUpdatesAsync();
                    }
                    catch (Exception nEx)
                    {
                        this.PrintLog(LogLevel.Warning, "Cannot stop listening to Characteristic '{0}' while disconnecting: {1}", nChar.Id, nEx.Message);
                    }
                }
                this.WriteCharacteristics.Clear();
                this.ReadCharacteristics.Clear();
                this.NotifyCharacteristics.Clear();

                string name = this.ConnectedDeviceNative.Name;
                await this.Ble.Adapter.DisconnectDeviceAsync(this.ConnectedDeviceNative);
                this.PrintLog(LogLevel.Debug, "Disconnection from '{0}' completed", name);
                this.ConnectedDeviceNative = null;
            }

            this.ConnectedDevice = null;

            var args = new ConnectionChangedEventArgs(this, ConnectionState.DISCONNECTED, e);
            this.RaiseConnectionChangedEvent(args);
        }

        public async Task<bool> SendData(ClientMessage message, Guid characteristic)
        {
            this.WriteCharacteristicToUse = this.WriteCharacteristics.Where(c => c.Id == characteristic).FirstOrDefault();
            if (this.WriteCharacteristicToUse == null) throw new NullReferenceException("Invalid Write characteristic " + characteristic + ". Cannot send data.");
            
            return await this.SendData(message);
        }

        protected override async Task SendDataNative(ClientMessage message)
        {
            ICharacteristic? characteristic;
            if (this.WriteCharacteristicToUse != null) characteristic = this.WriteCharacteristicToUse;
            else characteristic = this.WriteCharacteristics.FirstOrDefault();

            if (characteristic == null) throw new NullReferenceException("Write characteristic is not set. Cannot send data.");

            try
            {
                var res = await characteristic.WriteAsync(message.Data);
                if (res != 0) throw new Exception("Bluetooth sent error with code " + res);
            }
            catch (Exception ex)
            {
                this.PrintLog(LogLevel.Error, "Error sending data: '{0}'", ex.Message);
            }
            finally
            {
                // reset WriteChar to use
                this.WriteCharacteristicToUse = null;
            }
        }

        public async Task<(byte[], int)> ReadData(ClientMessage message, Guid? readCharacteristic, CancellationToken token = default)
        {
            ICharacteristic? characteristic = null;
            if (readCharacteristic != null) characteristic = this.ReadCharacteristics.Where(c => c.Id == readCharacteristic).FirstOrDefault();
            else characteristic = this.ReadCharacteristics.FirstOrDefault();

            if (characteristic == null) throw new NullReferenceException("Read characteristic is not set. Cannot read data.");

            try
            {
                return await characteristic.ReadAsync(token);
            }
            catch (CharacteristicReadException ex)
            {
                this.PrintLog(LogLevel.Error, "Error reading data: '{0}'", ex.Message);
                return new(null, ex.HResult) { };
            }
            catch (Exception ex)
            {
                this.PrintLog(LogLevel.Error, "Error reading data: '{0}'", ex.Message);
                return new (null, 0) { };
            }
        }

        public override ConnectionState GetConnectionState()
        {
            if (this.ConnectedDeviceNative?.State == DeviceState.Connected) return ConnectionState.CONNECTED;
            else return ConnectionState.DISCONNECTED;
        }

        public override void Dispose()
        {
            this.Ble.StateChanged -= Ble_StateChanged;
            this.Ble.Adapter.DeviceDiscovered -= Adapter_DeviceDiscovered;
            this.Ble.Adapter.DeviceConnectionError -= Adapter_DeviceConnectionLost;
            this.Ble.Adapter.DeviceConnectionLost -= Adapter_DeviceConnectionLost;
        }
    }
}
