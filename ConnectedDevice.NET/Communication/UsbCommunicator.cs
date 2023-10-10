using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET.Communication
{
    public class UsbCommunicatorParams : BaseCommunicatorParams
    {

    }

    public class UsbCommunicator : BaseCommunicator
    {
        public UsbCommunicator(UsbCommunicatorParams p = default) : base(ConnectionType.USB, p)
        {
        }

        protected override void ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default)
        {
            throw new NotImplementedException();
        }

        public override AdapterState GetAdapterState()
        {
            return AdapterState.ON;
        }

        public override void StartDiscoverDevices(CancellationToken cToken = default)
        {
            throw new NotImplementedException();
        }

        protected override void DisconnectFromDeviceNative(Exception? e = null)
        {
            throw new NotImplementedException();
        }

        protected override void SendDataNative(ClientMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
