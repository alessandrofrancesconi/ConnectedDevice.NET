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
        public bool RequestAdapterEnable = true;
        public bool CheckLocationSettings = true;
    }

    public class AndroidBluetoothLowEnergyCommunicator : BluetoothLowEnergyCommunicator
    {
        public const int BLUETOOTH_PERMISSIONS_REQUEST_CODE = 0xCDB1; //ConnectedDeviceBluetooth1
        
        public EventHandler<EventArgs>? OnLocationServiceDisabled;

        private AndroidBluetoothLowEnergyCommunicatorParams Params = default;

        public AndroidBluetoothLowEnergyCommunicator(IBluetoothLE ble, AndroidBluetoothLowEnergyCommunicatorParams p = default) : base(ble, p)
        {
            this.Params = p;
        }

        private bool RequestBluetoothPermissions(Activity currentActivity)
        {
            if (!this.Params.RequestPermission) return true;

            var permissionsStatus = new Dictionary<string, Permission>();
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                permissionsStatus.Add("BLUETOOTH_SCAN", ContextCompat.CheckSelfPermission(Application.Context, "BLUETOOTH_SCAN"));
                permissionsStatus.Add("BLUETOOTH_CONNECT", ContextCompat.CheckSelfPermission(Application.Context, "BLUETOOTH_CONNECT"));
            }
            else
            {
                permissionsStatus.Add("BLUETOOTH", ContextCompat.CheckSelfPermission(Application.Context, "BLUETOOTH"));
                permissionsStatus.Add("ACCESS_COARSE_LOCATION", ContextCompat.CheckSelfPermission(Application.Context, "ACCESS_COARSE_LOCATION"));
                permissionsStatus.Add("ACCESS_FINE_LOCATION", ContextCompat.CheckSelfPermission(Application.Context, "ACCESS_FINE_LOCATION"));
            }

            var toBeGranted = permissionsStatus.Where(i => i.Value != Permission.Granted).Select(i => i.Key);
            if (toBeGranted.Count() > 0)
            {
                ConnectedDeviceManager.PrintLog(LogLevel.Warning, "Some permission are not granted, sending request...");
                ActivityCompat.RequestPermissions(
                    currentActivity,
                    toBeGranted.ToArray(),
                    BLUETOOTH_PERMISSIONS_REQUEST_CODE);

                return false;
            }

            return true;
        }

        public override void StartDiscoverDevices(CancellationToken cToken = default)
        {
            throw new NotImplementedException("No not use this method on Android. Please use 'StartDiscoverDevices(Activity, CancellationToken)' instead.");
        }

        public void StartDiscoverDevices(Activity currentActivity, CancellationToken cToken = default)
        {
            if (!this.RequestAdapterEnable(currentActivity))
            {
                var args = new DiscoverDevicesFinishedEventArgs()
                {
                    Error = new AdapterDisabledException("Bluetooth adapter is disabled.")
                };
                this.OnDiscoverDevicesFinished(this, args);
                return;

            }
            else if (!this.CheckLocationSettings())
            {
                var args = new DiscoverDevicesFinishedEventArgs()
                {
                    Error = new LocationServiceDisabledException("Location service is disabled.")
                };
                this.OnDiscoverDevicesFinished(this, args);
                return;
            }
            else if (!this.RequestBluetoothPermissions(currentActivity))
            {
                var args = new DiscoverDevicesFinishedEventArgs()
                {
                    Error = new MissingPermissionException("Some permissions are required.")
                };
                this.OnDiscoverDevicesFinished(this, args);
                return;
            }

            base.StartDiscoverDevices(cToken);
        }

        public override AdapterState GetAdapterState()
        {
            BluetoothAdapter a = BluetoothAdapter.DefaultAdapter;
            if (a == null) return AdapterState.MISSING;
            else if (a.IsEnabled) return AdapterState.ON;
            else return AdapterState.OFF;
        }

        public bool RequestAdapterEnable(Activity currentActivity)
        {
            if (!this.Params.RequestAdapterEnable) return true;

            if (!this.RequestBluetoothPermissions(currentActivity)) return false;

            if (this.GetAdapterState() == AdapterState.ON) return true;
            else
            {
                BluetoothAdapter a = BluetoothAdapter.DefaultAdapter;
                return a.Enable();
            }
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