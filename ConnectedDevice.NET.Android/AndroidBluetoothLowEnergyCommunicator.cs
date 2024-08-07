using Android;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.Provider;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using ConnectedDevice.NET.Communication;
using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Exceptions;
using ConnectedDevice.NET.Models;
using Javax.Crypto;
using Microsoft.Extensions.Logging;
using Plugin.BLE;
using System.Reflection;

namespace ConnectedDevice.NET.Android
{
    public class AndroidBluetoothLowEnergyCommunicatorParams : BluetoothLowEnergyCommunicatorParams
    {
        public bool RequestPermission = true;
        public bool CheckLocationSettings = true;
        public Func<Activity> GetCurrentActivityMethod = null;
        public Action<Action> RunOnUIThreadMethod = null;
    }

    public class AndroidBluetoothLowEnergyCommunicator : BluetoothLowEnergyCommunicator
    {
        public const int BLUETOOTH_PERMISSIONS_REQUEST_CODE = 0xCDB1; //ConnectedDeviceBluetooth1
        
        public EventHandler<EventArgs>? OnLocationServiceDisabled;

        private new AndroidBluetoothLowEnergyCommunicatorParams Params = default;

        public AndroidBluetoothLowEnergyCommunicator(AndroidBluetoothLowEnergyCommunicatorParams p = default) : base(CrossBluetoothLE.Current, p)
        {
            this.Params = p;
        }

        private void CheckPermissions()
        {
            if (!this.RequestBluetoothPermissions()) throw new MissingPermissionException("Some permissions are required.");
            if (this.GetAdapterState() == AdapterState.OFF) throw new AdapterDisabledException("Bluetooth adapter is disabled.");
            if (!this.CheckLocationSettings()) throw new LocationServiceDisabledException("Location service is disabled.");
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

        protected override async Task ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default)
        {
            try
            {
                this.CheckPermissions();
            }
            catch (Exception e)
            {
                var args = new ConnectionChangedEventArgs(this, ConnectionState.DISCONNECTED, e);
                this.RaiseConnectionChangedEvent(args);
                return;
            }

            await base.ConnectToDeviceNative(dev, cToken);
        }

        public override async Task DiscoverDevices(CancellationToken cToken = default)
        {
            try
            {
                this.CheckPermissions();
            }
            catch (Exception e)
            {
                var args = new DiscoverDevicesFinishedEventArgs(this, e);
                this.RaiseDiscoverDevicesFinishedEvent(args);
                return;
            }

            await base.DiscoverDevices(cToken);
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
                bool locationEnabled = true;
                if (OperatingSystem.IsAndroidVersionAtLeast(28))
                {
                    LocationManager lm = (LocationManager)Application.Context.GetSystemService(Context.LocationService);
                    locationEnabled = lm.IsLocationEnabled;
                }
                else
                {
                    int mode = Settings.Secure.GetInt(Application.Context.ContentResolver, Settings.Secure.LocationMode, (int)SecurityLocationMode.Off);
                    locationEnabled = (mode != (int)SecurityLocationMode.Off);
                }

                if (!locationEnabled)
                {
                    ConnectedDeviceManager.PrintLog(LogLevel.Warning, "Location Service is disabled.");
                    return false;
                }
            }
            return true;
        }

        protected override Task SendDataNative(ClientMessage message)
        {
            if (this.WriteCharacteristic == null) throw new NullReferenceException("Write characteristic is not set. Cannot send data.");

            var writeAction = new Action(async () =>
            {
                try
                {
                    var res = await this.WriteCharacteristic.WriteAsync(message.Data);
                    if (res != 0) throw new Exception("Data send error");
                }
                catch { }
            });

            // if a method to run the Action on the UI thread has been given, use it
            if (this.Params.RunOnUIThreadMethod != null) this.Params.RunOnUIThreadMethod.Invoke(writeAction);
            else writeAction.Invoke();

            return Task.CompletedTask;

        }
    }
}