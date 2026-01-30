using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Exceptions;
using ConnectedDevice.NET.Models;
using Microsoft.Extensions.Logging;
using System.IO.Ports;

namespace ConnectedDevice.NET.Communication
{
    public class UsbCommunicatorParams : DeviceCommunicatorParams
    {
        public int BaudRate = 9600;
        public Parity Parity = Parity.None;
        public int DataBits = 8;
        public StopBits StopBits = StopBits.One;
        public int WriteTimeout = 1000;
        public int ReadTimeout = 1000;
        public Handshake Handshake = Handshake.None;
        public bool MonitorPort = true;

        public static readonly UsbCommunicatorParams Default = new() { };
    }

    public class UsbCommunicator : DeviceCommunicator
    {
        private SerialPort Serial;

        private Task? PortMonitor;
        private CancellationTokenSource? PortMonitorCts;
        private readonly int MONITOR_PERIOD = 1000;

        public UsbCommunicator(UsbCommunicatorParams? p = null) : base(p ?? UsbCommunicatorParams.Default)
        {
            this.Serial = new SerialPort();
            this.Serial.BaudRate = p.BaudRate;
            this.Serial.Parity = p.Parity;
            this.Serial.DataBits = p.DataBits;
            this.Serial.StopBits = p.StopBits;
            this.Serial.WriteTimeout = p.WriteTimeout;
            this.Serial.ReadTimeout = p.ReadTimeout;
            this.Serial.Handshake = p.Handshake;
            this.Serial.DataReceived += Serial_DataReceived;
            this.Serial.ErrorReceived += Serial_ErrorReceived;
        }

        public override string GetInterfaceName()
        {
            return "USB";
        }

        protected override Task ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default)
        {
            this.ConnectedDevice = dev;
            this.PrintLog(LogLevel.Debug, "Connecting to {0}...", this.ConnectedDevice.Address);

            try
            {
                this.Serial.PortName = dev.Address;
                this.Serial.Open();
                this.RaiseConnectionChangedEvent(new ConnectionChangedEventArgs(this, ConnectionState.CONNECTED, null));
                this.StartMonitor();
            }
            catch (Exception ex)
            {
                this.PrintLog(LogLevel.Error, "Error while connecting: {0}", ex.Message);
                this.DisconnectFromDeviceNative(ex);
            }

            return Task.CompletedTask;
        }

        private void StartMonitor()
        {
            this.StopMonitor();

            if (this.ConnectedDevice == null || !this.Serial.IsOpen)
            {
                this.PrintLog(LogLevel.Warning, "Cannot start USB Port monitor: device is not connected.");
                return;
            }

            this.PortMonitorCts = new CancellationTokenSource();
            this.PortMonitor = Task.Run(() => MonitorLoopAsync(this.PortMonitorCts.Token));
        }

        private void StopMonitor()
        {
            try { this.PortMonitorCts?.Cancel(); } catch { }
            this.PortMonitorCts?.Dispose();
            this.PortMonitorCts = null;
            this.PortMonitor = null;
        }

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(MONITOR_PERIOD, token);

                    if (this.ConnectedDevice == null) // Known disconnection happened
                        break;

                    if (this.Serial.IsOpen == false)
                    {
                        throw new NotConnectedException("USB Port monitor detected closed port.");
                    }
                    else if (!SerialPort.GetPortNames().Contains(Serial.PortName))
                    {
                        throw new NotConnectedException("USB Port monitor detected disappeared port.");
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (NotConnectedException ex)
                {
                    this.PrintLog(LogLevel.Warning, ex.Message);
                    this.DisconnectFromDeviceNative(ex);
                    break;
                }
            }
        }

        public override AdapterState GetAdapterState()
        {
            return AdapterState.ON;
        }

        public override Task DiscoverDevices(CancellationToken cToken = default)
        {
            Exception? error = null;

            try
            {
                var ports = SerialPort.GetPortNames();
                foreach (var port in ports)
                {
                    if (port.Contains("BTHENUM")) continue;

                    var dev = new RemoteDevice(port, port);
                    if (Params.DeviceFilter != null && Params.DeviceFilter(dev) == false)
                    {
                        this.PrintLog(LogLevel.Warning, "Device found but filtered ({0})", dev.Address);
                        continue;
                    }

                    var args = new DeviceDiscoveredEventArgs(this, dev);
                    this.RaiseDeviceDiscoveredEvent(args);
                }
            }
            catch (Exception e)
            {
                error = e;
                this.PrintLog(LogLevel.Error, "Error getting serial ports:" + e.Message);
            }

            this.PrintLog(LogLevel.Debug, "Discover finished.");
            this.RaiseDiscoverDevicesFinishedEvent(new DiscoverDevicesFinishedEventArgs(this, error));
            return Task.CompletedTask;
        }

        protected override async Task SendDataNative(ClientMessage message)
        {
            if (this.ConnectedDevice == null) throw new NullReferenceException("Device not connected. Cannot send data.");
            if (this.Serial.IsOpen == false) throw new NotConnectedException("Serial port is closed. Cannot send data.");

            await this.Serial.BaseStream.WriteAsync(message.Data);
        }

        protected override Task DisconnectFromDeviceNative(Exception? e = null)
        {
            this.StopMonitor();
            if (this.ConnectedDevice == null) return Task.CompletedTask;

            try
            {
                if (this.Serial.IsOpen)
                {
                    this.Serial.Close();
                }

                this.PrintLog(LogLevel.Debug, "Disconnection from '{0}' completed", this.ConnectedDevice.Address);
                this.ConnectedDevice = null;
                this.RaiseConnectionChangedEvent(new ConnectionChangedEventArgs(this, ConnectionState.DISCONNECTED, e));
            }
            catch (Exception ex)
            {
                this.PrintLog(LogLevel.Error, "Error disconnecting from '{0}':", ex.Message);
                // TODO should we raise the ConnectionChange event anyway?
            }

            return Task.CompletedTask;
        }

        private void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var sp = (SerialPort)sender;
                string data = sp.ReadExisting();
                this.HandleReceivedData(data);
            }
            catch (Exception ex)
            {
                this.PrintLog(LogLevel.Error, "Error getting data: {0}", ex.Message);
            }
        }

        private void Serial_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            this.PrintLog(LogLevel.Error, "Error received from Serial port: {0}", e.EventType);
            // TODO: What to do?
        }

        public override ConnectionState GetConnectionState()
        {
            return this.Serial.IsOpen ? ConnectionState.CONNECTED : ConnectionState.DISCONNECTED;
        }

        public override void Dispose()
        {
            this.Serial.DataReceived -= Serial_DataReceived;
            this.Serial.ErrorReceived -= Serial_ErrorReceived;
            this.Serial.Close();
            this.Serial.Dispose();
        }
    }
}
