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

        protected override async Task ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default)
        {
            throw new NotImplementedException();
        }

        public override AdapterState GetAdapterState()
        {
            return AdapterState.ON;
        }

        public override async Task DiscoverDevices(CancellationToken cToken = default)
        {
            throw new NotImplementedException();
        }

        protected override Task DisconnectFromDeviceNative(Exception? e = null)
        {
            throw new NotImplementedException();
        }

        protected override Task SendDataNative(ClientMessage message)
        {
            throw new NotImplementedException();
        }

        public override ConnectionState GetConnectionState()
        {
            throw new NotImplementedException();
        }
    }
}
