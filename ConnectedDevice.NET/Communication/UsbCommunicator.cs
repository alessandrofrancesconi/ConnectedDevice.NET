using ConnectedDevice.NET.Communication.Protocol;
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

        public static readonly UsbCommunicatorParams Default = new() { };
    }

    public class UsbCommunicator : DeviceCommunicator
    {
        private SerialPort Serial;

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
            }
            catch (Exception ex)
            {
                this.PrintLog(LogLevel.Error, "Error while connecting: {0}", ex.Message);
                this.DisconnectFromDeviceNative(ex);
            }

            return Task.CompletedTask;
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

        protected override Task SendDataNative(ClientMessage message)
        {
            if (this.ConnectedDevice == null) throw new NullReferenceException("Device not connected. Cannot send data.");
            if (this.Serial.IsOpen == false) throw new Exception("Serial port is closed. Cannot send data.");

            this.Serial.Write(message.Data, 0, message.Data.Length);
            return Task.CompletedTask;
        }

        protected override Task DisconnectFromDeviceNative(Exception? e = null)
        {
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
