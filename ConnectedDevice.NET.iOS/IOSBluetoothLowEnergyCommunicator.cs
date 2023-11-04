using ConnectedDevice.NET.Communication;
using ConnectedDevice.NET.Exceptions;
using CoreBluetooth;
using CoreFoundation;
using Microsoft.Extensions.Logging;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ConnectedDevice.NET.iOS
{
    public class IOSBluetoothLowEnergyCommunicatorParams : BluetoothLowEnergyCommunicatorParams
    {
        public bool RequestPermission = true;
        public Action BluetoothPermissionChangeAction = null;
    }

    public class IOSBluetoothLowEnergyCommunicator : BluetoothLowEnergyCommunicator
    {
        private CBCentralManager BluetoothManager;

        private new IOSBluetoothLowEnergyCommunicatorParams Params = default;

        public IOSBluetoothLowEnergyCommunicator(IBluetoothLE ble, IOSBluetoothLowEnergyCommunicatorParams p = default) : base(ble, p)
        {
            this.Params = p;

            this.BluetoothManager = new CBCentralManager(
                new CbCentralDelegate(this.OnBluetoothAdapterStateChanged),
                DispatchQueue.DefaultGlobalQueue,
                new CBCentralInitOptions { ShowPowerAlert = true }
            );
        }

        private void OnBluetoothAdapterStateChanged()
        {
            var state = this.GetAdapterState();
            this.RaiseAdapterStateChangedEvent(new AdapterStateChangedEventArgs(state));
        }

        private bool RequestBluetoothPermissions()
        {
            if (!this.Params.RequestPermission) return true;

            bool allowed = false;
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
                allowed = (CBCentralManager.Authorization == CBManagerAuthorization.AllowedAlways);
            }
            else
            {
                allowed = true;
            }

            if (!allowed)
            {
                new CBCentralManager(
                    new PermissionCBCentralManager(this.Params.BluetoothPermissionChangeAction),
                    DispatchQueue.MainQueue,
                    new CBCentralInitOptions() { ShowPowerAlert = false }
                );
            }

            return allowed;
        }

        public override void StartDiscoverDevices(CancellationToken cToken = default)
        {
            if (!this.RequestBluetoothPermissions())
            {
                var args = new DiscoverDevicesFinishedEventArgs(new MissingPermissionException("Bluetooth permission is required."));
                this.RaiseDiscoverDevicesFinishedEvent(args);
                return;
            }
            else if (this.GetAdapterState() == AdapterState.OFF)
            {
                var args = new DiscoverDevicesFinishedEventArgs(new MissingPermissionException("Bluetooth adapter is disabled."));
                this.RaiseDiscoverDevicesFinishedEvent(args);
                return;
            }

            base.StartDiscoverDevices(cToken);
        }

        public override AdapterState GetAdapterState()
        {
            switch(BluetoothManager.State)
            {
                case CBManagerState.PoweredOn:
                    return AdapterState.ON;
                case CBManagerState.PoweredOff:
                case CBManagerState.Resetting:
                    return AdapterState.OFF;
                default:
                    return AdapterState.MISSING;
            }
        }
    }

    internal class CbCentralDelegate : CBCentralManagerDelegate
    {
        private Action OnChanged;

        public CbCentralDelegate(Action a)
        {
            this.OnChanged = a;
        }

        public override void UpdatedState(CBCentralManager central)
        {
            this.OnChanged?.Invoke();
        }
    }

    internal class PermissionCBCentralManager : CBCentralManagerDelegate
    {
        Action OnChanged = null;

        public PermissionCBCentralManager(Action a)
        {
            OnChanged = a;
        }

        public override void UpdatedState(CBCentralManager central)
        {
            OnChanged?.Invoke();
        }
    }
}
