using Android;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using ConnectedDevice.NET.Communication;
using ConnectedDevice.NET.Exceptions;
using Microsoft.Extensions.Logging;
using Plugin.BLE.Abstractions.Contracts;

namespace ConnectedDevice.NET.Android
{
    public class AndroidBluetoothLowEnergyCommunicatorParams : BluetoothLowEnergyCommunicatorParams
    {
        public bool RequestPermission = true;
        public bool CheckLocationSettings = true;
        public Func<Activity> GetCurrentActivityMethod = null;
    }

    public class AndroidBluetoothLowEnergyCommunicator : BluetoothLowEnergyCommunicator
    {
        public const int BLUETOOTH_PERMISSIONS_REQUEST_CODE = 0xCDB1; //ConnectedDeviceBluetooth1
        
        public EventHandler<EventArgs>? OnLocationServiceDisabled;

        private new AndroidBluetoothLowEnergyCommunicatorParams Params = default;

        public AndroidBluetoothLowEnergyCommunicator(IBluetoothLE ble, AndroidBluetoothLowEnergyCommunicatorParams p = default) : base(ble, p)
        {
            this.Params = p;
        }

        private bool RequestBluetoothPermissions()
        {
            if (!this.Params.RequestPermission) return true;

            var permissionsStatus = new Dictionary<string, Permission>();
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                permissionsStatus.Add(Manifest.Permission.BluetoothScan, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.BluetoothScan));
                permissionsStatus.Add(Manifest.Permission.BluetoothConnect, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.BluetoothConnect));
                permissionsStatus.Add(Manifest.Permission.BluetoothAdmin, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.BluetoothAdmin));
            }
            else
            {
                permissionsStatus.Add(Manifest.Permission.Bluetooth, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.Bluetooth));
                permissionsStatus.Add(Manifest.Permission.BluetoothAdmin, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.BluetoothAdmin));
                permissionsStatus.Add(Manifest.Permission.AccessFineLocation, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.AccessFineLocation));
            }

            var toBeGranted = permissionsStatus.Where(i => i.Value != Permission.Granted).Select(i => i.Key);
            if (toBeGranted.Count() > 0)
            {
                ConnectedDeviceManager.PrintLog(LogLevel.Warning, "Some permission are not granted, sending request for [{0}]", string.Join(',', toBeGranted));
                ActivityCompat.RequestPermissions(
                    this.Params.GetCurrentActivityMethod?.Invoke(),
                    toBeGranted.ToArray(),
                    BLUETOOTH_PERMISSIONS_REQUEST_CODE);

                return false;
            }

            return true;
        }

        public override void StartDiscoverDevices(CancellationToken cToken = default)
        {
            if (!this.RequestBluetoothPermissions())
            {
                var args = new DiscoverDevicesFinishedEventArgs(new MissingPermissionException("Some permissions are required."));
                this.RaiseDiscoverDevicesFinishedEvent(args);
                return;
            }
            else if (this.GetAdapterState() == AdapterState.OFF)
            {
                var args = new DiscoverDevicesFinishedEventArgs(new MissingPermissionException("Bluetooth adapter is disabled."));
                this.RaiseDiscoverDevicesFinishedEvent(args);
                return;
            }
            else if (!this.CheckLocationSettings())
            {
                var args = new DiscoverDevicesFinishedEventArgs(new MissingPermissionException("Location service is disabled."));
                this.RaiseDiscoverDevicesFinishedEvent(args);
                return;
            }

            base.StartDiscoverDevices(cToken);
        }

        public override AdapterState GetAdapterState()
        {
            var a = BluetoothAdapter.DefaultAdapter;
            if (a == null) return AdapterState.MISSING;
            else if (a.IsEnabled) return AdapterState.ON;
            else return AdapterState.OFF;
        }

        private bool CheckLocationSettings()
        {
            if (!this.Params.CheckLocationSettings) return true;

            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                LocationManager locationManager = (LocationManager)Application.Context.GetSystemService(Context.LocationService);
                if (!locationManager.IsProviderEnabled(LocationManager.GpsProvider))
                {
                    ConnectedDeviceManager.PrintLog(LogLevel.Warning, "Location Service found to be disabled. Tell this to the user!");
                    return false;
                }
            }
            return true;
        }
    }
}